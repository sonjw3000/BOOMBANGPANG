using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

public sealed class EscapePauseMenu : MonoBehaviour
{
	private const string DocumentObjectName = "EscapePauseMenuDocument";
	private const string RootName = "escape-pause-menu";
	private const string MainPanelName = "pause-main-panel";
	private const string SavePanelName = "pause-save-panel";
	private const string SettingsPanelName = "pause-settings-panel";
	private const string TitleConfirmPanelName = "pause-title-confirm-panel";

	[SerializeField] private string titleSceneName = "TitleScene";
	[SerializeField] private KeyCode toggleKey = KeyCode.Escape;
	[SerializeField] private VisualTreeAsset visualTreeAsset;
	[SerializeField] private PanelSettings panelSettings;
	[SerializeField] private int sortingOrder = 200;

	private UIDocument uiDocument;
	private VisualElement root;
	private VisualElement mainPanel;
	private VisualElement savePanel;
	private VisualElement settingsPanel;
	private VisualElement titleConfirmPanel;
	private Button resumeButton;
	private Button openSaveButton;
	private Button openSettingsButton;
	private Button openTitleConfirmButton;
	private TextField saveNameInput;
	private Label saveMessageLabel;
	private Button confirmSaveButton;
	private Button backFromSaveButton;
	private Button backFromSettingsButton;
	private Button confirmTitleButton;
	private Button cancelTitleButton;
	[System.NonSerialized] private bool initialized;
	private bool isOpen;
	private bool loadingTitleScene;

	private GameTime GameTime => GameContext.HasInstance ? GameContext.Instance.GameTime : null;

	private void Awake()
	{
		EnsureInitialized();
	}

	private void OnEnable()
	{
		EnsureInitialized();
	}

	private void OnDisable()
	{
		UnbindControls();
		initialized = false;
	}

	private void Update()
	{
		if (Input.GetKeyDown(toggleKey) == false)
			return;

		if (isOpen)
			ResumeGame();
		else if (GameContext.HasInstance &&
			GameContext.Instance.InteractionCtx != null &&
			GameContext.Instance.InteractionCtx.TryCancelActiveMode())
		{
			return;
		}
		else
			Open();
	}

	private void OnDestroy()
	{
		if (loadingTitleScene || isOpen == false || GameTime == null)
			return;

		GameTime.ResumePreservedSpeed();
	}

	private bool EnsureInitialized()
	{
		if (initialized)
			return true;

		if (visualTreeAsset == null || panelSettings == null)
		{
			Debug.LogError("[PauseMenu] VisualTreeAsset or PanelSettings is missing.", this);
			enabled = false;
			return false;
		}

		EnsureDocument();
		VisualElement documentRoot = uiDocument.rootVisualElement;
		root = documentRoot.Q<VisualElement>(RootName);
		mainPanel = documentRoot.Q<VisualElement>(MainPanelName);
		savePanel = documentRoot.Q<VisualElement>(SavePanelName);
		settingsPanel = documentRoot.Q<VisualElement>(SettingsPanelName);
		titleConfirmPanel = documentRoot.Q<VisualElement>(TitleConfirmPanelName);
		resumeButton = documentRoot.Q<Button>("pause-resume-button");
		openSaveButton = documentRoot.Q<Button>("pause-save-menu-button");
		openSettingsButton = documentRoot.Q<Button>("pause-settings-menu-button");
		openTitleConfirmButton = documentRoot.Q<Button>("pause-title-menu-button");
		saveNameInput = documentRoot.Q<TextField>("pause-save-name-input");
		saveMessageLabel = documentRoot.Q<Label>("pause-save-message");
		confirmSaveButton = documentRoot.Q<Button>("pause-confirm-save-button");
		backFromSaveButton = documentRoot.Q<Button>("pause-save-back-button");
		backFromSettingsButton = documentRoot.Q<Button>("pause-settings-back-button");
		confirmTitleButton = documentRoot.Q<Button>("pause-confirm-title-button");
		cancelTitleButton = documentRoot.Q<Button>("pause-cancel-title-button");

		if (HasRequiredElements() == false)
		{
			Debug.LogError("[PauseMenu] Required UXML elements are missing.", this);
			enabled = false;
			return false;
		}

		BindControls();
		root.style.display = DisplayStyle.None;
		ShowPanel(mainPanel);
		initialized = true;
		return true;
	}

	private void EnsureDocument()
	{
		if (uiDocument != null)
			return;

		GameObject documentObject = new(DocumentObjectName);
		documentObject.SetActive(false);
		documentObject.transform.SetParent(transform, false);
		uiDocument = documentObject.AddComponent<UIDocument>();
		uiDocument.panelSettings = panelSettings;
		uiDocument.visualTreeAsset = visualTreeAsset;
		uiDocument.sortingOrder = sortingOrder;
		documentObject.SetActive(true);
	}

	private bool HasRequiredElements()
	{
		return root != null && mainPanel != null && savePanel != null && settingsPanel != null &&
			titleConfirmPanel != null && resumeButton != null && openSaveButton != null &&
			openSettingsButton != null && openTitleConfirmButton != null && saveNameInput != null &&
			saveMessageLabel != null && confirmSaveButton != null && backFromSaveButton != null &&
			backFromSettingsButton != null && confirmTitleButton != null && cancelTitleButton != null;
	}

	private void BindControls()
	{
		UnbindControls();
		resumeButton.clicked += ResumeGame;
		openSaveButton.clicked += OpenSavePanel;
		openSettingsButton.clicked += OpenSettingsPanel;
		openTitleConfirmButton.clicked += OpenTitleConfirmPanel;
		confirmSaveButton.clicked += SaveGame;
		backFromSaveButton.clicked += ShowMainPanel;
		backFromSettingsButton.clicked += ShowMainPanel;
		confirmTitleButton.clicked += LoadTitleScene;
		cancelTitleButton.clicked += ShowMainPanel;
	}

	private void UnbindControls()
	{
		if (resumeButton != null) resumeButton.clicked -= ResumeGame;
		if (openSaveButton != null) openSaveButton.clicked -= OpenSavePanel;
		if (openSettingsButton != null) openSettingsButton.clicked -= OpenSettingsPanel;
		if (openTitleConfirmButton != null) openTitleConfirmButton.clicked -= OpenTitleConfirmPanel;
		if (confirmSaveButton != null) confirmSaveButton.clicked -= SaveGame;
		if (backFromSaveButton != null) backFromSaveButton.clicked -= ShowMainPanel;
		if (backFromSettingsButton != null) backFromSettingsButton.clicked -= ShowMainPanel;
		if (confirmTitleButton != null) confirmTitleButton.clicked -= LoadTitleScene;
		if (cancelTitleButton != null) cancelTitleButton.clicked -= ShowMainPanel;
	}

	private void Open()
	{
		if (EnsureInitialized() == false || GameTime == null)
			return;

		GameTime.PausePreservingSpeed();
		isOpen = true;
		root.style.display = DisplayStyle.Flex;
		ShowMainPanel();
	}

	private void ResumeGame()
	{
		if (root != null)
			root.style.display = DisplayStyle.None;
		isOpen = false;
		GameTime?.ResumePreservedSpeed();
	}

	private void ShowMainPanel()
	{
		ShowPanel(mainPanel);
	}

	private void OpenSavePanel()
	{
		ShowPanel(savePanel);
		saveNameInput.value = $"Save_{System.DateTime.Now:yyyyMMdd_HHmmss}";
		SetSaveMessage("Enter a save file name.");
		saveNameInput.schedule.Execute(saveNameInput.Focus);
	}

	private void SaveGame()
	{
		if (GameContext.HasInstance == false || GameContext.Instance.SaveService == null)
		{
			SetSaveMessage("Save service is not ready.");
			return;
		}

		string sanitizedName = SanitizeSaveName(saveNameInput.value);
		if (string.IsNullOrWhiteSpace(sanitizedName))
		{
			SetSaveMessage("Please enter a valid save name.");
			return;
		}

		Directory.CreateDirectory(GameSaveService.SaveDirectoryPath);
		string savePath = Path.Combine(GameSaveService.SaveDirectoryPath, sanitizedName + ".json");
		GameContext.Instance.SaveService.SaveGame(savePath);
		SetSaveMessage($"Saved: {sanitizedName}");
	}

	private void OpenSettingsPanel()
	{
		ShowPanel(settingsPanel);
	}

	private void OpenTitleConfirmPanel()
	{
		ShowPanel(titleConfirmPanel);
	}

	private void LoadTitleScene()
	{
		loadingTitleScene = true;
		isOpen = false;
		Time.timeScale = 1.0f;
		SceneManager.LoadScene(titleSceneName, LoadSceneMode.Single);
	}

	private void ShowPanel(VisualElement panel)
	{
		if (mainPanel == null)
			return;

		mainPanel.style.display = panel == mainPanel ? DisplayStyle.Flex : DisplayStyle.None;
		savePanel.style.display = panel == savePanel ? DisplayStyle.Flex : DisplayStyle.None;
		settingsPanel.style.display = panel == settingsPanel ? DisplayStyle.Flex : DisplayStyle.None;
		titleConfirmPanel.style.display = panel == titleConfirmPanel ? DisplayStyle.Flex : DisplayStyle.None;
	}

	private void SetSaveMessage(string message)
	{
		saveMessageLabel.text = message;
	}

	private static string SanitizeSaveName(string saveName)
	{
		if (string.IsNullOrWhiteSpace(saveName))
			return string.Empty;

		char[] invalidChars = Path.GetInvalidFileNameChars();
		string sanitized = new(saveName.Trim().Select(ch => invalidChars.Contains(ch) ? '_' : ch).ToArray());
		return sanitized.Trim();
	}
}
