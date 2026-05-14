using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class TitleSceneController : MonoBehaviour
{
	[SerializeField] private string initialSceneName = "GameScene";
	[SerializeField] private string finalGameplaySceneName = "GameScene";
	[SerializeField] private float playerTurnDuration = 1.1f;
	[SerializeField] private float playerTurnAngle = 180f;

	private Button startNewButton;
	private Button loadButton;
	private Button settingsButton;
	private Button exitButton;
	private Canvas canvas;
	private Transform playerTransform;
	private TMP_Text templateText;
	private Button templateButton;

	private RuntimeLoadWindow loadWindow;
	private RuntimeSimpleWindow settingsWindow;
	private readonly List<RuntimeSaveEntry> saveEntries = new();
	private string selectedSavePath;
	private bool transitionInProgress;

	private void Awake()
	{
		if (TryCacheReferences() == false)
		{
			Debug.LogWarning("[Title] Failed to initialize title scene controller.");
			enabled = false;
			return;
		}

		BuildRuntimeWindows();
		BindButtons();
	}

	private void OnDestroy()
	{
		UnbindButtons();
	}

	private bool TryCacheReferences()
	{
		canvas = GetComponentInChildren<Canvas>(true);
		playerTransform = GameObject.Find("PlayerOfficeSpace/Player")?.transform;

		startNewButton = FindButton("Canvas/LeftTitleItems/Buttons/StartNew");
		loadButton = FindButton("Canvas/LeftTitleItems/Buttons/Load");
		settingsButton = FindButton("Canvas/LeftTitleItems/Buttons/Settings");
		exitButton = FindButton("Canvas/LeftTitleItems/Buttons/Exit");

		templateButton = loadButton != null ? loadButton : startNewButton;
		templateText = templateButton != null ? templateButton.GetComponentInChildren<TMP_Text>(true) : null;

		return canvas != null
			&& startNewButton != null
			&& loadButton != null
			&& settingsButton != null
			&& exitButton != null
			&& templateButton != null
			&& templateText != null;
	}

	private void BindButtons()
	{
		startNewButton.onClick.AddListener(HandleNewGameClicked);
		loadButton.onClick.AddListener(HandleLoadClicked);
		settingsButton.onClick.AddListener(HandleSettingsClicked);
		exitButton.onClick.AddListener(HandleExitClicked);
	}

	private void UnbindButtons()
	{
		if (startNewButton != null)
			startNewButton.onClick.RemoveListener(HandleNewGameClicked);

		if (loadButton != null)
			loadButton.onClick.RemoveListener(HandleLoadClicked);

		if (settingsButton != null)
			settingsButton.onClick.RemoveListener(HandleSettingsClicked);

		if (exitButton != null)
			exitButton.onClick.RemoveListener(HandleExitClicked);
	}

	private void HandleNewGameClicked()
	{
		BeginStartFlow(TitleSceneStartRequest.NewGame());
	}

	private void HandleLoadClicked()
	{
		if (transitionInProgress)
			return;

		settingsWindow.Close();
		OpenLoadWindow();
	}

	private void HandleSettingsClicked()
	{
		if (transitionInProgress)
			return;

		loadWindow.Close();
		settingsWindow.Open();
	}

	private void HandleExitClicked()
	{
#if UNITY_EDITOR
		UnityEditor.EditorApplication.isPlaying = false;
#else
		Application.Quit();
#endif
	}

	private void BeginStartFlow(TitleSceneStartRequest request)
	{
		if (transitionInProgress)
			return;

		transitionInProgress = true;
		SetMenuInteractable(false);
		loadWindow.Close();
		settingsWindow.Close();
		StartCoroutine(PlayStartFlow(request));
	}

	private IEnumerator PlayStartFlow(TitleSceneStartRequest request)
	{
		if (playerTransform != null)
		{
			Quaternion startRotation = playerTransform.rotation;
			Quaternion endRotation = startRotation * Quaternion.Euler(0f, playerTurnAngle, 0f);
			float elapsed = 0f;

			while (elapsed < playerTurnDuration)
			{
				elapsed += Time.deltaTime;
				float t = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, playerTurnDuration));
				playerTransform.rotation = Quaternion.Slerp(startRotation, endRotation, t);
				yield return null;
			}

			playerTransform.rotation = endRotation;
		}

		TitleSceneLoadBridge.BeginRoute(request, initialSceneName, finalGameplaySceneName);
	}

	private void BuildRuntimeWindows()
	{
		loadWindow = CreateLoadWindow();
		settingsWindow = CreateSettingsWindow();
	}

	private void OpenLoadWindow()
	{
		RefreshSaveList();
		loadWindow.Open();
	}

	private void RefreshSaveList()
	{
		selectedSavePath = null;
		loadWindow.SetConfirmInteractable(false);
		loadWindow.ClearEntries();
		saveEntries.Clear();

		IReadOnlyList<GameSaveFileSummary> saveFiles = GameSaveFileCatalog.EnumerateAllJsonFiles();
		if (saveFiles.Count == 0)
		{
			loadWindow.SetMessage("No save files were found.");
			return;
		}

		loadWindow.SetMessage("Select a save file to load.");

		foreach (GameSaveFileSummary summary in saveFiles)
		{
			RuntimeSaveEntry entry = loadWindow.AddEntry(summary, templateButton, OnSaveEntrySelected);
			saveEntries.Add(entry);
		}
	}

	private void OnSaveEntrySelected(GameSaveFileSummary summary)
	{
		selectedSavePath = summary.IsLoadable ? summary.FilePath : null;

		foreach (RuntimeSaveEntry entry in saveEntries)
			entry.SetSelected(entry.Summary.FilePath == summary.FilePath);

		loadWindow.SetConfirmInteractable(summary.IsLoadable);
	}

	private RuntimeLoadWindow CreateLoadWindow()
	{
		RuntimeModalRoot modal = CreateModalRoot("LoadGameWindow", "Load Game");
		modal.Message.text = "Select a save file to load.";

		ScrollRect scrollRect = CreateScrollArea(modal.ContentRoot);
		Button confirmButton = CloneButton(templateButton, modal.FooterRow, "ConfirmButton", "Confirm");
		Button closeButton = CloneButton(templateButton, modal.FooterRow, "CloseButton", "Close");

		RuntimeLoadWindow window = new RuntimeLoadWindow(modal.Root, modal.Message, scrollRect.content, confirmButton);
		confirmButton.onClick.AddListener(() =>
		{
			if (string.IsNullOrWhiteSpace(selectedSavePath))
				return;

			BeginStartFlow(TitleSceneStartRequest.LoadSave(selectedSavePath));
		});
		closeButton.onClick.AddListener(window.Close);
		window.SetConfirmInteractable(false);
		return window;
	}

	private RuntimeSimpleWindow CreateSettingsWindow()
	{
		RuntimeModalRoot modal = CreateModalRoot("SettingsWindow", "Settings");
		modal.Message.text = "Settings will be added here later.";

		Button closeButton = CloneButton(templateButton, modal.FooterRow, "CloseButton", "Close");
		RuntimeSimpleWindow window = new RuntimeSimpleWindow(modal.Root);
		closeButton.onClick.AddListener(window.Close);
		return window;
	}

	private RuntimeModalRoot CreateModalRoot(string objectName, string title)
	{
		GameObject overlay = CreateUiObject(objectName, canvas.transform, typeof(Image));
		RectTransform overlayRect = overlay.GetComponent<RectTransform>();
		StretchToParent(overlayRect);
		Image overlayImage = overlay.GetComponent<Image>();
		overlayImage.color = new Color(0f, 0f, 0f, 0.55f);

		GameObject panelObject = CreateUiObject("Panel", overlay.transform, typeof(Image), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter), typeof(LayoutElement));
		RectTransform panelRect = panelObject.GetComponent<RectTransform>();
		panelRect.anchorMin = new Vector2(0.5f, 0.5f);
		panelRect.anchorMax = new Vector2(0.5f, 0.5f);
		panelRect.pivot = new Vector2(0.5f, 0.5f);
		panelRect.sizeDelta = new Vector2(820f, 560f);
		Image panelImage = panelObject.GetComponent<Image>();
		panelImage.color = new Color(0.11f, 0.13f, 0.17f, 0.96f);

		VerticalLayoutGroup layout = panelObject.GetComponent<VerticalLayoutGroup>();
		layout.padding = new RectOffset(28, 28, 24, 24);
		layout.spacing = 18f;
		layout.childAlignment = TextAnchor.UpperCenter;
		layout.childControlWidth = true;
		layout.childControlHeight = false;
		layout.childForceExpandHeight = false;
		layout.childForceExpandWidth = true;

		ContentSizeFitter fitter = panelObject.GetComponent<ContentSizeFitter>();
		fitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;
		fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

		LayoutElement panelLayout = panelObject.GetComponent<LayoutElement>();
		panelLayout.preferredWidth = 820f;
		panelLayout.preferredHeight = 560f;

		TMP_Text titleText = CloneText(templateText, panelObject.transform, "Title", title);
		titleText.fontSize = Mathf.Max(titleText.fontSize + 8f, 40f);
		titleText.alignment = TextAlignmentOptions.Center;

		TMP_Text messageText = CloneText(templateText, panelObject.transform, "Message", string.Empty);
		messageText.alignment = TextAlignmentOptions.Center;
		messageText.textWrappingMode = TextWrappingModes.Normal;

		GameObject contentObject = CreateUiObject("Content", panelObject.transform, typeof(LayoutElement));
		LayoutElement contentLayout = contentObject.GetComponent<LayoutElement>();
		contentLayout.flexibleHeight = 1f;
		contentLayout.minHeight = 280f;

		GameObject footerObject = CreateUiObject("Footer", panelObject.transform, typeof(HorizontalLayoutGroup), typeof(LayoutElement));
		HorizontalLayoutGroup footerLayout = footerObject.GetComponent<HorizontalLayoutGroup>();
		footerLayout.spacing = 16f;
		footerLayout.childAlignment = TextAnchor.MiddleCenter;
		footerLayout.childControlHeight = true;
		footerLayout.childControlWidth = false;
		footerLayout.childForceExpandWidth = false;
		footerLayout.childForceExpandHeight = false;
		LayoutElement footerElement = footerObject.GetComponent<LayoutElement>();
		footerElement.minHeight = 72f;

		overlay.SetActive(false);
		return new RuntimeModalRoot(overlay, messageText, contentObject.transform, footerObject.transform);
	}

	private ScrollRect CreateScrollArea(Transform parent)
	{
		GameObject frameObject = CreateUiObject("ListFrame", parent, typeof(Image), typeof(LayoutElement));
		Image frameImage = frameObject.GetComponent<Image>();
		frameImage.color = new Color(0.17f, 0.20f, 0.25f, 0.95f);
		LayoutElement frameLayout = frameObject.GetComponent<LayoutElement>();
		frameLayout.flexibleHeight = 1f;
		frameLayout.minHeight = 300f;

		GameObject scrollObject = CreateUiObject("ScrollView", frameObject.transform, typeof(ScrollRect));
		RectTransform scrollRectTransform = scrollObject.GetComponent<RectTransform>();
		StretchToParent(scrollRectTransform, 10f, 10f, 10f, 10f);

		GameObject viewportObject = CreateUiObject("Viewport", scrollObject.transform, typeof(Image), typeof(Mask));
		RectTransform viewportRect = viewportObject.GetComponent<RectTransform>();
		StretchToParent(viewportRect);
		Image viewportImage = viewportObject.GetComponent<Image>();
		viewportImage.color = new Color(0f, 0f, 0f, 0.08f);
		viewportObject.GetComponent<Mask>().showMaskGraphic = false;

		GameObject contentObject = CreateUiObject("Content", viewportObject.transform, typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
		RectTransform contentRect = contentObject.GetComponent<RectTransform>();
		contentRect.anchorMin = new Vector2(0f, 1f);
		contentRect.anchorMax = new Vector2(1f, 1f);
		contentRect.pivot = new Vector2(0.5f, 1f);
		contentRect.anchoredPosition = Vector2.zero;
		contentRect.sizeDelta = new Vector2(0f, 0f);

		VerticalLayoutGroup contentLayout = contentObject.GetComponent<VerticalLayoutGroup>();
		contentLayout.padding = new RectOffset(8, 8, 8, 8);
		contentLayout.spacing = 10f;
		contentLayout.childAlignment = TextAnchor.UpperCenter;
		contentLayout.childControlWidth = true;
		contentLayout.childControlHeight = false;
		contentLayout.childForceExpandHeight = false;
		contentLayout.childForceExpandWidth = true;

		ContentSizeFitter contentFitter = contentObject.GetComponent<ContentSizeFitter>();
		contentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
		contentFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

		ScrollRect scrollRect = scrollObject.GetComponent<ScrollRect>();
		scrollRect.horizontal = false;
		scrollRect.movementType = ScrollRect.MovementType.Clamped;
		scrollRect.viewport = viewportRect;
		scrollRect.content = contentRect;

		return scrollRect;
	}

	private void SetMenuInteractable(bool interactable)
	{
		startNewButton.interactable = interactable;
		loadButton.interactable = interactable;
		settingsButton.interactable = interactable;
		exitButton.interactable = interactable;
	}

	private Button FindButton(string relativePath)
	{
		Transform target = transform.Find(relativePath);
		return target != null ? target.GetComponent<Button>() : null;
	}

	private static void StretchToParent(RectTransform rectTransform, float left = 0f, float right = 0f, float top = 0f, float bottom = 0f)
	{
		rectTransform.anchorMin = Vector2.zero;
		rectTransform.anchorMax = Vector2.one;
		rectTransform.offsetMin = new Vector2(left, bottom);
		rectTransform.offsetMax = new Vector2(-right, -top);
	}

	private static GameObject CreateUiObject(string name, Transform parent, params System.Type[] componentTypes)
	{
		GameObject gameObject = new(name, componentTypes.Prepend(typeof(RectTransform)).ToArray());
		gameObject.transform.SetParent(parent, false);
		return gameObject;
	}

	private TMP_Text CloneText(TMP_Text template, Transform parent, string objectName, string label)
	{
		TMP_Text text = Instantiate(template, parent);
		text.name = objectName;
		text.text = label;
		return text;
	}

	private Button CloneButton(Button template, Transform parent, string objectName, string label)
	{
		Button button = Instantiate(template, parent);
		button.name = objectName;
		RectTransform rectTransform = button.GetComponent<RectTransform>();
		rectTransform.localScale = Vector3.one;
		rectTransform.sizeDelta = new Vector2(rectTransform.sizeDelta.x, Mathf.Max(rectTransform.sizeDelta.y, 56f));

		TMP_Text labelText = button.GetComponentInChildren<TMP_Text>(true);
		if (labelText != null)
		{
			labelText.text = label;
			labelText.textWrappingMode = TextWrappingModes.Normal;
			labelText.alignment = TextAlignmentOptions.Center;
		}

		button.onClick = new Button.ButtonClickedEvent();

		LayoutElement layout = button.GetComponent<LayoutElement>();
		if (layout == null)
			layout = button.gameObject.AddComponent<LayoutElement>();
		layout.minHeight = Mathf.Max(layout.minHeight, 56f);
		layout.preferredHeight = Mathf.Max(layout.preferredHeight, 56f);
		layout.flexibleWidth = 1f;

		return button;
	}

	private sealed class RuntimeModalRoot
	{
		public RuntimeModalRoot(GameObject root, TMP_Text message, Transform contentRoot, Transform footerRow)
		{
			Root = root;
			Message = message;
			ContentRoot = contentRoot;
			FooterRow = footerRow;
		}

		public GameObject Root { get; }
		public TMP_Text Message { get; }
		public Transform ContentRoot { get; }
		public Transform FooterRow { get; }
	}

	private sealed class RuntimeSimpleWindow
	{
		private readonly GameObject root;

		public RuntimeSimpleWindow(GameObject root)
		{
			this.root = root;
		}

		public void Open()
		{
			root.SetActive(true);
		}

		public void Close()
		{
			root.SetActive(false);
		}
	}

	private sealed class RuntimeLoadWindow
	{
		private readonly GameObject root;
		private readonly TMP_Text messageText;
		private readonly Transform listRoot;
		private readonly Button confirmButton;

		public RuntimeLoadWindow(GameObject root, TMP_Text messageText, Transform listRoot, Button confirmButton)
		{
			this.root = root;
			this.messageText = messageText;
			this.listRoot = listRoot;
			this.confirmButton = confirmButton;
		}

		public void Open()
		{
			root.SetActive(true);
		}

		public void Close()
		{
			root.SetActive(false);
		}

		public void SetMessage(string message)
		{
			messageText.text = message;
		}

		public void SetConfirmInteractable(bool interactable)
		{
			confirmButton.interactable = interactable;
		}

		public void ClearEntries()
		{
			for (int i = listRoot.childCount - 1; i >= 0; i--)
				Destroy(listRoot.GetChild(i).gameObject);
		}

		public RuntimeSaveEntry AddEntry(GameSaveFileSummary summary, Button templateButton, System.Action<GameSaveFileSummary> onSelect)
		{
			Button entryButton = Object.Instantiate(templateButton, listRoot);
			entryButton.name = $"{summary.SaveName}_Entry";
			entryButton.onClick = new Button.ButtonClickedEvent();

			LayoutElement layout = entryButton.GetComponent<LayoutElement>();
			if (layout == null)
				layout = entryButton.gameObject.AddComponent<LayoutElement>();
			layout.minHeight = 84f;
			layout.preferredHeight = 84f;
			layout.flexibleWidth = 1f;

			TMP_Text label = entryButton.GetComponentInChildren<TMP_Text>(true);
			if (label != null)
			{
				label.alignment = TextAlignmentOptions.TopLeft;
				label.textWrappingMode = TextWrappingModes.Normal;
				label.text = BuildEntryLabel(summary);
			}

			RuntimeSaveEntry entry = new RuntimeSaveEntry(summary, entryButton);
			entryButton.interactable = true;
			entryButton.onClick.AddListener(() => onSelect?.Invoke(summary));
			entry.SetSelected(false);
			return entry;
		}

		private static string BuildEntryLabel(GameSaveFileSummary summary)
		{
			if (summary.IsLoadable == false)
				return $"{summary.SaveName}\nStatus: {summary.StatusText}";

			return $"{summary.SaveName}\nSaved: {summary.SavedAtText} | Version: {summary.Version}\nMoney: ${summary.Money:N0} | Reputation: {summary.Reputation:F1}";
		}
	}

	private sealed class RuntimeSaveEntry
	{
		private static readonly Color SelectedColor = new(0.66f, 0.84f, 1f, 1f);

		private readonly TMP_Text label;
		private readonly Image image;
		private readonly Color defaultLabelColor;
		private readonly Color defaultImageColor;

		public RuntimeSaveEntry(GameSaveFileSummary summary, Button button)
		{
			Summary = summary;
			Button = button;
			label = button.GetComponentInChildren<TMP_Text>(true);
			image = button.image;
			defaultLabelColor = label != null ? label.color : Color.white;
			defaultImageColor = image != null ? image.color : Color.white;
		}

		public GameSaveFileSummary Summary { get; }
		public Button Button { get; }

		public void SetSelected(bool selected)
		{
			if (label != null)
				label.color = selected ? SelectedColor : defaultLabelColor;

			if (image != null)
				image.color = selected ? new Color(0.27f, 0.37f, 0.52f, 1f) : defaultImageColor;
		}
	}
}

public static class TitleSceneRuntimeInstaller
{
	[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
	private static void Install()
	{
		if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != "TitleScene")
			return;

		GameObject titleRoot = GameObject.Find("UI_Title");
		if (titleRoot == null)
			return;

		if (titleRoot.GetComponent<TitleSceneController>() == null)
			titleRoot.AddComponent<TitleSceneController>();
	}
}
