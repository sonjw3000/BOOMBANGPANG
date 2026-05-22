using System.Collections.Generic;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.Scripts.UI
{
	public sealed class EventNoticeWindow : MonoBehaviour
	{
		[SerializeField] private UIWindow window;
		[SerializeField] private string idleWindowTitle = "Event Notice";
		[SerializeField] private Sprite defaultIcon;
		[SerializeField] private Vector2 defaultWindowSize = new(500f, 800f);

		private RectTransform contentRoot;
		private Image contentIconImage;
		private TMP_Text messageText;
		private Button confirmButton;
		private TMP_Text confirmButtonText;
		private Button extraActionButton;
		private TMP_Text extraActionButtonText;
		private EventNoticeRequest currentRequest;
		private bool initialized;
		private bool pauseHeld;
		private Action<EventNoticeWindow> dismissedCallback;

		public event Action<EventNoticeWindow> Dismissed;
		private GameTime GameTime => GameContext.HasInstance ? GameContext.Instance.GameTime : null;

		private void Awake()
		{
			EnsureInitialized();
		}

		private void OnEnable()
		{
			EnsureInitialized();
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

		public void Show(EventNoticeRequest request, Vector2 anchoredPosition, Action<EventNoticeWindow> onDismissed = null)
		{
			if (request == null)
				return;

			EnsureInitialized();
			currentRequest = request;
			dismissedCallback = onDismissed;
			ApplyRequest(request);
			HoldPause();

			if (gameObject.activeSelf == false)
				gameObject.SetActive(true);

			RectTransform rect = GetComponent<RectTransform>();
			if (rect != null)
				rect.anchoredPosition = anchoredPosition;

			transform.SetAsLastSibling();
			window.Open();
		}

		private void EnsureInitialized()
		{
			if (initialized)
				return;

			window ??= GetComponent<UIWindow>();
			window ??= GetComponentInChildren<UIWindow>(true);
			if (window == null)
			{
				Debug.LogWarning("[EventNoticeWindow] UIWindow reference is missing.");
				return;
			}

			if (window.RootRect != null && defaultWindowSize.x > 0f && defaultWindowSize.y > 0f)
				window.RootRect.sizeDelta = defaultWindowSize;

			window.SetTitle(idleWindowTitle);
			window.SetIcon(defaultIcon);
			BuildContent();
			ApplyIdleState();
			window.Closed -= HandleWindowClosed;
			window.Closed += HandleWindowClosed;
			window.Close();
			initialized = true;
		}

		private void BuildContent()
		{
			contentRoot = window.ContentRoot;
			if (contentRoot == null)
				return;

			ClearChildren(contentRoot);

			GameObject container = CreateVerticalContainer("EventNoticeContent", contentRoot, 20f, TextAnchor.UpperCenter);
			LayoutElement containerLayout = container.AddComponent<LayoutElement>();
			containerLayout.flexibleHeight = 1f;

			GameObject iconObject = new("Icon", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
			iconObject.transform.SetParent(container.transform, false);
			LayoutElement iconLayout = iconObject.GetComponent<LayoutElement>();
			iconLayout.preferredWidth = 160f;
			iconLayout.preferredHeight = 160f;
			contentIconImage = iconObject.GetComponent<Image>();
			contentIconImage.preserveAspect = true;
			contentIconImage.raycastTarget = false;

			messageText = CreateText("Message", container.transform, string.Empty, TextAlignmentOptions.Center, 28f);
			messageText.textWrappingMode = TextWrappingModes.Normal;
			messageText.overflowMode = TextOverflowModes.Overflow;
			messageText.color = Color.white;
			LayoutElement messageLayout = messageText.gameObject.GetComponent<LayoutElement>();
			messageLayout.preferredHeight = 220f;
			messageLayout.flexibleHeight = 1f;

			GameObject footerSpacer = new("FooterSpacer", typeof(RectTransform), typeof(LayoutElement));
			footerSpacer.transform.SetParent(container.transform, false);
			footerSpacer.GetComponent<LayoutElement>().flexibleHeight = 1f;

			GameObject footer = CreateHorizontalContainer("Footer", container.transform, 12f);
			LayoutElement footerLayout = footer.AddComponent<LayoutElement>();
			footerLayout.minHeight = 56f;

			confirmButton = CreateButton("ConfirmButton", footer.transform, "Confirm", out confirmButtonText);
			confirmButton.onClick.RemoveListener(HandleConfirmClicked);
			confirmButton.onClick.AddListener(HandleConfirmClicked);

			GameObject flexibleSpace = new("FlexibleSpace", typeof(RectTransform), typeof(LayoutElement));
			flexibleSpace.transform.SetParent(footer.transform, false);
			flexibleSpace.GetComponent<LayoutElement>().flexibleWidth = 1f;

			extraActionButton = CreateButton("ExtraActionButton", footer.transform, "Action", out extraActionButtonText);
			extraActionButton.onClick.RemoveListener(HandleExtraActionClicked);
			extraActionButton.onClick.AddListener(HandleExtraActionClicked);
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
			gameObject.SetActive(false);
			dismissedCallback?.Invoke(this);
			Dismissed?.Invoke(this);
			dismissedCallback = null;
		}

		private void ApplyRequest(EventNoticeRequest request)
		{
			if (request == null)
				return;

			window.SetTitle(request.Title);
			window.SetIcon(request.Icon != null ? request.Icon : defaultIcon);

			if (messageText != null)
				messageText.text = request.Message;

			if (contentIconImage != null)
			{
				Sprite icon = request.Icon != null ? request.Icon : defaultIcon;
				contentIconImage.sprite = icon;
				contentIconImage.enabled = icon != null;
			}

			if (confirmButtonText != null)
				confirmButtonText.text = "Confirm";

			bool hasExtraAction = request.ExtraAction != null;
			if (extraActionButton != null)
				extraActionButton.gameObject.SetActive(hasExtraAction);
			if (extraActionButtonText != null && hasExtraAction)
				extraActionButtonText.text = request.ExtraAction.Label;
		}

		private void ApplyIdleState()
		{
			window.SetTitle(idleWindowTitle);
			window.SetIcon(defaultIcon);

			if (messageText != null)
				messageText.text = string.Empty;

			if (contentIconImage != null)
			{
				contentIconImage.sprite = defaultIcon;
				contentIconImage.enabled = defaultIcon != null;
			}

			if (extraActionButton != null)
				extraActionButton.gameObject.SetActive(false);
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

		private static void ClearChildren(Transform parent)
		{
			for (int i = parent.childCount - 1; i >= 0; --i)
			{
				Destroy(parent.GetChild(i).gameObject);
			}
		}

		private static GameObject CreateVerticalContainer(string objectName, Transform parent, float spacing, TextAnchor alignment)
		{
			GameObject container = new(objectName, typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
			container.transform.SetParent(parent, false);

			VerticalLayoutGroup layout = container.GetComponent<VerticalLayoutGroup>();
			layout.spacing = spacing;
			layout.padding = new RectOffset(24, 24, 24, 24);
			layout.childAlignment = alignment;
			layout.childControlWidth = true;
			layout.childControlHeight = true;
			layout.childForceExpandWidth = true;
			layout.childForceExpandHeight = false;

			ContentSizeFitter fitter = container.GetComponent<ContentSizeFitter>();
			fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
			fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

			return container;
		}

		private static GameObject CreateHorizontalContainer(string objectName, Transform parent, float spacing)
		{
			GameObject container = new(objectName, typeof(RectTransform), typeof(HorizontalLayoutGroup));
			container.transform.SetParent(parent, false);

			HorizontalLayoutGroup layout = container.GetComponent<HorizontalLayoutGroup>();
			layout.spacing = spacing;
			layout.childAlignment = TextAnchor.MiddleLeft;
			layout.childControlWidth = true;
			layout.childControlHeight = true;
			layout.childForceExpandWidth = false;
			layout.childForceExpandHeight = false;

			return container;
		}

		private static TMP_Text CreateText(string objectName, Transform parent, string value, TextAlignmentOptions alignment, float fontSize)
		{
			GameObject textObject = new(objectName, typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
			textObject.transform.SetParent(parent, false);

			TMP_Text text = textObject.GetComponent<TextMeshProUGUI>();
			text.text = value;
			text.alignment = alignment;
			text.fontSize = fontSize;
			text.color = Color.white;

			LayoutElement layout = textObject.GetComponent<LayoutElement>();
			layout.minHeight = 32f;

			return text;
		}

		private static Button CreateButton(string objectName, Transform parent, string label, out TMP_Text labelText)
		{
			GameObject buttonObject = new(objectName, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
			buttonObject.transform.SetParent(parent, false);

			Image image = buttonObject.GetComponent<Image>();
			image.color = new Color(0.22f, 0.22f, 0.22f, 0.96f);

			LayoutElement layout = buttonObject.GetComponent<LayoutElement>();
			layout.preferredWidth = 180f;
			layout.preferredHeight = 44f;

			Button button = buttonObject.GetComponent<Button>();
			ColorBlock colors = button.colors;
			colors.normalColor = image.color;
			colors.highlightedColor = new Color(0.28f, 0.28f, 0.28f, 1f);
			colors.pressedColor = new Color(0.16f, 0.16f, 0.16f, 1f);
			colors.selectedColor = colors.highlightedColor;
			button.colors = colors;

			labelText = CreateText("Label", buttonObject.transform, label, TextAlignmentOptions.Center, 20f);
			RectTransform labelRect = labelText.rectTransform;
			labelRect.anchorMin = Vector2.zero;
			labelRect.anchorMax = Vector2.one;
			labelRect.offsetMin = Vector2.zero;
			labelRect.offsetMax = Vector2.zero;

			return button;
		}
	}
}
