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

		private readonly List<TabEntry> tabs = new();
		private VisualElement windowRoot;
		private VisualElement iconElement;
		private Label titleLabel;
		private Button closeButton;
		private VisualElement tabBar;
		private VisualElement contentRoot;
		private VisualElement standaloneContent;
		private int selectedTabIndex = -1;
		private bool initialized;

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
			if (closeButton != null)
				closeButton.clicked -= Close;

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
			iconElement = documentRoot.Q<VisualElement>(IconName);
			titleLabel = documentRoot.Q<Label>(TitleName);
			closeButton = documentRoot.Q<Button>(CloseButtonName);
			tabBar = documentRoot.Q<VisualElement>(TabBarName);
			contentRoot = documentRoot.Q<VisualElement>(ContentRootName);

			if (windowRoot == null || iconElement == null || titleLabel == null ||
				closeButton == null || tabBar == null || contentRoot == null)
			{
				Debug.LogError("[UIWindow] The assigned UXML does not contain the required UIWindow elements.", this);
				return false;
			}

			closeButton.clicked -= Close;
			closeButton.clicked += Close;

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

			windowRoot.style.display = DisplayStyle.None;
			if (notify)
				Closed?.Invoke();
		}

		private void RefreshTabVisibility()
		{
			if (tabBar != null)
				tabBar.style.display = tabs.Count > 0 ? DisplayStyle.Flex : DisplayStyle.None;
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
