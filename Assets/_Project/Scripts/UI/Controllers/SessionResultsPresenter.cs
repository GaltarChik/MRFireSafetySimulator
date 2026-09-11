using MRFireSafety.Analytics.Models;
using MRFireSafety.Analytics.Systems;
using TMPro;
using UnityEngine;

namespace MRFireSafety.UI.Controllers
{
    /// <summary>
    /// Shows a compact post-session summary after analytics finalizes a training report.
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
                _sessionDataManager.SessionEnded += HandleSessionEnded;
            }
        }

        private void OnDisable()
        {
            if (_sessionDataManager != null)
            {
                _sessionDataManager.SessionEnded -= HandleSessionEnded;
            }
        }

        private void HandleSessionEnded(SessionMetrics metrics)
        {
            if (_resultsPanel != null)
            {
                _resultsPanel.SetActive(true);
            }

            if (_resultsText != null)
            {
                string outcome = metrics.IsFireSuppressed ? "SUCCESS" : "SESSION ENDED";
                _resultsText.text = $"{outcome}\nTime: {metrics.DurationSeconds:0.0}s\nAgent used: {metrics.AgentConsumed:0.00}\nIntegrity: {metrics.ObjectIntegrity:P0}\nAverage FPS: {metrics.AverageFramesPerSecond:0}";
            }
        }
    }
}
