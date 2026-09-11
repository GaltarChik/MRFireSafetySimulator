using System;
using System.Collections.Generic;
using MRFireSafety.Analytics.Models;
using MRFireSafety.Analytics.Services;
using TMPro;
using UnityEngine;

namespace MRFireSafety.UI.Controllers
{
    /// <summary>
    /// Loads and displays a compact recent-session history from JSON reports after the UI is ready.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SessionHistoryPresenter : MonoBehaviour
    {
        [SerializeField] private TMP_Text _historyText;
        [SerializeField, Range(1, 5)] private int _maximumDisplayedReports = 3;

        private readonly SessionReportService _sessionReportService = new SessionReportService();

        /// <summary>
        /// Assigns the text field used for recent session summaries.
        /// </summary>
        /// <param name="historyText">Text field that displays saved session summaries.</param>
        public void Configure(TMP_Text historyText)
        {
            _historyText = historyText;
        }

        private async void Start()
        {
            try
            {
                IReadOnlyList<SessionMetrics> reports = await _sessionReportService.LoadReportsAsync();
                PresentReports(reports);
            }
            catch (Exception exception)
            {
                Debug.LogWarning("MR Fire Safety: session history could not be loaded. " + exception.Message, this);
            }
        }

        private void PresentReports(IReadOnlyList<SessionMetrics> reports)
        {
            if (_historyText == null)
            {
                return;
            }

            if (reports.Count == 0)
            {
                _historyText.text = "RECENT SESSIONS\nNo saved sessions";
                return;
            }

            int reportCount = Mathf.Min(_maximumDisplayedReports, reports.Count);
            string history = "RECENT SESSIONS";
            for (int reportIndex = 0; reportIndex < reportCount; reportIndex++)
            {
                SessionMetrics report = reports[reportIndex];
                history += $"\n{reportIndex + 1}. {report.DurationSeconds:0}s | {report.ObjectIntegrity:P0} | {report.AverageFramesPerSecond:0} FPS";
            }

            _historyText.text = history;
        }
    }
}
