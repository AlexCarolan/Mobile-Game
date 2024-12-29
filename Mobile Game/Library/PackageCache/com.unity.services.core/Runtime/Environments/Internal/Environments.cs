namespace Unity.Services.Core.Environments.Internal
{
    /// <inheritdoc />
    class Environments : IEnvironments
    {
        string m_Current;

        public string Current
        {
            get => m_Current;
            internal set
            {
                m_Current = value;

#if ENABLE_CLOUD_SERVICES_IDENTIFIERS
                UnityEngine.Connect.Identifiers.SetEnvironmentName(value);
#endif
            }
        }
    }
}
