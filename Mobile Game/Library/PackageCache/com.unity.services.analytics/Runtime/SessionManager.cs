using UnityEngine;

namespace Unity.Services.Analytics.Internal
{
    internal interface ISessionManager
    {
        string SessionId { get; }

        void StartNewSession();
    }

    internal class SessionManager : ISessionManager
    {
        public string SessionId { get; private set; }

        public SessionManager()
        {
            StartNewSession();
        }

        public void StartNewSession()
        {
            SessionId = System.Guid.NewGuid().ToString();

#if UNITY_ANALYTICS_DEVELOPMENT
            Debug.Log("Analytics SDK started new session: " + SessionId);
#endif
        }
    }
}
