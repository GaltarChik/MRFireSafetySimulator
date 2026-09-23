using MRFireSafety.Analytics.Models;
using MRFireSafety.Analytics.Systems;
using TMPro;
using UnityEngine;

namespace MRFireSafety.UI.Controllers
{
    /// <summary>
    /// Shows a compact post-session summary after analytics finalizes a training report, and hides
    /// it again when the next run begins.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SessionResultsPresenter : MonoBehaviour
    {
        [SerializeField] private SessionDataManager _sessionDataManager;
        [SerializeField] private GameObject _resultsPanel;
        [SerializeField] private TMP_Text _resultsText;

        /// <summary>
        /// Assigns generated results controls without editor-only serialized-property access.
        /// </summary>
        /// <param name="resultsPanel">Panel to show when the session ends.</param>
        /// <param name="resultsText">Text that presents the session outcome.</param>
        public void Configure(GameObject resultsPanel, TMP_Text resultsText)
        {
            _resultsPanel = resultsPanel;
            _resultsText = resultsText;
        }

        private void Awake()
        {
            _sessionDataManager ??= FindFirstObjectByType<SessionDataManager>();
            if (_resultsPanel != null)
            {
                _resultsPanel.SetActive(false);
            }
        }

        private void OnEnable()
        {
            if (_sessionDataManager != null)
            {
                _sessionDataManager.SessionStarted += HandleSessionStarted;
                _sessionDataManager.SessionEnded += HandleSessionEnded;
            }
        }

        private void OnDisable()
        {
            if (_sessionDataManager != null)
            {
                _sessionDataManager.SessionStarted -= HandleSessionStarted;
                _sessionDataManager.SessionEnded -= HandleSessionEnded;
            }
        }

        private void HandleSessionStarted()
        {
            if (_resultsPanel != null)
            {
                _resultsPanel.SetActive(false);
            }
        }

        private void HandleSessionEnded(SessionMetrics metrics)
        {
            if (_resultsPanel != null)
            {
                _resultsPanel.SetActive(true);
            }

            if (_resultsText == null)
            {
                return;
            }

            _resultsText.text =
                $"{DescribeOutcome(metrics)}\n" +
                $"Time: {metrics.DurationSeconds:0.0} s\n" +
                $"Agent used: {metrics.AgentConsumed:0.00}\n" +
                $"Integrity: {metrics.ObjectIntegrity:P0}\n" +
                $"Average FPS: {metrics.AverageFramesPerSecond:0}\n" +
                "Press B to run again";
        }

        private static string DescribeOutcome(SessionMetrics metrics)
        {
            if (!System.Enum.TryParse(metrics.Outcome, out SessionOutcome outcome))
            {
                return metrics.IsFireSuppressed ? "FIRE SUPPRESSED" : "SESSION ENDED";
            }

            return outcome switch
            {
                SessionOutcome.Suppressed => "FIRE SUPPRESSED",
                SessionOutcome.AgentDepleted => "EXTINGUISHER EMPTY",
                SessionOutcome.ObjectDestroyed => "EQUIPMENT LOST",
                SessionOutcome.Interrupted => "SESSION INTERRUPTED",
                _ => "SESSION ENDED"
            };
        }
    }
}
