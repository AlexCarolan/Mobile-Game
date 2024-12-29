using Unity.Services.Core.Configuration.Editor;
using Unity.Services.Core.Editor.Environments.Save;
using Unity.Services.Core.Environments;

namespace Unity.Services.Core.Editor.Environments.Config
{
    class EnvironmentConfigProvider : IConfigurationProvider
    {
        readonly IEnvironmentSaveSystem m_EnvironmentSaveSystem;
        public int callbackOrder { get; }

        public EnvironmentConfigProvider() : this(new FileSystem()) {}

        public EnvironmentConfigProvider(IFileSystem fileSystem)
        {
            m_EnvironmentSaveSystem = new EnvironmentSaveSystem(fileSystem);
        }

        internal EnvironmentConfigProvider(IEnvironmentSaveSystem environmentSaveSystem)
        {
            m_EnvironmentSaveSystem = environmentSaveSystem;
        }

        public void OnBuildingConfiguration(ConfigurationBuilder builder)
        {
            builder.SetString(
                EnvironmentsOptionsExtensions.EnvironmentNameKey,
                GetEnvironmentName());
        }

        internal string GetEnvironmentName()
        {
            var loadedEnv = m_EnvironmentSaveSystem.LoadEnvironment();

            return string.IsNullOrEmpty(loadedEnv.EnvironmentName) ? EnvironmentsOptionsExtensions.EnvironmentDefaultNameValue : loadedEnv.EnvironmentName;
        }
    }
}
