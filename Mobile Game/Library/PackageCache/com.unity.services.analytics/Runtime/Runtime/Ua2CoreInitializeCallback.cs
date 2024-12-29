using System.Threading.Tasks;
using Unity.Services.Analytics;
using Unity.Services.Authentication.Internal;
using Unity.Services.Core.Analytics.Internal;
using Unity.Services.Core.Configuration.Internal;
using Unity.Services.Core.Device.Internal;
using Unity.Services.Core.Environments.Internal;
using Unity.Services.Core.Internal;
using UnityEngine;

class Ua2CoreInitializeCallback : IInitializablePackage
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Register()
    {
        CoreRegistry.Instance.RegisterPackage(new Ua2CoreInitializeCallback())
            .DependsOn<IInstallationId>()
            .DependsOn<ICloudProjectId>()
            .DependsOn<IEnvironments>()
            .DependsOn<IExternalUserId>()
            .DependsOn<IProjectConfiguration>()
            .OptionallyDependsOn<IPlayerId>()
            .ProvidesComponent<IAnalyticsStandardEventComponent>();
    }

    public async Task Initialize(CoreRegistry registry)
    {
        AnalyticsService.Initialize(registry);

        await Task.CompletedTask;
    }
}
