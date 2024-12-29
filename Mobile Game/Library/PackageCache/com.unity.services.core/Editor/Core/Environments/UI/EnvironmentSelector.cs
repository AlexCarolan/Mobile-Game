using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Unity.Services.Core.Editor.Shared.EditorUtils;
using Unity.Services.Core.Editor.Shared.UI;
using Unity.Services.Core.Environments.Client.Http;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;



namespace Unity.Services.Core.Editor.Environments.UI
{
    class EnvironmentSelector : VisualElement
    {
        const string k_UxmlPath = "Packages/com.unity.services.core/Editor/Core/Environments/UI/Assets/EnvironmentSelectorUI.uxml";
        const string k_UxmlPathNoConnection = "Packages/com.unity.services.core/Editor/Core/UiHelpers/UXML/Offline.uxml";
#if UNITY_2021_3_OR_NEWER
        const string k_UxmlPathDropdown = "Packages/com.unity.services.core/Editor/Core/Environments/UI/Assets/EnvironmentDropdown.uxml";
#endif

        ModelBinding<IEnvironmentService> m_EnvironmentBindings;
        readonly IEnvironmentService m_EnvironmentService;

        TemplateContainer m_RegularUxmlContainer;
        TemplateContainer m_NoConnectionUxmlContainer;

        VisualElement m_ContainerDropdownSection;
        VisualElement m_ContainerFetching;
        VisualElement m_ContainerWarning;

        Button m_RefreshEnvironmentButton;
        Button m_RetryConnectionButton;

#if UNITY_2021_3_OR_NEWER
        DropdownField m_DropdownControl;
#else
        PopupField<string> m_DropdownControl;
        VisualElement m_DropdownControlContainer;
#endif

        public EnvironmentSelector(IEnvironmentService environmentService)
        {
            m_EnvironmentService = environmentService;
            SetupRegularUxml();
            SetupNoConnectionUxml();
            Sync.SafeAsync(RefreshEnvironmentsAsync);
        }

        TemplateContainer AddUxmlToVisualElement(VisualElement containerElement, string uxmlPath)
        {
            var uxmlAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(uxmlPath);
            if (uxmlAsset == null)
            {
                throw new MissingReferenceException("Could not find a uxml asset to load.");
            }

            var asset = uxmlAsset.Instantiate();
            containerElement.Add(asset);
            return asset;
        }

        void SetupRegularUxml()
        {
            m_RegularUxmlContainer = AddUxmlToVisualElement(this, k_UxmlPath);
            SetupDropdown(m_RegularUxmlContainer);
            SetupManageEnvironments(m_RegularUxmlContainer);
            SetupWarning(m_RegularUxmlContainer);
            BindEvents();
        }

        void SetupNoConnectionUxml()
        {
            m_NoConnectionUxmlContainer = AddUxmlToVisualElement(this, k_UxmlPathNoConnection);
            m_NoConnectionUxmlContainer.style.display = DisplayStyle.None;
            SetupRetryConnectionButton(m_NoConnectionUxmlContainer, UxmlNames.RetryConnectionButton);
        }

        async Task RefreshEnvironmentsAsync()
        {
            try
            {
                await m_EnvironmentService.RefreshAsync();
                m_RegularUxmlContainer.style.display = DisplayStyle.Flex;
                m_NoConnectionUxmlContainer.style.display = DisplayStyle.None;
            }
            catch (Exception e)
                when (e is RequestFailedException || e is HttpException)
            {
                m_RegularUxmlContainer.style.display = DisplayStyle.None;
                m_NoConnectionUxmlContainer.style.display = DisplayStyle.Flex;
            }
        }

        void BindEvents()
        {
            m_EnvironmentBindings = new ModelBinding<IEnvironmentService>(this)
            {
                Source = m_EnvironmentService
            };

            m_EnvironmentBindings.BindProperty(nameof(IEnvironmentService.Environments), OnEnvironmentsRefreshed);

            m_EnvironmentBindings.BindProperty(nameof(IEnvironmentService.ActiveEnvironmentId), service =>
            {
                OnEnvironmentChanged(service.ActiveEnvironmentInfo());
            });

            m_DropdownControl.RegisterValueChangedCallback(OnDropdownEnvironmentChanged);
        }

        void OnEnvironmentsRefreshed(IEnvironmentService service)
        {
            if (m_EnvironmentService.Environments == null)
            {
                SetVisibleContainer(m_ContainerFetching);
            }
            else if (m_DropdownControl != null)
            {
                var choices = m_EnvironmentService.Environments.Select(env => env.Name).ToList();

                // Unity Editor 2021.1 has `choices` set as internal
#if UNITY_2021_1_OR_NEWER && !UNITY_2021_2_OR_NEWER
                    m_DropdownControl
                        .GetType()
                        .InvokeMember(
                            "choices",
                            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.SetProperty,
                            null,
                            m_DropdownControl,
                            new object[] { choices });
#else
                m_DropdownControl.choices = choices;
#endif

                var currentEnvInfo = m_EnvironmentService.ActiveEnvironmentInfo();
                if (currentEnvInfo != null)
                {
                    m_DropdownControl.SetValueWithoutNotify(currentEnvInfo.Value.Name);
                }
                else
                {
                    m_DropdownControl.value = string.Empty;
                }

                SetVisibleContainer(m_ContainerDropdownSection);
            }
            else
            {
                throw new Exception("Dropdown field of the Environment Selector has not been set.");
            }

            OnEnvironmentChanged(m_EnvironmentService.ActiveEnvironmentInfo());
        }

        void OnDropdownEnvironmentChanged(ChangeEvent<string> changeEvent)
        {
            var info = m_EnvironmentService.EnvironmentInfoFromName(changeEvent.newValue);
            if (info != null)
            {
                m_EnvironmentService.SetActiveEnvironment(info.Value);
            }
        }

        void SetupRetryConnectionButton(VisualElement containerElement, string buttonName)
        {
            m_RetryConnectionButton = containerElement.Q<Button>(buttonName);

            if (m_RetryConnectionButton == null)
            {
                return;
            }

            m_RetryConnectionButton.clicked += () =>
            {
                Sync.SafeAsync(RefreshEnvironmentsAsync);
            };
        }

        void SetupRefreshEnvironmentListButton(Button button)
        {
            if (button == null)
            {
                return;
            }

            m_RefreshEnvironmentButton = button;

            m_RefreshEnvironmentButton.clicked += () =>
            {
                m_RefreshEnvironmentButton.AddToClassList(UxmlClasses.UnityDisabled);
                Sync.SafeAsync(RefreshEnvironmentsAsync, OnDoneRefreshing);
            };
        }

        void OnDoneRefreshing(Task refreshTask)
        {
            m_RefreshEnvironmentButton?.RemoveFromClassList(UxmlClasses.UnityDisabled);
        }

        void SetupDropdown(VisualElement containerElement)
        {
            m_ContainerDropdownSection = containerElement.Q(UxmlNames.ContainerDropdownSection);
            m_ContainerFetching = containerElement.Q(UxmlNames.ContainerFetching);

            m_ContainerDropdownSection.style.display = DisplayStyle.None;

#if UNITY_2021_3_OR_NEWER
            var uxmlAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(k_UxmlPathDropdown);
            if (uxmlAsset != null)
            {
                uxmlAsset.CloneTree(m_ContainerDropdownSection);
                m_DropdownControl = this.Q<DropdownField>();
                var refreshEnvButton = this.Q<Button>(UxmlNames.ButtonRefreshEnvList);
                SetupRefreshEnvironmentListButton(refreshEnvButton);
            }
            else
            {
                throw new MissingReferenceException("Could not find a uxml asset to load.");
            }
#else
            m_DropdownControlContainer = new VisualElement { name = UxmlNames.ContainerDropdownControl };
            m_DropdownControlContainer.AddToClassList(UxmlClasses.DropdownControlContainer);

            m_DropdownControl = new PopupField<string>
            {
                name = UxmlNames.DropdownControl,
                label = UxmlLabels.EditorEnvironment
            };
            m_DropdownControl.AddToClassList(UxmlClasses.NoMargin);
            m_DropdownControl.AddToClassList(UxmlClasses.DropdownControl);
            m_DropdownControlContainer.Add(m_DropdownControl);

            var newButton = new Button
            {
                name = UxmlNames.ButtonRefreshEnvList,
                text = UxmlLabels.Refresh
            };
            newButton.AddToClassList(UxmlClasses.DropdownRefreshButton);
            m_DropdownControlContainer.Add(newButton);
            SetupRefreshEnvironmentListButton(newButton);

            m_ContainerDropdownSection.Add(m_DropdownControlContainer);
#endif
        }

        static void SetupManageEnvironments(VisualElement containerElement)
        {
            var containerManageEnvironments = containerElement.Q(UxmlNames.ContainerManageEnvironments);
#if ENABLE_EDITOR_GAME_SERVICES
            var clickable = new Clickable(() =>
            {
                Application.OpenURL($"https://dashboard.unity3d.com/organizations/{CloudProjectSettings.organizationKey}/projects/{CloudProjectSettings.projectId}/settings/environments");
            });
            containerManageEnvironments.AddManipulator(clickable);
#else
            containerManageEnvironments.style.display = DisplayStyle.None;
#endif
        }

        void SetupWarning(VisualElement containerElement)
        {
            m_ContainerWarning = containerElement.Q(UxmlNames.ContainerWarning);
            m_ContainerWarning.style.display = DisplayStyle.None;
        }

        void OnEnvironmentChanged(EnvironmentInfo? environmentInfo)
        {
            m_ContainerWarning.style.display = environmentInfo?.IsDefault ?? false
                ? DisplayStyle.Flex
                : DisplayStyle.None;
        }

        void SetVisibleContainer(VisualElement containerElement)
        {
            m_ContainerDropdownSection.style.display =
                containerElement == m_ContainerDropdownSection
                ? DisplayStyle.Flex
                : DisplayStyle.None;
            m_ContainerFetching.style.display =
                containerElement == m_ContainerFetching
                ? DisplayStyle.Flex
                : DisplayStyle.None;
        }

        static class UxmlNames
        {
            public const string ContainerDropdownSection = "Dropdown Section";
            public const string ContainerFetching = "Fetching Environments Section";
            public const string ContainerManageEnvironments = "Manage Environments Container";
            public const string ContainerWarning = "Default Environment Section";
            public const string ButtonRefreshEnvList = "RefreshDropdownButton";
            public const string ContainerDropdownControl = "DropdownContainer";
            public const string DropdownControl = "DropdownControl";
            public const string RetryConnectionButton = "RefreshBtn";
        }

        static class UxmlClasses
        {
            public const string DropdownControlContainer = "dropdown-control-container";
            public const string DropdownRefreshButton = "dropdown-refresh-button";
            public const string NoMargin = "no-margin";
            public const string DropdownControl = "dropdown-control";
            public const string UnityDisabled = "unity-disabled";
        }

        static class UxmlLabels
        {
            public const string Refresh = "Refresh";
            public const string EditorEnvironment = "Editor Environment";
        }
    }
}
