using System.IO;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class EscapePauseMenu : MonoBehaviour
{
	[SerializeField] private string titleSceneName = "TitleScene";
	[SerializeField] private KeyCode toggleKey = KeyCode.Escape;

	[Header("Root")]
	[SerializeField] private GameObject root;

	[Header("Panels")]
	[SerializeField] private GameObject mainPanel;
	[SerializeField] private GameObject savePanel;
	[SerializeField] private GameObject settingsPanel;
	[SerializeField] private GameObject titleConfirmPanel;

	[Header("Main Menu")]
	[SerializeField] private Button resumeButton;
	[SerializeField] private Button openSaveButton;
	[SerializeField] private Button openSettingsButton;
	[SerializeField] private Button openTitleConfirmButton;

	[Header("Save Menu")]
	[SerializeField] private TMP_InputField saveNameInput;
	[SerializeField] private TMP_Text saveMessageText;
	[SerializeField] private Button confirmSaveButton;
	[SerializeField] private Button backFromSaveButton;

	[Header("Settings Menu")]
	[SerializeField] private Button backFromSettingsButton;

	[Header("Title Confirmation")]
	[SerializeField] private Button confirmTitleButton;
	[SerializeField] private Button cancelTitleButton;

	private bool isOpen;
	private bool loadingTitleScene;

	private GameTime GameTime => GameContext.HasInstance ? GameContext.Instance.GameTime : null;

	private void Awake()
	{
		if (HasRequiredReferences() == false)
		{
			Debug.LogWarning("[PauseMenu] EscapePauseMenu prefab references are not fully assigned.");
			enabled = false;
			return;
		}

		root.transform.SetAsLastSibling();
		root.SetActive(false);
	}

	private void OnEnable()
	{
		resumeButton?.onClick.AddListener(ResumeGame);
		openSaveButton?.onClick.AddListener(OpenSavePanel);
		openSettingsButton?.onClick.AddListener(OpenSettingsPanel);
		openTitleConfirmButton?.onClick.AddListener(OpenTitleConfirmPanel);
		confirmSaveButton?.onClick.AddListener(SaveGame);
		backFromSaveButton?.onClick.AddListener(ShowMainPanel);
		backFromSettingsButton?.onClick.AddListener(ShowMainPanel);
		confirmTitleButton?.onClick.AddListener(LoadTitleScene);
		cancelTitleButton?.onClick.AddListener(ShowMainPanel);
	}

	private void OnDisable()
	{
		resumeButton?.onClick.RemoveListener(ResumeGame);
		openSaveButton?.onClick.RemoveListener(OpenSavePanel);
		openSettingsButton?.onClick.RemoveListener(OpenSettingsPanel);
		openTitleConfirmButton?.onClick.RemoveListener(OpenTitleConfirmPanel);
		confirmSaveButton?.onClick.RemoveListener(SaveGame);
		backFromSaveButton?.onClick.RemoveListener(ShowMainPanel);
		backFromSettingsButton?.onClick.RemoveListener(ShowMainPanel);
		confirmTitleButton?.onClick.RemoveListener(LoadTitleScene);
		cancelTitleButton?.onClick.RemoveListener(ShowMainPanel);
	}

	private void Update()
	{
		if (Input.GetKeyDown(toggleKey) == false)
			return;

		if (isOpen)
			ResumeGame();
		else
			Open();
	}

	private void OnDestroy()
	{
		if (loadingTitleScene || isOpen == false || GameTime == null)
			return;

		GameTime.ResumePreservedSpeed();
	}

	private void Open()
	{
		if (GameTime == null)
			return;

		GameTime.PausePreservingSpeed();
		isOpen = true;
		root.SetActive(true);
		ShowMainPanel();
	}

	private void ResumeGame()
	{
		root.SetActive(false);
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
		if (saveNameInput != null)
		{
			saveNameInput.text = $"Save_{System.DateTime.Now:yyyyMMdd_HHmmss}";
			saveNameInput.Select();
			saveNameInput.ActivateInputField();
		}

		SetSaveMessage("Enter a save file name.");
	}

	private void SaveGame()
	{
		if (GameContext.HasInstance == false || GameContext.Instance.SaveService == null)
		{
			SetSaveMessage("Save service is not ready.");
			return;
		}

		string saveName = saveNameInput != null ? saveNameInput.text : string.Empty;
		string sanitizedName = SanitizeSaveName(saveName);
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

	private void ShowPanel(GameObject panel)
	{
		mainPanel.SetActive(panel == mainPanel);
		savePanel.SetActive(panel == savePanel);
		settingsPanel.SetActive(panel == settingsPanel);
		titleConfirmPanel.SetActive(panel == titleConfirmPanel);
	}

	private void SetSaveMessage(string message)
	{
		if (saveMessageText != null)
			saveMessageText.text = message;
	}

	private bool HasRequiredReferences()
	{
		return root != null
			&& mainPanel != null
			&& savePanel != null
			&& settingsPanel != null
			&& titleConfirmPanel != null
			&& resumeButton != null
			&& openSaveButton != null
			&& openSettingsButton != null
			&& openTitleConfirmButton != null
			&& saveNameInput != null
			&& saveMessageText != null
			&& confirmSaveButton != null
			&& backFromSaveButton != null
			&& backFromSettingsButton != null
			&& confirmTitleButton != null
			&& cancelTitleButton != null;
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
