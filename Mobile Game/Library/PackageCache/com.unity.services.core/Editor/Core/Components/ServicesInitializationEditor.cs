using UnityEditor;

namespace Unity.Services.Core.Components.Editor
{
    class ServicesInitializationEditor
    {
        [MenuItem("CONTEXT/ServicesInitialization/Open Services Settings")]
        static void OpenServicesSettings(MenuCommand _)
        {
            SettingsService.OpenProjectSettings("Project/Services");
        }

        [MenuItem("CONTEXT/ServicesInitialization/Open Environment Settings")]
        static void OpenEnvironmentSettings(MenuCommand _)
        {
            SettingsService.OpenProjectSettings("Project/Services/Environments");
        }
    }
}
