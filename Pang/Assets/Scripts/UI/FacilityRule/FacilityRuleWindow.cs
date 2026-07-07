using Assets.Scripts.UI;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class FacilityRuleWindow : MonoBehaviour
{
	[SerializeField] private UIWindow window = null;
	[SerializeField] private FacilityRuleWindowView view = null;
	[SerializeField] private FacilityRuleEditWindow editWindow = null;
	[SerializeField] private FacilityRuleFacilityListWindow facilityListWindow = null;
	[SerializeField] private Button controlButton = null;
	[SerializeField] private TMP_Text controlButtonLabel = null;
	[SerializeField] private string windowTitle = "Facility Rules";
	[SerializeField] private string controlButtonText = "R";

	private bool initialized;
	private bool applyModeActive;
	private uint applyingPresetId;

	private FacilityRuleManager RuleManager => GameContext.HasInstance ? GameContext.Instance.FacilityRuleMgr : null;
	private InteractionContext Interaction => GameContext.HasInstance ? GameContext.Instance.InteractionCtx : null;

	private void Awake()
	{
		EnsureInitialized();
	}

	private void OnEnable()
	{
		EnsureInitialized();
		SubscribeRuleManager();
		Refresh();
	}

	private void Update()
	{
		if (applyModeActive && Input.GetMouseButtonDown(1))
			EndApplyMode();
	}

	private void OnDestroy()
	{
		UnsubscribeRuleManager();

		if (Interaction != null)
			Interaction.OnItemSelected -= HandleItemSelected;

		if (window != null)
		{
			window.Opened -= HandleWindowOpened;
			window.Closed -= HandleWindowClosed;
		}

		if (controlButton != null)
			controlButton.onClick.RemoveListener(ToggleWindow);
	}

	public void ToggleWindow()
	{
		EnsureInitialized();
		if (window == null)
			return;

		if (gameObject.activeSelf == false || window.IsOpen == false)
		{
			EnsureHostActive();
			Refresh();
			window.Open();
			return;
		}

		window.Close();
	}

	public void Open()
	{
		EnsureInitialized();
		EnsureHostActive();
		Refresh();
		window?.Open();
	}

	public void Close()
	{
		EnsureInitialized();
		window?.Close();
	}

	public void Refresh()
	{
		EnsureInitialized();
		RefreshRows();
		UpdateStatus();
	}

	private void EnsureInitialized()
	{
		if (initialized)
			return;

		window ??= GetComponent<UIWindow>();
		window ??= GetComponentInChildren<UIWindow>(true);
		view ??= GetComponentInChildren<FacilityRuleWindowView>(true);
		editWindow ??= FindFirstObjectByType<FacilityRuleEditWindow>(FindObjectsInactive.Include);
		facilityListWindow ??= FindFirstObjectByType<FacilityRuleFacilityListWindow>(FindObjectsInactive.Include);

		if (window == null || view == null)
			return;

		window.SetTitle(windowTitle);
		window.Opened -= HandleWindowOpened;
		window.Closed -= HandleWindowClosed;
		window.Opened += HandleWindowOpened;
		window.Closed += HandleWindowClosed;

		view.CreatePresetButton?.Configure("Create Preset", HandleCreatePresetClicked);
		view.CancelApplyModeButton?.Configure("Cancel Apply", EndApplyMode);
		if (view.CancelApplyModeButton?.Button != null)
			view.CancelApplyModeButton.Button.interactable = false;

		BindControlButton();
		window.Close();
		initialized = true;
	}

	private void BindControlButton()
	{
		if (controlButtonLabel != null)
			controlButtonLabel.text = controlButtonText;

		if (controlButton == null)
			return;

		controlButton.onClick.RemoveListener(ToggleWindow);
		controlButton.onClick.AddListener(ToggleWindow);
	}

	private void EnsureHostActive()
	{
		if (gameObject.activeSelf == false)
			gameObject.SetActive(true);
	}

	private void SubscribeRuleManager()
	{
		FacilityRuleManager manager = RuleManager;
		if (manager == null)
			return;

		manager.OnPresetCreated -= HandlePresetChanged;
		manager.OnPresetChanged -= HandlePresetChanged;
		manager.OnPresetDeleted -= HandlePresetDeleted;
		manager.OnFacilityRulePresetApplied -= HandleFacilityPresetApplied;
		manager.OnPresetsRebuilt -= Refresh;

		manager.OnPresetCreated += HandlePresetChanged;
		manager.OnPresetChanged += HandlePresetChanged;
		manager.OnPresetDeleted += HandlePresetDeleted;
		manager.OnFacilityRulePresetApplied += HandleFacilityPresetApplied;
		manager.OnPresetsRebuilt += Refresh;
	}

	private void UnsubscribeRuleManager()
	{
		FacilityRuleManager manager = RuleManager;
		if (manager == null)
			return;

		manager.OnPresetCreated -= HandlePresetChanged;
		manager.OnPresetChanged -= HandlePresetChanged;
		manager.OnPresetDeleted -= HandlePresetDeleted;
		manager.OnFacilityRulePresetApplied -= HandleFacilityPresetApplied;
		manager.OnPresetsRebuilt -= Refresh;
	}

	private void HandleWindowOpened()
	{
		SubscribeRuleManager();
		Refresh();
	}

	private void HandleWindowClosed()
	{
		EndApplyMode();
	}

	private void HandlePresetChanged(FacilityRulePreset preset)
	{
		Refresh();
	}

	private void HandlePresetDeleted(uint presetId)
	{
		if (applyingPresetId == presetId)
			EndApplyMode();

		Refresh();
	}

	private void HandleFacilityPresetApplied(IFacility facility, uint previousPresetId, uint nextPresetId)
	{
		Refresh();
	}

	private void HandleCreatePresetClicked()
	{
		EnsureEditWindow();
		editWindow?.OpenCreate();
	}

	private void HandleEditPresetRequested(uint presetId)
	{
		if (RuleManager == null || RuleManager.TryGetPreset(presetId, out FacilityRulePreset preset) == false)
			return;

		EnsureEditWindow();
		editWindow?.OpenEdit(preset);
	}

	private void HandleListPresetRequested(uint presetId)
	{
		if (RuleManager == null || RuleManager.TryGetPreset(presetId, out FacilityRulePreset preset) == false)
			return;

		EnsureFacilityListWindow();
		facilityListWindow?.OpenForPreset(preset);
	}

	private void HandleApplyPresetRequested(uint presetId)
	{
		if (RuleManager == null)
			return;

		if (presetId != FacilityRuleManager.NoRulePresetId && RuleManager.TryGetPreset(presetId, out _) == false)
			return;

		BeginApplyMode(presetId);
	}

	private void BeginApplyMode(uint presetId)
	{
		if (Interaction == null)
			return;

		if (applyModeActive && applyingPresetId == presetId)
		{
			UpdateStatus();
			return;
		}

		EndApplyMode();
		Interaction.ExitBuildingMode();
		applyingPresetId = presetId;
		applyModeActive = true;
		Interaction.OnItemSelected -= HandleItemSelected;
		Interaction.OnItemSelected += HandleItemSelected;
		UpdateStatus();
	}

	private void EndApplyMode()
	{
		if (applyModeActive == false)
			return;

		if (Interaction != null)
			Interaction.OnItemSelected -= HandleItemSelected;

		applyModeActive = false;
		applyingPresetId = FacilityRuleManager.NoRulePresetId;
		UpdateStatus();
	}

	private void HandleItemSelected(GameObject selectedObject)
	{
		if (applyModeActive == false || selectedObject == null)
			return;

		if (TryGetFacility(selectedObject, out IFacility facility) == false)
		{
			SetStatus("Select a Facility. Right click to cancel.");
			return;
		}

		if (RuleManager == null || RuleManager.ApplyPreset(facility, applyingPresetId) == false)
		{
			SetStatus("Failed to apply preset.");
			return;
		}

		string facilityName = selectedObject.name;
		string presetName = GetApplyingPresetName();
		SetStatus($"Applied {presetName} to {facilityName}. Right click to finish.");
	}

	private void RefreshRows()
	{
		FacilityRulePresetRowView[] rows = view != null ? view.PresetRows : null;
		if (rows == null)
			return;

		FacilityRuleManager manager = RuleManager;
		IReadOnlyList<FacilityRulePreset> presets = manager != null ? manager.Presets : null;
		for (int i = 0; i < rows.Length; ++i)
		{
			FacilityRulePresetRowView row = rows[i];
			if (row == null)
				continue;

			if (i == 0 && manager != null)
			{
				row.gameObject.SetActive(true);
				row.ConfigureNoRule(
					manager.GetNoRuleFacilityCount(),
					HandleApplyPresetRequested);
				continue;
			}

			int presetIndex = i - 1;
			bool hasPreset = presets != null && presetIndex >= 0 && presetIndex < presets.Count && presets[presetIndex] != null;
			row.gameObject.SetActive(hasPreset);
			if (hasPreset == false)
			{
				row.Clear();
				continue;
			}

			FacilityRulePreset preset = presets[presetIndex];
			row.Configure(
				preset,
				manager.GetAppliedFacilityCount(preset.Id),
				HandleListPresetRequested,
				HandleEditPresetRequested,
				HandleApplyPresetRequested);
		}
	}

	private void UpdateStatus()
	{
		if (RuleManager == null)
		{
			SetStatus("FacilityRuleManager is unavailable.");
			return;
		}

		if (applyModeActive)
		{
			string presetName = GetApplyingPresetName();
			SetStatus($"Applying {presetName}. Left click a Facility. Right click to cancel.");
		}
		else
		{
			SetStatus("Create, edit, or apply a Facility Rule preset.");
		}

		if (view?.CancelApplyModeButton?.Button != null)
			view.CancelApplyModeButton.Button.interactable = applyModeActive;
	}

	private void SetStatus(string message)
	{
		if (view?.StatusRow?.Text != null)
			view.StatusRow.Text.text = message;
	}

	private void EnsureEditWindow()
	{
		if (editWindow == null)
			editWindow = FindFirstObjectByType<FacilityRuleEditWindow>(FindObjectsInactive.Include);
	}

	private void EnsureFacilityListWindow()
	{
		if (facilityListWindow == null)
			facilityListWindow = FindFirstObjectByType<FacilityRuleFacilityListWindow>(FindObjectsInactive.Include);
	}

	private string GetApplyingPresetName()
	{
		if (applyingPresetId == FacilityRuleManager.NoRulePresetId)
			return "No Rule";

		return RuleManager != null && RuleManager.TryGetPreset(applyingPresetId, out FacilityRulePreset preset)
			? preset.DisplayName
			: applyingPresetId.ToString();
	}

	private static bool TryGetFacility(GameObject selectedObject, out IFacility facility)
	{
		facility = null;
		if (selectedObject == null)
			return false;

		Component[] components = selectedObject.GetComponents<Component>();
		for (int i = 0; i < components.Length; ++i)
		{
			if (components[i] is IFacility found)
			{
				facility = found;
				return true;
			}
		}

		return false;
	}
}
