using Unity.Services.Core.Analytics.Internal;

namespace Unity.Services.Analytics.Internal
{
    internal class AnalyticsUserIdServiceComponent : IAnalyticsUserId
    {
        readonly IAnalyticsService m_AnalyticsService;

        public AnalyticsUserIdServiceComponent(IAnalyticsService analyticsService)
        {
            m_AnalyticsService = analyticsService;
        }

        public string GetAnalyticsUserId()
        {
            return m_AnalyticsService.GetAnalyticsUserID();
        }
    }
}
