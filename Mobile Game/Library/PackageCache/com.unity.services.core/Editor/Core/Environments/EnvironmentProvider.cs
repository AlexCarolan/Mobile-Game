using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Unity.Services.Core.Editor.Environments.Save;
using Unity.Services.Core.Editor.Settings;
using Unity.Services.Core.Editor.Shared.EditorUtils;
using UnityEditor;

namespace Unity.Services.Core.Editor.Environments
{
    sealed class EnvironmentProvider : IEnvironmentProvider, IDisposable
    {
        readonly IEnvironmentSaveSystem m_EnvironmentSaveSystem;
        readonly EditorValueTracker<string> m_ProjectIdTracker;

        public EnvironmentProvider(IEnvironmentSaveSystem environmentSaveSystem)
        {
            var projectHasBeenSetPreviously = !string.IsNullOrEmpty(CloudProjectSettings.projectId);
            m_ProjectIdTracker = new EditorValueTracker<string>(() => CloudProjectSettings.projectId);
            m_ProjectIdTracker.ValueChanged += (_, __) =>
            {
                if (projectHasBeenSetPreviously)
                {
                    ActiveEnvironment = new EnvironmentSettings();
                }

                projectHasBeenSetPreviously = projectHasBeenSetPreviously || !string.IsNullOrEmpty(CloudProjectSettings.projectId);
            };

            m_EnvironmentSaveSystem = environmentSaveSystem;
        }

        public EnvironmentSettings ActiveEnvironment
        {
            get => m_EnvironmentSaveSystem.LoadEnvironment();
            set
            {
                m_EnvironmentSaveSystem.SaveEnvironment(value);
                OnPropertyChanged();
                OnPropertyChanged(nameof(ActiveEnvironment));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        void OnPropertyChanged([CallerMemberName] string caller = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(caller));
        }

        public void Dispose()
        {
            m_ProjectIdTracker?.Dispose();
        }
    }
}
