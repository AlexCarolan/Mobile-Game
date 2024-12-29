using System;
using System.ComponentModel;
using Unity.Services.Core.Editor.Settings;

namespace Unity.Services.Core.Editor.Environments
{
    /// <summary>
    /// This interface represents a container for the current selected environment.
    /// </summary>
    interface IEnvironmentProvider : INotifyPropertyChanged
    {
        /// <summary>
        /// The currently selected environment.
        /// </summary>
        EnvironmentSettings ActiveEnvironment { get; set; }
    }
}
