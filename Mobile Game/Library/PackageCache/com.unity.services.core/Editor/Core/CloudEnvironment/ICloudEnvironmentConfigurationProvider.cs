using JetBrains.Annotations;
using Unity.Services.Core.Configuration.Editor;

namespace Unity.Services.Core.Editor
{
    interface ICloudEnvironmentConfigurationProvider : IConfigurationProvider
    {
        bool IsStaging();
    }
}
