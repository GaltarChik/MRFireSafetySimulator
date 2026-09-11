using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using MRFireSafety.Analytics.Models;
using UnityEngine;

namespace MRFireSafety.Analytics.Services
{
    /// <summary>
    /// Loads previously persisted training-session reports outside Unity's frame loop. This service
    /// keeps report retrieval separate from collection and storage responsibilities.
    /// </summary>
    public sealed class SessionReportService
    {
        private const string ReportsDirectoryName = "SessionReports";

        /// <summary>
        /// Asynchronously loads valid session reports ordered from newest to oldest.
        /// </summary>
        /// <returns>A task containing the loaded session reports.</returns>
        public async Task<IReadOnlyList<SessionMetrics>> LoadReportsAsync()
        {
            string reportsDirectory = Path.Combine(Application.persistentDataPath, ReportsDirectoryName);
            if (!Directory.Exists(reportsDirectory))
            {
                return Array.Empty<SessionMetrics>();
            }

            string[] reportPaths = Directory.GetFiles(reportsDirectory, "session_*.json", SearchOption.TopDirectoryOnly);
            Array.Sort(reportPaths, StringComparer.OrdinalDescending);
            List<SessionMetrics> reports = new List<SessionMetrics>(reportPaths.Length);

            foreach (string reportPath in reportPaths)
            {
                string json = await File.ReadAllTextAsync(reportPath);
                SessionMetrics report = JsonUtility.FromJson<SessionMetrics>(json);
                if (report != null)
                {
                    reports.Add(report);
                }
            }

            return reports;
        }
    }
}
