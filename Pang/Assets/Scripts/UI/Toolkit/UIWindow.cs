using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace UniverseLogistics.UI.Toolkit
{
	[RequireComponent(typeof(UIDocument))]
	public sealed class UIWindow : MonoBehaviour
	{
		private const string RootName = "ui-window";
		private const string TitleBarName = "window-title-bar";
		private const string IconName = "window-icon";
		private const string TitleName = "window-title";
		private const string CloseButtonName = "window-close-button";
		private const string TabBarName = "window-tab-bar";
		private const string ContentRootName = "window-content-root";
		private const string TabButtonClass = "ui-window__tab-button";
		private const string SelectedTabButtonClass = "ui-window__tab-button--selected";

		[SerializeField] private UIDocument uiDocument;
		[SerializeField] private string initialTitle = "Window";
		[SerializeField] private Sprite initialIcon;
		[SerializeField] private bool openOnEnable = true;
		[SerializeField] private bool movable = true;
		[SerializeField] private bool resizable = true;
		[SerializeField] private Vector2 defaultSize = new(990f, 690f);
		[SerializeField] private Vector2 minimumSize = new(420f, 300f);

		private readonly List<TabEntry> tabs = new();
		private readonly List<ResizeHandleBinding> resizeHandleBindings = new();
		private VisualElement windowRoot;
		private VisualElement titleBar;
		private VisualElement iconElement;
		private Label titleLabel;
		private Button closeButton;
		private VisualElement tabBar;
		private VisualElement contentRoot;
		private VisualElement standaloneContent;
		private int selectedTabIndex = -1;
		private bool initialized;
		private bool isMoving;
		private ResizeDirection resizeDirection;
		private int activePointerId = -1;
		private Vector2 pointerStart;
		private Rect windowStartRect;
		private bool hasAppliedDefaultSize;
		private bool hasOpened;
		private bool hasRememberedWindowRect;
		private Rect rememberedWindowRect;

		public event Action Opened;
		public event Action Closed;
		public event Action<int> TabChanged;

		public bool IsOpen => initialized && windowRoot.style.display != DisplayStyle.None;
		public int SelectedTabIndex => selectedTabIndex;
		public int TabCount => tabs.Count;
		public VisualElement ContentRoot => contentRoot;

		public void SetOpenOnEnable(bool value)
		{
			openOnEnable = value;
		}

		public void SetDefaultSize(Vector2 size)
		{
			defaultSize = new Vector2(
				Mathf.Max(minimumSize.x, size.x),
				Mathf.Max(minimumSize.y, size.y));

			if (hasRememberedWindowRect)
				return;

			hasAppliedDefaultSize = false;

			if (initialized)
			{
				ApplyDefaultSize();
				windowRoot.schedule.Execute(ClampWindowToPanel);
			}
		}

		private void Reset()
		{
			uiDocument = GetComponent<UIDocument>();
		}

		private void OnEnable()
		{
			if (Initialize() == false)
				return;

			SetTitle(initialTitle);
			SetIcon(initialIcon);

			if (openOnEnable)
			{
				windowRoot.style.display = DisplayStyle.None;
				Open();
			}
			else
				Close(false);
		}

		private void OnDisable()
		{
			if (initialized && IsOpen && hasOpened)
				RememberWindowRect();

			if (closeButton != null)
				closeButton.clicked -= Close;

			UnbindWindowInteraction();

			initialized = false;
		}

		public bool Initialize()
		{
			if (initialized)
				return true;

			uiDocument ??= GetComponent<UIDocument>();
			if (uiDocument == null)
			{
				Debug.LogError("[UIWindow] UIDocument is missing.", this);
				return false;
			}

			VisualElement documentRoot = uiDocument.rootVisualElement;
			documentRoot.pickingMode = PickingMode.Ignore;
			windowRoot = documentRoot.Q<VisualElement>(RootName);
			titleBar = documentRoot.Q<VisualElement>(TitleBarName);
			iconElement = documentRoot.Q<VisualElement>(IconName);
			titleLabel = documentRoot.Q<Label>(TitleName);
			closeButton = documentRoot.Q<Button>(CloseButtonName);
			tabBar = documentRoot.Q<VisualElement>(TabBarName);
			contentRoot = documentRoot.Q<VisualElement>(ContentRootName);

			if (windowRoot == null || titleBar == null || iconElement == null || titleLabel == null ||
				closeButton == null || tabBar == null || contentRoot == null)
			{
				Debug.LogError("[UIWindow] The assigned UXML does not contain the required UIWindow elements.", this);
				return false;
			}

			closeButton.clicked -= Close;
			closeButton.clicked += Close;
			BindWindowInteraction();
			ApplyDefaultSize();

			for (int i = 0; i < tabs.Count; ++i)
			{
				tabBar.Add(tabs[i].Button);
				contentRoot.Add(tabs[i].Content);
			}

			if (tabs.Count == 0 && standaloneContent != null)
				contentRoot.Add(standaloneContent);

			initialized = true;
			RefreshTabVisibility();
			if (selectedTabIndex >= 0)
				SelectTab(selectedTabIndex);
			return true;
		}

		public void SetTitle(string title)
		{
			initialTitle = title ?? string.Empty;
			if (Initialize())
				titleLabel.text = initialTitle;
		}

		public void SetIcon(Sprite icon)
		{
			initialIcon = icon;
			if (Initialize() == false)
				return;

			if (icon != null)
				iconElement.style.backgroundImage = new StyleBackground(icon);
			else
				iconElement.style.backgroundImage = StyleKeyword.None;
			iconElement.style.display = icon != null ? DisplayStyle.Flex : DisplayStyle.None;
		}

		public void SetContent(VisualElement content)
		{
			if (Initialize() == false)
				return;

			standaloneContent?.RemoveFromHierarchy();
			standaloneContent = content;

			if (tabs.Count == 0 && standaloneContent != null)
				contentRoot.Add(standaloneContent);
		}

		public int AddTab(string label, VisualElement content)
		{
			if (Initialize() == false)
				return -1;

			if (content == null)
			{
				Debug.LogWarning("[UIWindow] A tab requires a content element.", this);
				return -1;
			}

			standaloneContent?.RemoveFromHierarchy();

			int index = tabs.Count;
			Button tabButton = new(() => SelectTab(index))
			{
				text = string.IsNullOrWhiteSpace(label) ? $"Tab {index + 1}" : label,
			};
			tabButton.AddToClassList(TabButtonClass);
			tabBar.Add(tabButton);

			content.style.flexGrow = 1f;
			content.style.display = DisplayStyle.None;
			contentRoot.Add(content);
			tabs.Add(new TabEntry(tabButton, content));

			RefreshTabVisibility();
			if (tabs.Count == 1)
				SelectTab(0);

			return index;
		}

		public bool SelectTab(int index)
		{
			if (index < 0 || index >= tabs.Count)
				return false;

			for (int i = 0; i < tabs.Count; ++i)
			{
				bool isSelected = i == index;
				tabs[i].Button.EnableInClassList(SelectedTabButtonClass, isSelected);
				tabs[i].Content.style.display = isSelected ? DisplayStyle.Flex : DisplayStyle.None;
			}

			if (selectedTabIndex == index)
				return true;

			selectedTabIndex = index;
			TabChanged?.Invoke(index);
			return true;
		}

		public void ClearTabs()
		{
			if (Initialize() == false)
				return;

			for (int i = 0; i < tabs.Count; ++i)
			{
				tabs[i].Button.RemoveFromHierarchy();
				tabs[i].Content.RemoveFromHierarchy();
			}

			tabs.Clear();
			selectedTabIndex = -1;
			RefreshTabVisibility();

			if (standaloneContent != null)
				contentRoot.Add(standaloneContent);
		}

		public void Open()
		{
			if (Initialize() == false || IsOpen)
				return;

			windowRoot.style.display = DisplayStyle.Flex;
			windowRoot.schedule.Execute(RestoreOrClampWindow);
			hasOpened = true;
			Opened?.Invoke();
		}

		public void Close()
		{
			Close(true);
		}

		private void Close(bool notify)
		{
			if (Initialize() == false || IsOpen == false)
				return;

			if (hasOpened)
				RememberWindowRect();

			windowRoot.style.display = DisplayStyle.None;
			if (notify)
				Closed?.Invoke();
		}

		private void RefreshTabVisibility()
		{
			if (tabBar != null)
				tabBar.style.display = tabs.Count > 0 ? DisplayStyle.Flex : DisplayStyle.None;
		}

		private void BindWindowInteraction()
		{
			UnbindWindowInteraction();
			titleBar.RegisterCallback<PointerDownEvent>(OnTitleBarPointerDown);
			windowRoot.RegisterCallback<PointerMoveEvent>(OnWindowPointerMove);
			windowRoot.RegisterCallback<PointerUpEvent>(OnWindowPointerUp);
			windowRoot.RegisterCallback<PointerCancelEvent>(OnWindowPointerCancel);

			RegisterResizeHandle("resize-top", ResizeDirection.Top);
			RegisterResizeHandle("resize-right", ResizeDirection.Right);
			RegisterResizeHandle("resize-bottom", ResizeDirection.Bottom);
			RegisterResizeHandle("resize-left", ResizeDirection.Left);
			RegisterResizeHandle("resize-top-left", ResizeDirection.Top | ResizeDirection.Left);
			RegisterResizeHandle("resize-top-right", ResizeDirection.Top | ResizeDirection.Right);
			RegisterResizeHandle("resize-bottom-left", ResizeDirection.Bottom | ResizeDirection.Left);
			RegisterResizeHandle("resize-bottom-right", ResizeDirection.Bottom | ResizeDirection.Right);
		}

		private void UnbindWindowInteraction()
		{
			if (titleBar != null)
				titleBar.UnregisterCallback<PointerDownEvent>(OnTitleBarPointerDown);

			if (windowRoot != null)
			{
				windowRoot.UnregisterCallback<PointerMoveEvent>(OnWindowPointerMove);
				windowRoot.UnregisterCallback<PointerUpEvent>(OnWindowPointerUp);
				windowRoot.UnregisterCallback<PointerCancelEvent>(OnWindowPointerCancel);
			}

			for (int i = 0; i < resizeHandleBindings.Count; ++i)
			{
				ResizeHandleBinding binding = resizeHandleBindings[i];
				binding.Handle.UnregisterCallback(binding.Callback);
			}

			resizeHandleBindings.Clear();
			EndWindowInteraction();
		}

		private void RegisterResizeHandle(string elementName, ResizeDirection direction)
		{
			VisualElement handle = windowRoot.Q<VisualElement>(elementName);
			if (handle == null)
				return;

			EventCallback<PointerDownEvent> callback = evt => OnResizeHandlePointerDown(evt, direction);
			handle.RegisterCallback(callback);
			resizeHandleBindings.Add(new ResizeHandleBinding(handle, callback));
		}

		private void OnTitleBarPointerDown(PointerDownEvent evt)
		{
			if (movable == false || evt.button != 0 || IsCloseButtonTarget(evt.target))
				return;

			BeginWindowInteraction(evt, true, ResizeDirection.None);
		}

		private void OnResizeHandlePointerDown(PointerDownEvent evt, ResizeDirection direction)
		{
			if (resizable == false || evt.button != 0)
				return;

			BeginWindowInteraction(evt, false, direction);
		}

		private void BeginWindowInteraction(PointerDownEvent evt, bool moveWindow, ResizeDirection direction)
		{
			windowStartRect = windowRoot.worldBound;
			pointerStart = new Vector2(evt.position.x, evt.position.y);
			activePointerId = evt.pointerId;
			isMoving = moveWindow;
			resizeDirection = direction;
			windowRoot.CapturePointer(activePointerId);
			evt.StopPropagation();
		}

		private void OnWindowPointerMove(PointerMoveEvent evt)
		{
			if (evt.pointerId != activePointerId || (isMoving == false && resizeDirection == ResizeDirection.None))
				return;

			Vector2 pointer = new(evt.position.x, evt.position.y);
			Vector2 delta = pointer - pointerStart;
			if (isMoving)
				MoveWindow(delta);
			else
				ResizeWindow(delta);

			evt.StopPropagation();
		}

		private void OnWindowPointerUp(PointerUpEvent evt)
		{
			if (evt.pointerId != activePointerId)
				return;

			EndWindowInteraction();
			evt.StopPropagation();
		}

		private void OnWindowPointerCancel(PointerCancelEvent evt)
		{
			if (evt.pointerId == activePointerId)
				EndWindowInteraction();
		}

		private void EndWindowInteraction()
		{
			if (windowRoot != null && activePointerId >= 0 && windowRoot.HasPointerCapture(activePointerId))
				windowRoot.ReleasePointer(activePointerId);

			if (isMoving || resizeDirection != ResizeDirection.None)
				RememberWindowRect();

			activePointerId = -1;
			isMoving = false;
			resizeDirection = ResizeDirection.None;
		}

		private void MoveWindow(Vector2 delta)
		{
			Vector2 panelSize = GetPanelSize();
			float maxX = Mathf.Max(0f, panelSize.x - windowStartRect.width);
			float maxY = Mathf.Max(0f, panelSize.y - windowStartRect.height);
			float x = Mathf.Clamp(windowStartRect.x + delta.x, 0f, maxX);
			float y = Mathf.Clamp(windowStartRect.y + delta.y, 0f, maxY);
			ApplyWindowRect(new Rect(x, y, windowStartRect.width, windowStartRect.height));
		}

		private void ResizeWindow(Vector2 delta)
		{
			Vector2 panelSize = GetPanelSize();
			float minimumWidth = Mathf.Min(minimumSize.x, panelSize.x);
			float minimumHeight = Mathf.Min(minimumSize.y, panelSize.y);
			float left = windowStartRect.x;
			float top = windowStartRect.y;
			float right = windowStartRect.xMax;
			float bottom = windowStartRect.yMax;

			if ((resizeDirection & ResizeDirection.Left) != 0)
				left = Mathf.Clamp(windowStartRect.x + delta.x, 0f, right - minimumWidth);
			if ((resizeDirection & ResizeDirection.Right) != 0)
				right = Mathf.Clamp(windowStartRect.xMax + delta.x, left + minimumWidth, panelSize.x);
			if ((resizeDirection & ResizeDirection.Top) != 0)
				top = Mathf.Clamp(windowStartRect.y + delta.y, 0f, bottom - minimumHeight);
			if ((resizeDirection & ResizeDirection.Bottom) != 0)
				bottom = Mathf.Clamp(windowStartRect.yMax + delta.y, top + minimumHeight, panelSize.y);

			ApplyWindowRect(Rect.MinMaxRect(left, top, right, bottom));
		}

		private void ClampWindowToPanel()
		{
			if (windowRoot == null)
				return;

			Rect clampedRect = ClampRectToPanel(windowRoot.worldBound);
			ApplyWindowRect(clampedRect);
			if (hasRememberedWindowRect)
				rememberedWindowRect = clampedRect;
		}

		private Rect ClampRectToPanel(Rect rect)
		{
			Vector2 panelSize = GetPanelSize();
			float width = Mathf.Clamp(rect.width, Mathf.Min(minimumSize.x, panelSize.x), panelSize.x);
			float height = Mathf.Clamp(rect.height, Mathf.Min(minimumSize.y, panelSize.y), panelSize.y);
			float x = Mathf.Clamp(rect.x, 0f, Mathf.Max(0f, panelSize.x - width));
			float y = Mathf.Clamp(rect.y, 0f, Mathf.Max(0f, panelSize.y - height));
			return new Rect(x, y, width, height);
		}

		private void RestoreOrClampWindow()
		{
			if (hasRememberedWindowRect)
			{
				rememberedWindowRect = ClampRectToPanel(rememberedWindowRect);
				ApplyWindowRect(rememberedWindowRect);
				return;
			}

			ClampWindowToPanel();
		}

		private void RememberWindowRect()
		{
			if (windowRoot == null)
				return;

			Rect rect = windowRoot.worldBound;
			if (IsFinite(rect.x) == false || IsFinite(rect.y) == false ||
				IsFinite(rect.width) == false || IsFinite(rect.height) == false ||
				rect.width <= 0f || rect.height <= 0f)
				return;

			rememberedWindowRect = ClampRectToPanel(rect);
			hasRememberedWindowRect = true;
		}

		private static bool IsFinite(float value)
		{
			return float.IsNaN(value) == false && float.IsInfinity(value) == false;
		}

		private Vector2 GetPanelSize()
		{
			VisualElement documentRoot = uiDocument.rootVisualElement;
			float width = documentRoot.resolvedStyle.width;
			float height = documentRoot.resolvedStyle.height;
			if (float.IsNaN(width) || width <= 0f)
				width = Screen.width;
			if (float.IsNaN(height) || height <= 0f)
				height = Screen.height;
			return new Vector2(width, height);
		}

		private void ApplyWindowRect(Rect rect)
		{
			windowRoot.style.translate = new Translate(0f, 0f);
			windowRoot.style.left = rect.x;
			windowRoot.style.top = rect.y;
			windowRoot.style.width = rect.width;
			windowRoot.style.height = rect.height;
		}

		private void ApplyDefaultSize()
		{
			if (windowRoot == null || hasAppliedDefaultSize)
				return;

			windowRoot.style.width = Mathf.Max(minimumSize.x, defaultSize.x);
			windowRoot.style.height = Mathf.Max(minimumSize.y, defaultSize.y);
			hasAppliedDefaultSize = true;
		}

		private bool IsCloseButtonTarget(IEventHandler target)
		{
			return target is VisualElement element && (element == closeButton || closeButton.Contains(element));
		}

		[Flags]
		private enum ResizeDirection
		{
			None = 0,
			Left = 1 << 0,
			Right = 1 << 1,
			Top = 1 << 2,
			Bottom = 1 << 3,
		}

		private readonly struct ResizeHandleBinding
		{
			public VisualElement Handle { get; }
			public EventCallback<PointerDownEvent> Callback { get; }

			public ResizeHandleBinding(VisualElement handle, EventCallback<PointerDownEvent> callback)
			{
				Handle = handle;
				Callback = callback;
			}
		}

		private readonly struct TabEntry
		{
			public Button Button { get; }
			public VisualElement Content { get; }

			public TabEntry(Button button, VisualElement content)
			{
				Button = button;
				Content = content;
			}
		}
	}
}
