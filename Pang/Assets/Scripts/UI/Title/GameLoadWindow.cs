using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class GameLoadWindow : MonoBehaviour
{
	[SerializeField] private TMP_Text messageText;
	[SerializeField] private Transform saveListRoot;
	[SerializeField] private Button saveEntryButtonPrefab;
	[SerializeField] private TMP_Text selectedSaveTitleText;
	[SerializeField] private TMP_Text selectedSaveInfoText;
	[SerializeField] private Button loadButton;
	[SerializeField] private Button deleteButton;
	[SerializeField] private Button closeButton;

	private readonly List<SaveEntryView> entries = new();
	private GameSaveFileSummary? selectedSummary;
	private System.Action<GameSaveFileSummary> loadRequested;

	public void Initialize(System.Action<GameSaveFileSummary> onLoadRequested)
	{
		loadRequested = onLoadRequested;

		if (loadButton != null)
		{
			loadButton.onClick.RemoveListener(HandleLoadClicked);
			loadButton.onClick.AddListener(HandleLoadClicked);
		}

		if (deleteButton != null)
		{
			deleteButton.onClick.RemoveAllListeners();
			deleteButton.interactable = true;
		}

		if (closeButton != null)
		{
			closeButton.onClick.RemoveListener(Close);
			closeButton.onClick.AddListener(Close);
		}

		ClearSelection();
		gameObject.SetActive(false);
	}

	public void Open(IReadOnlyList<GameSaveFileSummary> saveFiles)
	{
		Refresh(saveFiles);
		gameObject.SetActive(true);
	}

	public void Close()
	{
		gameObject.SetActive(false);
	}

	private void Refresh(IReadOnlyList<GameSaveFileSummary> saveFiles)
	{
		ClearEntries();
		ClearSelection();

		if (saveFiles == null || saveFiles.Count == 0)
		{
			SetMessage("No save files were found.");
			return;
		}

		SetMessage("Select a save file.");

		foreach (GameSaveFileSummary summary in saveFiles)
			AddEntry(summary);

		Select(saveFiles[0]);
	}

	private void AddEntry(GameSaveFileSummary summary)
	{
		if (saveEntryButtonPrefab == null || saveListRoot == null)
			return;

		Button entryButton = Instantiate(saveEntryButtonPrefab, saveListRoot);
		entryButton.gameObject.SetActive(true);
		entryButton.name = $"{summary.SaveName}_Entry";
		entryButton.onClick.RemoveAllListeners();
		entryButton.onClick.AddListener(() => Select(summary));

		TMP_Text label = entryButton.GetComponentInChildren<TMP_Text>(true);
		if (label != null)
			label.text = BuildEntryText(summary);

		SaveEntryView entry = new(summary, entryButton, label);
		entries.Add(entry);
		entry.SetSelected(false);
	}

	private void Select(GameSaveFileSummary summary)
	{
		selectedSummary = summary;

		foreach (SaveEntryView entry in entries)
			entry.SetSelected(entry.Summary.FilePath == summary.FilePath);

		if (selectedSaveTitleText != null)
			selectedSaveTitleText.text = summary.SaveName;

		if (selectedSaveInfoText != null)
			selectedSaveInfoText.text = BuildDetailText(summary);

		if (loadButton != null)
			loadButton.interactable = summary.IsLoadable;
	}

	private void ClearSelection()
	{
		selectedSummary = null;

		if (selectedSaveTitleText != null)
			selectedSaveTitleText.text = "No save selected";

		if (selectedSaveInfoText != null)
			selectedSaveInfoText.text = "Select a save file from the list.";

		if (loadButton != null)
			loadButton.interactable = false;
	}

	private void ClearEntries()
	{
		if (saveListRoot == null)
			return;

		for (int i = saveListRoot.childCount - 1; i >= 0; --i)
		{
			Transform child = saveListRoot.GetChild(i);
			if (saveEntryButtonPrefab != null && child == saveEntryButtonPrefab.transform)
			{
				child.gameObject.SetActive(false);
				continue;
			}

			Destroy(child.gameObject);
		}

		entries.Clear();
	}

	private void HandleLoadClicked()
	{
		if (selectedSummary.HasValue == false || selectedSummary.Value.IsLoadable == false)
			return;

		loadRequested?.Invoke(selectedSummary.Value);
	}

	private void SetMessage(string message)
	{
		if (messageText != null)
			messageText.text = message;
	}

	private static string BuildEntryText(GameSaveFileSummary summary)
	{
		if (summary.IsLoadable == false)
			return $"{summary.SaveName}\n{summary.StatusText}";

		return $"{summary.SaveName}\n{summary.SavedAtText}";
	}

	private static string BuildDetailText(GameSaveFileSummary summary)
	{
		if (summary.IsLoadable == false)
			return $"Status: {summary.StatusText}\nPath: {summary.FilePath}";

		return
			$"Version: {summary.Version}\n" +
			$"Saved At: {summary.SavedAtText}\n" +
			$"Money: ${summary.Money:N0}\n" +
			$"Reputation: {summary.Reputation:F1}\n" +
			$"Status: {summary.StatusText}\n" +
			$"Path: {summary.FilePath}";
	}

	private sealed class SaveEntryView
	{
		private static readonly Color SelectedButtonColor = new(0.25f, 0.39f, 0.62f, 1f);
		private static readonly Color SelectedTextColor = Color.white;

		private readonly Button button;
		private readonly TMP_Text label;
		private readonly Color defaultButtonColor;
		private readonly Color defaultTextColor;

		public SaveEntryView(GameSaveFileSummary summary, Button button, TMP_Text label)
		{
			Summary = summary;
			this.button = button;
			this.label = label;
			defaultButtonColor = button != null && button.image != null ? button.image.color : Color.white;
			defaultTextColor = label != null ? label.color : Color.white;
		}

		public GameSaveFileSummary Summary { get; }

		public void SetSelected(bool selected)
		{
			if (button != null && button.image != null)
				button.image.color = selected ? SelectedButtonColor : defaultButtonColor;

			if (label != null)
				label.color = selected ? SelectedTextColor : defaultTextColor;
		}
	}
}
