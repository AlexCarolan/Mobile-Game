using System;
using Newtonsoft.Json;
using Unity.Services.Core.Editor.Settings;
using Unity.Services.Core.Internal.Serialization;

namespace Unity.Services.Core.Editor.Environments.Save
{
    class EnvironmentSaveSystem : IEnvironmentSaveSystem
    {
        const string k_EnvironmentSettingsPath = "ProjectSettings/Packages/com.unity.services.core/Settings.json";

        readonly IFileSystem m_FileSystem;
        readonly IJsonSerializer m_JsonSerializer;

        EnvironmentSettings m_CachedEnvironmentSettings;
        bool m_IsDirty = true;

        public EnvironmentSaveSystem(IFileSystem fileSystem)
        {
            m_FileSystem = fileSystem;
            m_JsonSerializer = new NewtonsoftSerializer(
                new JsonSerializerSettings()
                {
                    Formatting = Formatting.Indented
                });
        }

        public void SaveEnvironment(EnvironmentSettings settings)
        {
            var fileContent = m_JsonSerializer.SerializeObject(settings);

            m_FileSystem.SaveFile(k_EnvironmentSettingsPath, fileContent);
            m_CachedEnvironmentSettings = new EnvironmentSettings(settings);
            m_IsDirty = false;
        }

        public EnvironmentSettings LoadEnvironment()
        {
            if (m_IsDirty)
            {
                var fileContent = m_FileSystem.GetOrCreateFileContent(k_EnvironmentSettingsPath);
                m_CachedEnvironmentSettings = m_JsonSerializer.DeserializeObject<EnvironmentSettings>(fileContent) ?? new EnvironmentSettings();
                m_IsDirty = false;
            }


            // let's sanitize this in case we are in the upgrade path where an old settings.json
            // is being accessed and has only the environment name field:
            if (!string.IsNullOrEmpty(m_CachedEnvironmentSettings.EnvironmentName) && m_CachedEnvironmentSettings.EnvironmentId == Guid.Empty)
            {
                // here we have a problem: if we want to return the environment id associated to this
                // environment name, we need to perform a query which takes time and shouldn't be done synchronously in a getter.
                // the lesser evil is to reset the current config and pretend the environment is not setup.
                m_CachedEnvironmentSettings = new EnvironmentSettings();
                SaveEnvironment(m_CachedEnvironmentSettings);
                return m_CachedEnvironmentSettings;
            }

            return m_CachedEnvironmentSettings;
        }
    }
}
