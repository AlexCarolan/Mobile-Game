using System;
using System.Collections.Generic;
using System.Text;
using Unity.Services.Core.Editor.OrganizationHandler;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.Services.Analytics.Editor
{
    internal interface IDebugPanelWindow
    {
        void SetStatusIndicator(DebugState state);
        void SetNextUpload(float remainingSeconds);
        void ClearNextUpload();
        void ClearUploadFields();
        void RefreshEventStreamDisplay();
    }

    class DebugPanelWindow : EditorWindow, IDebugPanelWindow
    {
        DebugPanelController m_Model;
        TextField m_UserIdLabel;
        TextField m_InstallationIdLabel;
        TextField m_ExternalUserIdLabel;
        TextField m_PlayerIdLabel;
        VisualElement m_StatusIndicatorIcon;
        TextElement m_StatusIndicatorText;
        TextElement m_NextUploadIndicator;
        Button m_ForceUploadButton;

        ListView m_EventStreamContainer;
        VisualElement m_EventStreamEmptyContainer;
        Label m_EventStreamEmptyLabel;

        TextField m_PayloadDisplay;
        Label m_NoPayloadSelectedLabel;
        string m_PayloadString;
        ScrollView m_PayloadScrollView;
        Button m_ClearStreamButton;
        VisualElement m_PrivacyLinkContainer;

        [MenuItem("Services/Analytics/Debug Panel")]
        static void OpenDebugPanel()
        {
            DebugPanelWindow wnd = GetWindow<DebugPanelWindow>();
            wnd.titleContent = new GUIContent("Analytics Debug Panel");
            wnd.minSize = new Vector2(310.0f, 680.0f);
        }

        void CreateGUI()
        {
            VisualTreeAsset uiAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Packages/com.unity.services.analytics/Editor/Tools/DebugPanel.uxml");
            VisualElement ui = uiAsset.Instantiate();
            ui.AddToClassList("main-window");
            rootVisualElement.Add(ui);

            if (EditorGUIUtility.isProSkin)
            {
                rootVisualElement.styleSheets.Add(AssetDatabase.LoadAssetAtPath<StyleSheet>("Packages/com.unity.services.analytics/Editor/Tools/DebugPanelStylesDark.uss"));
            }
            else
            {
                rootVisualElement.styleSheets.Add(AssetDatabase.LoadAssetAtPath<StyleSheet>("Packages/com.unity.services.analytics/Editor/Tools/DebugPanelStylesLight.uss"));
            }

            m_Model = new DebugPanelController(this);

            TextElement managerLink = rootVisualElement.Q<TextElement>("main-help-link-manager-text");
            managerLink.AddManipulator(new Clickable(OpenDashboardLink));
            VisualElement managerLinkIcon = rootVisualElement.Q<TextElement>("main-help-link-manager-icon");
            managerLinkIcon.AddManipulator(new Clickable(OpenDashboardLink));

            TextElement browserLink = rootVisualElement.Q<TextElement>("main-help-link-browser-text");
            browserLink.AddManipulator(new Clickable(OpenBrowserLink));
            VisualElement browserLinkIcon = rootVisualElement.Q<TextElement>("main-help-link-browser-icon");
            browserLinkIcon.AddManipulator(new Clickable(OpenBrowserLink));

            m_StatusIndicatorIcon = rootVisualElement.Q<VisualElement>("state-indicator-icon");
            m_StatusIndicatorText = rootVisualElement.Q<TextElement>("state-indicator-text");

            m_UserIdLabel = rootVisualElement.Q<TextField>("ids-user-id");
            m_InstallationIdLabel = rootVisualElement.Q<TextField>("ids-installation-id");
            m_ExternalUserIdLabel = rootVisualElement.Q<TextField>("ids-external-id");
            m_PlayerIdLabel = rootVisualElement.Q<TextField>("ids-player-id");

            m_NextUploadIndicator = rootVisualElement.Q<TextElement>("next-upload-indicator");

            m_ForceUploadButton = rootVisualElement.Q<Button>("next-upload-force-button");
            // NOTE: this one needs to be a lambda because .Instance may not exist when the window is first opened.
            m_ForceUploadButton.clicked += () => AnalyticsService.Instance.Flush();

            m_ClearStreamButton = rootVisualElement.Q<Button>("event-stream-clear-button");
            m_ClearStreamButton.clicked += m_Model.ClearStream;

            m_EventStreamContainer = rootVisualElement.Q<ListView>("event-stream-list");
            m_EventStreamContainer.makeItem = MakeEventStreamListViewItem;
            m_EventStreamContainer.bindItem = BindEventStreamListViewItem;
            m_EventStreamContainer.onSelectionChange += SelectEventStreamItem;
            m_EventStreamContainer.itemsSource = m_Model.EventStream;

            m_EventStreamEmptyContainer = rootVisualElement.Q<VisualElement>("event-stream-empty");
            m_EventStreamEmptyLabel = rootVisualElement.Q<Label>("event-stream-empty-main-text");

            TextElement privacyLink = rootVisualElement.Q<TextElement>("event-stream-empty-privacy-link-text");
            privacyLink.AddManipulator(new Clickable(OpenPrivacyLink));
            VisualElement privacyLinkIcon = rootVisualElement.Q<VisualElement>("event-stream-empty-privacy-link-icon");
            privacyLinkIcon.AddManipulator(new Clickable(OpenPrivacyLink));
            m_PrivacyLinkContainer = rootVisualElement.Q<VisualElement>("event-stream-empty-privacy-link");

            m_PayloadDisplay = rootVisualElement.Q<TextField>("payload-display");
            m_PayloadScrollView = rootVisualElement.Q<ScrollView>("payload-scrollview");
            m_NoPayloadSelectedLabel = rootVisualElement.Q<Label>("payload-empty-label");

            Button copyToClipboard = rootVisualElement.Q<Button>("payload-copy-button");
            copyToClipboard.clicked += CopyToClipboard;

            AnalyticsService.SubscribeDebugEvents(EventRecorded, EventsUploading, FlushStarted, FlushCompleted);
            EditorApplication.update += UpdateLoop;

            m_Model.Initialize();
        }

        void OpenDashboardLink()
        {
            Application.OpenURL("https://dashboard.unity3d.com/organizations/" +
                OrganizationProvider.Organization.Key +
                "/projects/" +
                CloudProjectSettings.projectId +
                "/analytics/v2/events");
        }

        void OpenBrowserLink()
        {
            Application.OpenURL("https://dashboard.unity3d.com/organizations/" +
                OrganizationProvider.Organization.Key +
                "/projects/" +
                CloudProjectSettings.projectId +
                "/analytics/v2/eventBrowser");
        }

        void OpenPrivacyLink()
        {
            Application.OpenURL("https://docs.unity.com/ugs/en-us/manual/analytics/manual/privacy-overview");
        }

        void CopyToClipboard()
        {
            GUIUtility.systemCopyBuffer = m_PayloadString;
        }

        void EventRecorded(string eventId, string eventName, DateTime eventTimestamp, byte[] payload)
        {
            m_Model.AddEventToStream(eventId, eventName, eventTimestamp, payload);
        }

        VisualElement MakeEventStreamListViewItem()
        {
            VisualElement container = new VisualElement();
            container.AddToClassList("event-stream-item");
            Label timestamp = new Label();
            timestamp.name = "TimestampLabel";
            timestamp.AddToClassList("event-stream-item-timestamp");
            Label eventName = new Label();
            eventName.name = "EventNameLabel";
            eventName.AddToClassList("event-stream-item-name");
            Image uploadedIcon = new Image();
            uploadedIcon.name = "IconImage";
            uploadedIcon.AddToClassList("event-stream-item-icon");

            container.Add(timestamp);
            container.Add(eventName);
            container.Add(uploadedIcon);

            return container;
        }

        void BindEventStreamListViewItem(VisualElement e, int index)
        {
            EventStreamItem item = (EventStreamItem)m_Model.EventStream[index];
            Label timestampLabel = (Label)e.Q("TimestampLabel");
            timestampLabel.text = item.TimestampString;
            Label eventNameLabel = (Label)e.Q("EventNameLabel");
            eventNameLabel.text = item.Name;
            Image iconImage = (Image)e.Q("IconImage");

            switch (item.Status)
            {
                case EventStreamItemStatus.Uploading:
                    iconImage.AddToClassList("event-stream-item-icon-uploading");
                    break;
                case EventStreamItemStatus.Uploaded:
                    iconImage.AddToClassList("event-stream-item-icon-uploaded");
                    iconImage.RemoveFromClassList("event-stream-item-icon-uploading");
                    break;
                case EventStreamItemStatus.Discarded:
                    iconImage.AddToClassList("event-stream-item-icon-discarded");
                    iconImage.RemoveFromClassList("event-stream-item-icon-uploading");
                    break;
                case EventStreamItemStatus.Queued:
                default:
                    iconImage.RemoveFromClassList("event-stream-item-icon-discarded");
                    iconImage.RemoveFromClassList("event-stream-item-icon-uploaded");
                    iconImage.RemoveFromClassList("event-stream-item-icon-uploading");
                    break;
            }
        }

        void EventsUploading(HashSet<string> eventIds)
        {
            m_Model.MarkEventsBatchAsUploading(eventIds);
        }

        void FlushStarted(byte[] payload)
        {
            m_Model.AddEventToStream("UploadStarted",
                "- Upload Started...",
                DateTime.Now,
                payload);
        }

        void FlushCompleted(int statusCode, bool success, bool badRequest, bool intermittentError, bool networkError, byte[] payload)
        {
            if (networkError || intermittentError)
            {
                m_Model.MarkCurrentEventsBatchAsUploadingFailed();
            }
            else if (badRequest)
            {
                m_Model.MarkCurrentEventsBatchAsDiscarded();
            }
            else
            {
                m_Model.MarkCurrentEventsBatchAsUploaded();
            }

            m_Model.AddEventToStream("UploadCompleted",
                networkError ? "- Upload Failed (No Internet)" : $"- Upload Finished ({statusCode})",
                DateTime.Now,
                payload);
        }

        void SelectEventStreamItem(IEnumerable<object> selection)
        {
            if (m_EventStreamContainer.selectedIndex >= 0)
            {
                // We have set selection to single item, so we can ignore the selection collection here.
                string payloadText = Encoding.UTF8.GetString(((EventStreamItem)m_Model.EventStream[m_EventStreamContainer.selectedIndex]).Payload);
                m_PayloadString = PrettyPrint(payloadText);
                // NOTE: UIToolkit text boxes have a subtle length limit beyond which they will throw a warning and fail to render properly.
                // So we have to truncate the output for display, while still allowing the full payload to be copied to clipboard.
                m_PayloadDisplay.value = m_PayloadString.Length > 10000 ? m_PayloadString.Substring(0, 10000) + "..." : m_PayloadString;
                m_PayloadScrollView.style.display = DisplayStyle.Flex;
                m_NoPayloadSelectedLabel.style.display = DisplayStyle.None;
            }
        }

        public void RefreshEventStreamDisplay()
        {
            m_EventStreamContainer.RefreshItems();
            m_EventStreamContainer.ScrollToItem(-1); // Scroll to bottom to keep track of latest events.

            if (m_EventStreamContainer.selectedItem == null)
            {
                m_PayloadScrollView.style.display = DisplayStyle.None;
                m_NoPayloadSelectedLabel.style.display = DisplayStyle.Flex;
            }

            if (m_Model.EventStream.Count > 0)
            {
                m_ClearStreamButton.SetEnabled(true);
                m_EventStreamEmptyContainer.style.display = DisplayStyle.None;
                m_EventStreamContainer.style.display = DisplayStyle.Flex;
            }
            else
            {
                m_ClearStreamButton.SetEnabled(false);
                m_EventStreamEmptyContainer.style.display = DisplayStyle.Flex;
                m_EventStreamContainer.style.display = DisplayStyle.None;
            }
        }

        void OnDisable()
        {
            AnalyticsService.UnsubscribeDebugEvents();
            if (m_Model != null)
            {
                m_Model.ClearStream();
            }
            EditorApplication.update -= UpdateLoop;
        }

        public void SetStatusIndicator(DebugState state)
        {
            switch (state)
            {
                case DebugState.SdkUninitialized:
                    m_StatusIndicatorIcon.RemoveFromClassList("sdk-status-icon-active");
                    m_StatusIndicatorIcon.RemoveFromClassList("sdk-status-icon-inactive");
                    m_StatusIndicatorIcon.AddToClassList("sdk-status-icon-uninitialised");
                    m_StatusIndicatorText.text = "Uninitialized";
                    m_StatusIndicatorText.tooltip = "The SDK is not in memory. Events cannot be recorded.";
                    m_EventStreamEmptyLabel.text = "Use <b>UnityServices.InitializeAsync()</b> to initialize the Analytics SDK.";
                    m_PrivacyLinkContainer.style.display = DisplayStyle.None;
                    break;
                case DebugState.EditMode:
                    m_StatusIndicatorIcon.RemoveFromClassList("sdk-status-icon-active");
                    m_StatusIndicatorIcon.RemoveFromClassList("sdk-status-icon-inactive");
                    m_StatusIndicatorIcon.AddToClassList("sdk-status-icon-uninitialised");
                    m_StatusIndicatorText.text = "Edit Mode";
                    m_StatusIndicatorText.tooltip = "The SDK is not in memory. Events cannot be recorded.";
                    m_EventStreamEmptyLabel.text = "Events are not recorded while in Edit Mode";
                    m_PrivacyLinkContainer.style.display = DisplayStyle.None;
                    break;
                case DebugState.SdkInactive:
                    m_StatusIndicatorIcon.RemoveFromClassList("sdk-status-icon-active");
                    m_StatusIndicatorIcon.AddToClassList("sdk-status-icon-inactive");
                    m_StatusIndicatorIcon.RemoveFromClassList("sdk-status-icon-uninitialised");
                    m_StatusIndicatorText.text = "Data Collection Inactive";
                    m_StatusIndicatorText.tooltip = "The SDK is in memory but is currently disabled." +
                        "Incoming events will be ignored and discarded immediately.";
                    m_EventStreamEmptyLabel.text = "You must get consent from the player to collect their data. " +
                        "Once you confirm you have player consent, call <b>AnalyticsService.Instance.StartDataCollection()</b> to enable data collection.\n\n" +
                        "As the game developer, you are responsible for the privacy and consent of your players. " +
                        "Data won't be collected unless you inform the SDK that a player has consented. " +
                        "See the privacy page:";
                    m_PrivacyLinkContainer.style.display = DisplayStyle.Flex;
                    break;
                case DebugState.SdkActive:
                    m_StatusIndicatorIcon.AddToClassList("sdk-status-icon-active");
                    m_StatusIndicatorIcon.RemoveFromClassList("sdk-status-icon-inactive");
                    m_StatusIndicatorIcon.RemoveFromClassList("sdk-status-icon-uninitialised");
                    m_StatusIndicatorText.text = "Data Collection Active";
                    m_StatusIndicatorText.tooltip =
                        "The SDK is in memory and is collecting events." +
                        "Incoming events will be batched and uploaded on a regular cadence.";
                    m_EventStreamEmptyLabel.text = "";
                    m_PrivacyLinkContainer.style.display = DisplayStyle.None;
                    break;
                case DebugState.SdkUploading:
                    m_StatusIndicatorIcon.AddToClassList("sdk-status-icon-active");
                    m_StatusIndicatorIcon.RemoveFromClassList("sdk-status-icon-inactive");
                    m_StatusIndicatorIcon.RemoveFromClassList("sdk-status-icon-uninitialised");
                    m_StatusIndicatorText.text = "Uploading";
                    m_StatusIndicatorText.tooltip =
                        "The SDK is in memory and is collecting events." +
                        "An upload of the latest batch of events is in progress.";
                    m_EventStreamEmptyLabel.text = "Events are being uploaded...";
                    m_PrivacyLinkContainer.style.display = DisplayStyle.None;
                    break;
                default:
                    break;
            }
        }

        public void ClearNextUpload()
        {
            m_NextUploadIndicator.text = "-";
        }

        public void SetNextUpload(float remainingSeconds)
        {
            m_NextUploadIndicator.text = $"{remainingSeconds.ToString("00")}s";
        }

        public void ClearUploadFields()
        {
            m_NextUploadIndicator.text = "-";
        }

        void UpdateLoop()
        {
            if (AnalyticsService.IsInitialized)
            {
                m_UserIdLabel.value = AnalyticsService.Instance.GetAnalyticsUserID();
                m_InstallationIdLabel.value = AnalyticsService.ServiceDebug.UserIdentity.InstallId;
                m_PlayerIdLabel.value = AnalyticsService.ServiceDebug.UserIdentity.PlayerId;
                m_ExternalUserIdLabel.value = AnalyticsService.ServiceDebug.UserIdentity.ExternalId;
            }
            else
            {
                m_UserIdLabel.value = String.Empty;
                m_InstallationIdLabel.value = String.Empty;
                m_PlayerIdLabel.value = String.Empty;
                m_ExternalUserIdLabel.value = String.Empty;
            }

            bool needsRepaint = m_Model.Update(
                Application.isPlaying,
                AnalyticsService.IsInitialized,
                AnalyticsService.ServiceDebug,
                AnalyticsService.DispatcherDebug,
                AnalyticsContainer.ContainerDebug);

            m_ForceUploadButton.SetEnabled(AnalyticsService.IsInitialized);

            if (needsRepaint)
            {
                Repaint();
            }
        }

        internal static string PrettyPrint(string minifiedJson)
        {
            StringBuilder prettyPrintBuffer = new StringBuilder(minifiedJson.Length * 2);

            int indent = 0;
            bool stringOpen = false;
            for (int i = 0; i < minifiedJson.Length; i++)
            {
                switch (minifiedJson[i])
                {
                    case '"':
                        prettyPrintBuffer.Append(minifiedJson[i]);
                        if (minifiedJson[i - 1] != '\\')
                        {
                            stringOpen = !stringOpen;
                        }
                        break;
                    case ':':
                        prettyPrintBuffer.Append(": ");
                        break;
                    case '{':
                    case '[':
                        prettyPrintBuffer.Append(minifiedJson[i]);
                        if (!stringOpen)
                        {
                            prettyPrintBuffer.AppendLine();
                            indent++;
                            prettyPrintBuffer.Append('\t', indent);
                        }
                        break;
                    case ',':
                        prettyPrintBuffer.Append(minifiedJson[i]);
                        if (!stringOpen)
                        {
                            prettyPrintBuffer.AppendLine();
                            prettyPrintBuffer.Append('\t', indent);
                        }
                        break;
                    case '}':
                    case ']':
                        if (!stringOpen)
                        {
                            indent--;
                            prettyPrintBuffer.AppendLine();
                            prettyPrintBuffer.Append('\t', indent);
                        }
                        prettyPrintBuffer.Append(minifiedJson[i]);
                        break;
                    default:
                        prettyPrintBuffer.Append(minifiedJson[i]);
                        break;
                }
            }

            return prettyPrintBuffer.ToString();
        }
    }
}
