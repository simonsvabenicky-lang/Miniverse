using System.Collections.Generic;

namespace Miniverse.Hub.Analytics
{
    /// <summary>
    /// One sink for analytics events. LocalFileAnalyticsBackend is the only implementation
    /// today (no ads/cloud account needed to start collecting data); a real dashboard
    /// backend (Unity Analytics, Firebase, ...) is a second class implementing this, added
    /// to AnalyticsService — call sites in HubLauncher never change.
    /// </summary>
    public interface IAnalyticsBackend
    {
        void LogEvent(string eventName, IReadOnlyDictionary<string, object> parameters);
    }
}
