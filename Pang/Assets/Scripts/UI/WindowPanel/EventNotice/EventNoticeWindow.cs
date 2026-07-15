using System;
using UnityEngine;
using UnityEngine.UIElements;
using ToolkitWindow = UniverseLogistics.UI.Toolkit.UIWindow;

namespace Assets.Scripts.UI
{
	public sealed class EventNoticeWindow : MonoBehaviour
	{
		private const string ContentRootName = "event-notice-content";
		private const string IconName = "event-notice-icon";
		private const string MessageName = "event-notice-message";
		private const string ConfirmButtonName = "event-notice-confirm-button";
		private const string ExtraActionButtonName = "event-notice-extra-action-button";

		private ToolkitWindow window;
		private VisualTreeAsset contentTemplate;
		private string idleWindowTitle;
		private Sprite defaultIcon;
		private Vector2 defaultWindowSize;
		private VisualElement contentRoot;
		private VisualElement contentIcon;
		private Label messageLabel;
		private Button confirmButton;
		private Button extraActionButton;
		private EventNoticeRequest currentRequest;
		private bool initialized;
		private bool pauseHeld;
		private Action<EventNoticeWindow> dismissedCallback;

		public event Action<EventNoticeWindow> Dismissed;

		private GameTime GameTime => GameContext.HasInstance ? GameContext.Instance.GameTime : null;

		public void Configure(
			ToolkitWindow targetWindow,
			VisualTreeAsset targetContentTemplate,
			string targetIdleWindowTitle,
			Sprite targetDefaultIcon,
			Vector2 targetDefaultWindowSize)
		{
			window = targetWindow;
			contentTemplate = targetContentTemplate;
			idleWindowTitle = string.IsNullOrWhiteSpace(targetIdleWindowTitle) ? "Event Notice" : targetIdleWindowTitle;
			defaultIcon = targetDefaultIcon;
			defaultWindowSize = targetDefaultWindowSize;
		}

		private void OnDisable()
		{
			UnbindControls();
			initialized = false;
		}

		private void OnDestroy()
		{
			if (pauseHeld)
			{
				GameTime?.ResumePreservedSpeed();
				pauseHeld = false;
			}
		}

		public void Enqueue(EventNoticeRequest request)
		{
			Show(request, Vector2.zero);
		}

		public void Notify(string title, string message, Sprite icon = null, EventNoticeAction extraAction = null)
		{
			Show(new EventNoticeRequest(title, message, icon, extraAction), Vector2.zero);
		}

		public void Show(EventNoticeRequest request, Vector2 windowOffset, Action<EventNoticeWindow> onDismissed = null)
		{
			if (request == null)
				return;

			if (gameObject.activeSelf == false)
				gameObject.SetActive(true);

			if (EnsureInitialized() == false)
				return;

			currentRequest = request;
			dismissedCallback = onDismissed;
			ApplyRequest(request);
			HoldPause();
			window.Open();
			PositionWindow(windowOffset);
		}

		public void ShowPosition(Vector2 windowOffset)
		{
			if (initialized)
				PositionWindow(windowOffset);
		}

		private bool EnsureInitialized()
		{
			if (initialized)
				return true;

			if (window == null || contentTemplate == null || window.Initialize() == false)
			{
				Debug.LogError("[EventNoticeWindow] Toolkit window or content template is missing.", this);
				return false;
			}

			window.SetOpenOnEnable(false);
			window.SetDefaultSize(defaultWindowSize);
			contentRoot = contentTemplate.CloneTree();
			contentIcon = contentRoot.Q<VisualElement>(IconName);
			messageLabel = contentRoot.Q<Label>(MessageName);
			confirmButton = contentRoot.Q<Button>(ConfirmButtonName);
			extraActionButton = contentRoot.Q<Button>(ExtraActionButtonName);

			if (contentRoot.Q<VisualElement>(ContentRootName) == null || contentIcon == null || messageLabel == null ||
				confirmButton == null || extraActionButton == null)
			{
				Debug.LogError("[EventNoticeWindow] Required UXML elements are missing.", this);
				return false;
			}

			window.SetContent(contentRoot);
			window.Closed -= HandleWindowClosed;
			window.Closed += HandleWindowClosed;
			confirmButton.clicked += HandleConfirmClicked;
			extraActionButton.clicked += HandleExtraActionClicked;
			ApplyIdleState();
			window.Close();
			initialized = true;
			return true;
		}

		private void UnbindControls()
		{
			if (window != null)
				window.Closed -= HandleWindowClosed;
			if (confirmButton != null)
				confirmButton.clicked -= HandleConfirmClicked;
			if (extraActionButton != null)
				extraActionButton.clicked -= HandleExtraActionClicked;
		}

		private void HandleConfirmClicked()
		{
			window?.Close();
		}

		private void HandleExtraActionClicked()
		{
			currentRequest?.ExtraAction?.Invoke();
		}

		private void HandleWindowClosed()
		{
			if (currentRequest == null)
				return;

			currentRequest = null;
			ReleasePause();
			ApplyIdleState();
			dismissedCallback?.Invoke(this);
			Dismissed?.Invoke(this);
			dismissedCallback = null;
		}

		private void ApplyRequest(EventNoticeRequest request)
		{
			Sprite icon = request.Icon != null ? request.Icon : defaultIcon;
			window.SetTitle(request.Title);
			window.SetIcon(icon);
			messageLabel.text = request.Message;
			ApplyContentIcon(icon);

			bool hasExtraAction = request.ExtraAction != null;
			extraActionButton.style.display = hasExtraAction ? DisplayStyle.Flex : DisplayStyle.None;
			if (hasExtraAction)
				extraActionButton.text = request.ExtraAction.Label;
		}

		private void ApplyIdleState()
		{
			window.SetTitle(idleWindowTitle);
			window.SetIcon(defaultIcon);
			messageLabel.text = string.Empty;
			ApplyContentIcon(defaultIcon);
			confirmButton.text = "Confirm";
			extraActionButton.style.display = DisplayStyle.None;
		}

		private void ApplyContentIcon(Sprite icon)
		{
			contentIcon.style.backgroundImage = icon != null ? new StyleBackground(icon) : StyleKeyword.None;
			contentIcon.style.display = icon != null ? DisplayStyle.Flex : DisplayStyle.None;
		}

		private void PositionWindow(Vector2 offset)
		{
			VisualElement documentRoot = GetComponent<UIDocument>().rootVisualElement;
			VisualElement windowRoot = documentRoot.Q<VisualElement>("ui-window");
			windowRoot?.schedule.Execute(() =>
			{
				float panelWidth = documentRoot.resolvedStyle.width;
				float panelHeight = documentRoot.resolvedStyle.height;
				float width = windowRoot.resolvedStyle.width;
				float height = windowRoot.resolvedStyle.height;
				windowRoot.style.translate = new Translate(0f, 0f);
				windowRoot.style.left = Mathf.Max(0f, (panelWidth - width) * 0.5f + offset.x);
				windowRoot.style.top = Mathf.Max(0f, (panelHeight - height) * 0.5f + offset.y);
			});
		}

		private void HoldPause()
		{
			if (pauseHeld || GameTime == null)
				return;

			GameTime.PausePreservingSpeed();
			pauseHeld = true;
		}

		private void ReleasePause()
		{
			if (pauseHeld == false || GameTime == null)
				return;

			GameTime.ResumePreservedSpeed();
			pauseHeld = false;
		}
	}
}
