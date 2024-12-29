#if UNITY_EDITOR
#if FEATURE_SERVICES_EDITOR_EXPERIMENTAL
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;

namespace Unity.Services.Core.Internal
{
    static class UnityServicesEditorInitializer
    {
        [InitializeOnLoadMethod]
        static void SetupForEditorInitialization()
        {
            UnityServicesBuilder.InstanceCreationDelegate = UnityServicesInitializer.CreateInstance;

            var corePackageRegistry = new CorePackageRegistry();
            var coreRegistry = new CoreRegistry(corePackageRegistry.Registry);

            CorePackageRegistry.Instance = corePackageRegistry;
            CoreRegistry.Instance = coreRegistry;

            var coreDiagnostics = new CoreDiagnostics();
            CoreDiagnostics.Instance = coreDiagnostics;

            RegisterInitializers(corePackageRegistry);
        }

        static void RegisterInitializers(CorePackageRegistry registry)
        {
            try
            {
                var initializers = GetInitializersV2();

                foreach (var initializer in initializers)
                {
                    try
                    {
                        initializer.Register(registry);
                    }
                    catch (Exception e)
                    {
                        CoreLogger.LogError(e.Message);
                    }
                }
            }
            catch (Exception e)
            {
                CoreLogger.LogError(e.Message);
            }
        }

        static IEnumerable<IInitializablePackageV2> GetInitializersV2()
        {
            var packages = TypeCache.GetTypesDerivedFrom<IInitializablePackageV2>();

            return packages.Where(type => !type.IsAbstract && typeof(IInitializablePackageV2).IsAssignableFrom(type))
                .Select(type => (IInitializablePackageV2)Activator.CreateInstance(type));
        }
    }
}
#endif
#endif
