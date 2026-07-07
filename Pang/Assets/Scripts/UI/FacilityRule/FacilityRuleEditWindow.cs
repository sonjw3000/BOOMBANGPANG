using System;
using System.Collections.Generic;
using System.Text;
using Assets.Scripts.UI;
using UnityEngine;
using UnityEngine.UI;

public sealed class FacilityRuleEditWindow : MonoBehaviour
{
	private static readonly ItemTag[] ItemTagOptions =
	{
		ItemTag.Fragile,
		ItemTag.Food,
		ItemTag.Danger,
		ItemTag.Electric,
	};

	private static readonly ItemStatus[] ItemStatusOptions =
	{
		ItemStatus.NotDefined,
		ItemStatus.None,
		ItemStatus.Labeled,
		ItemStatus.Packed,
	};

	private static readonly WorkerAbility[] WorkerAbilityOptions =
	{
		WorkerAbility.CarryBox,
		WorkerAbility.PickingStoring,
		WorkerAbility.Packing,
		WorkerAbility.Labeling,
		WorkerAbility.CargoHandling,
	};

	private static readonly WorkerKind[] WorkerKindOptions =
	{
		WorkerKind.None,
		WorkerKind.Human,
		WorkerKind.Robot,
	};

	private static readonly HumanType[] HumanTypeOptions =
	{
		HumanType.FullTime,
		HumanType.PartTime,
		HumanType.Illegal,
	};

	private static readonly RobotType[] RobotTypeOptions =
	{
		RobotType.Transfer,
	};

	private static readonly OrderDestination[] OrderDestinationOptions =
	{
		OrderDestination.None,
		OrderDestination.Mars,
		OrderDestination.Titan,
	};

	private const int DefaultPriorityOptionMax = 10;

	[SerializeField] private UIWindow window = null;
	[SerializeField] private FacilityRuleEditWindowView view = null;
	[SerializeField] private string createWindowTitle = "Create Facility Rule";
	[SerializeField] private string editWindowTitle = "Edit Facility Rule";

	private bool initialized;
	private bool rulesUiBound;
	private bool suppressRuleEvents;
	private uint editingPresetId;
	private int currentPriorityOptionMax = -1;
	private FacilityRule workingRule = new();
	private Color workingColor = Color.white;

	private FacilityRuleManager RuleManager => GameContext.HasInstance ? GameContext.Instance.FacilityRuleMgr : null;
	private FacilityRuleEditorView RuleEditorView => view != null ? view.RuleEditorView : null;

	private void Awake()
	{
		EnsureInitialized();
	}

	private void OnDestroy()
	{
		if (window != null)
			window.Closed -= HandleWindowClosed;

		UnbindColorSliders();
	}

	public void OpenCreate()
	{
		EnsureInitialized();
		if (window == null)
			return;

		editingPresetId = FacilityRuleManager.NoRulePresetId;
		workingRule = new FacilityRule();
		workingColor = Color.white;
		SetNameWithoutNotify(BuildDefaultPresetName());
		ApplyColorWithoutNotify(workingColor);
		window.SetTitle(createWindowTitle);
		RefreshRuleEditor();
		EnsureHostActive();
		window.Open();
	}

	public void OpenEdit(FacilityRulePreset preset)
	{
		if (preset == null)
			return;

		EnsureInitialized();
		if (window == null)
			return;

		editingPresetId = preset.Id;
		workingRule = new FacilityRule(preset.Rule);
		workingColor = preset.Color;
		SetNameWithoutNotify(preset.DisplayName);
		ApplyColorWithoutNotify(workingColor);
		window.SetTitle(editWindowTitle);
		RefreshRuleEditor();
		EnsureHostActive();
		window.Open();
	}

	private void EnsureInitialized()
	{
		if (initialized)
			return;

		window ??= GetComponent<UIWindow>();
		window ??= GetComponentInChildren<UIWindow>(true);
		view ??= GetComponentInChildren<FacilityRuleEditWindowView>(true);

		if (window == null || view == null)
			return;

		BindRuleEditor();
		BindColorSliders();
		view.SaveButton?.Configure("Save", HandleSaveClicked);
		view.CancelButton?.Configure("Cancel", HandleCancelClicked);
		window.Closed -= HandleWindowClosed;
		window.Closed += HandleWindowClosed;
		window.Close();
		initialized = true;
	}

	private void EnsureHostActive()
	{
		if (gameObject.activeSelf == false)
			gameObject.SetActive(true);
	}

	private void HandleSaveClicked()
	{
		FacilityRuleManager manager = RuleManager;
		if (manager == null)
			return;

		string displayName = view.PresetNameInput != null ? view.PresetNameInput.text : string.Empty;
		if (editingPresetId == FacilityRuleManager.NoRulePresetId)
		{
			manager.CreatePreset(displayName, workingRule, workingColor);
		}
		else
		{
			manager.RenamePreset(editingPresetId, displayName);
			manager.SetPresetColor(editingPresetId, workingColor);
			manager.SetPresetRule(editingPresetId, workingRule);
		}

		window?.Close();
	}

	private void HandleCancelClicked()
	{
		window?.Close();
	}

	private void HandleWindowClosed()
	{
		suppressRuleEvents = false;
	}

	private void BindRuleEditor()
	{
		FacilityRuleEditorView editorView = RuleEditorView;
		if (editorView == null || rulesUiBound)
			return;

		BindDropdown(editorView.PriorityDropdownRow, HandlePriorityChanged);
		BindDropdown(editorView.WorkerKindDropdownRow, HandleWorkerKindChanged);
		BindDropdown(editorView.RequiredItemStatusDropdownRow, HandleRequiredItemStatusChanged);
		SetDropdownOptions(editorView.WorkerKindDropdownRow, WorkerKindOptions);
		SetDropdownOptions(editorView.RequiredItemStatusDropdownRow, ItemStatusOptions);

		BindMultiSelectDropdown(editorView.RequiredItemTagsDropdown, ItemTagOptions, HandleRequiredItemTagChanged);
		BindMultiSelectDropdown(editorView.ForbiddenItemTagsDropdown, ItemTagOptions, HandleForbiddenItemTagChanged);
		BindMultiSelectDropdown(editorView.RequiredWorkerAbilitiesDropdown, WorkerAbilityOptions, HandleRequiredWorkerAbilityChanged);
		BindMultiSelectDropdown(editorView.RequiredHumanTypesDropdown, HumanTypeOptions, HandleRequiredHumanTypeChanged);
		BindMultiSelectDropdown(editorView.ForbiddenHumanTypesDropdown, HumanTypeOptions, HandleForbiddenHumanTypeChanged);
		BindMultiSelectDropdown(editorView.RequiredRobotTypesDropdown, RobotTypeOptions, HandleRequiredRobotTypeChanged);
		BindMultiSelectDropdown(editorView.ForbiddenRobotTypesDropdown, RobotTypeOptions, HandleForbiddenRobotTypeChanged);
		BindMultiSelectDropdown(editorView.RequiredDestinationsDropdown, OrderDestinationOptions, HandleRequiredDestinationChanged);

		editorView.ClearWhiteListButton?.Configure("Clear White List", HandleClearWhiteListClicked);
		editorView.ClearBlackListButton?.Configure("Clear Black List", HandleClearBlackListClicked);
		editorView.ResetRuleButton?.Configure("Reset Entire Rule", HandleResetRuleClicked);

		rulesUiBound = true;
	}

	private void BindColorSliders()
	{
		UnbindColorSliders();

		if (view.RedSlider != null)
			view.RedSlider.onValueChanged.AddListener(HandleColorSliderChanged);
		if (view.GreenSlider != null)
			view.GreenSlider.onValueChanged.AddListener(HandleColorSliderChanged);
		if (view.BlueSlider != null)
			view.BlueSlider.onValueChanged.AddListener(HandleColorSliderChanged);
	}

	private void UnbindColorSliders()
	{
		if (view == null)
			return;

		if (view.RedSlider != null)
			view.RedSlider.onValueChanged.RemoveListener(HandleColorSliderChanged);
		if (view.GreenSlider != null)
			view.GreenSlider.onValueChanged.RemoveListener(HandleColorSliderChanged);
		if (view.BlueSlider != null)
			view.BlueSlider.onValueChanged.RemoveListener(HandleColorSliderChanged);
	}

	private void HandleColorSliderChanged(float value)
	{
		float r = view.RedSlider != null ? view.RedSlider.value : workingColor.r;
		float g = view.GreenSlider != null ? view.GreenSlider.value : workingColor.g;
		float b = view.BlueSlider != null ? view.BlueSlider.value : workingColor.b;
		workingColor = new Color(r, g, b, 1f);
		if (view.ColorPreview != null)
			view.ColorPreview.color = workingColor;
	}

	private void ApplyColorWithoutNotify(Color color)
	{
		workingColor = color;
		if (view == null)
			return;

		if (view.RedSlider != null)
			view.RedSlider.SetValueWithoutNotify(color.r);
		if (view.GreenSlider != null)
			view.GreenSlider.SetValueWithoutNotify(color.g);
		if (view.BlueSlider != null)
			view.BlueSlider.SetValueWithoutNotify(color.b);
		if (view.ColorPreview != null)
			view.ColorPreview.color = color;
	}

	private void SetNameWithoutNotify(string displayName)
	{
		if (view?.PresetNameInput != null)
			view.PresetNameInput.SetTextWithoutNotify(displayName);
	}

	private void RefreshRuleEditor()
	{
		FacilityRuleEditorView editorView = RuleEditorView;
		if (editorView == null)
			return;

		FacilityItemRule itemRule = workingRule.ItemRule ?? new FacilityItemRule();
		FacilityWorkerRule workerRule = workingRule.WorkerRule ?? new FacilityWorkerRule();
		FacilityManifestRule manifestRule = workingRule.ManifestRule ?? new FacilityManifestRule();

		suppressRuleEvents = true;
		try
		{
			RefreshPriorityDropdown(workingRule.Priority);
			SetDropdownIndex(editorView.WorkerKindDropdownRow, WorkerKindOptions, workerRule.RequiredWorkerKind);
			SetDropdownIndex(editorView.RequiredItemStatusDropdownRow, ItemStatusOptions, itemRule.RequiredItemStatus);
			ApplyWorkerKindVisibility(workerRule.RequiredWorkerKind);

			ApplyFlagDropdown(editorView.RequiredItemTagsDropdown, ItemTagOptions, itemRule.RequiredItemTags);
			ApplyFlagDropdown(editorView.ForbiddenItemTagsDropdown, ItemTagOptions, itemRule.ForbiddenItemTags);
			ApplyFlagDropdown(editorView.RequiredWorkerAbilitiesDropdown, WorkerAbilityOptions, workerRule.RequiredWorkerAbility);
			ApplyListDropdown(editorView.RequiredHumanTypesDropdown, HumanTypeOptions, workerRule.RequiredHumanTypes);
			ApplyListDropdown(editorView.ForbiddenHumanTypesDropdown, HumanTypeOptions, workerRule.ForbiddenHumanTypes);
			ApplyListDropdown(editorView.RequiredRobotTypesDropdown, RobotTypeOptions, workerRule.RequiredRobotTypes);
			ApplyListDropdown(editorView.ForbiddenRobotTypesDropdown, RobotTypeOptions, workerRule.ForbiddenRobotTypes);
			ApplyListDropdown(editorView.RequiredDestinationsDropdown, OrderDestinationOptions, manifestRule.RequiredDestinations);

			if (editorView.WhiteListSummaryRow?.Text != null)
				editorView.WhiteListSummaryRow.Text.text = BuildItemListSummary("White List", itemRule.WhiteList);
			if (editorView.BlackListSummaryRow?.Text != null)
				editorView.BlackListSummaryRow.Text.text = BuildItemListSummary("Black List", itemRule.BlackList);

			if (editorView.ClearWhiteListButton?.Button != null)
				editorView.ClearWhiteListButton.Button.interactable = itemRule.WhiteList != null && itemRule.WhiteList.Count > 0;
			if (editorView.ClearBlackListButton?.Button != null)
				editorView.ClearBlackListButton.Button.interactable = itemRule.BlackList != null && itemRule.BlackList.Count > 0;
		}
		finally
		{
			suppressRuleEvents = false;
		}
	}

	private void RefreshPriorityDropdown(int priority)
	{
		FacilityRuleEditorView editorView = RuleEditorView;
		if (editorView?.PriorityDropdownRow?.Dropdown == null)
			return;

		int maxPriority = Mathf.Max(DefaultPriorityOptionMax, priority);
		if (currentPriorityOptionMax != maxPriority)
		{
			currentPriorityOptionMax = maxPriority;
			List<string> options = new();
			for (int i = 0; i <= currentPriorityOptionMax; ++i)
				options.Add(i.ToString());

			editorView.PriorityDropdownRow.Dropdown.ClearOptions();
			editorView.PriorityDropdownRow.Dropdown.AddOptions(options);
		}

		editorView.PriorityDropdownRow.Dropdown.SetValueWithoutNotify(Mathf.Clamp(priority, 0, currentPriorityOptionMax));
	}

	private void HandlePriorityChanged(int index)
	{
		if (suppressRuleEvents)
			return;

		workingRule.SetPriority(index);
		RefreshRuleEditor();
	}

	private void HandleWorkerKindChanged(int index)
	{
		if (suppressRuleEvents || index < 0 || index >= WorkerKindOptions.Length)
			return;

		WorkerKind nextWorkerKind = WorkerKindOptions[index];
		FacilityWorkerRule workerRule = new(workingRule.WorkerRule);
		workerRule.SetRequiredWorkerKind(nextWorkerKind);
		workingRule.SetWorkerRule(workerRule);
		RefreshRuleEditor();
	}

	private void HandleRequiredItemStatusChanged(int index)
	{
		if (suppressRuleEvents || index < 0 || index >= ItemStatusOptions.Length)
			return;

		FacilityItemRule itemRule = new(workingRule.ItemRule);
		itemRule.SetRequiredItemStatus(ItemStatusOptions[index]);
		workingRule.SetItemRule(itemRule);
		RefreshRuleEditor();
	}

	private void HandleRequiredItemTagChanged(ItemTag tag, bool isOn)
	{
		if (suppressRuleEvents)
			return;

		FacilityItemRule itemRule = new(workingRule.ItemRule);
		itemRule.SetRequiredItemTags(isOn ? itemRule.RequiredItemTags | tag : itemRule.RequiredItemTags & ~tag);
		workingRule.SetItemRule(itemRule);
		RefreshRuleEditor();
	}

	private void HandleForbiddenItemTagChanged(ItemTag tag, bool isOn)
	{
		if (suppressRuleEvents)
			return;

		FacilityItemRule itemRule = new(workingRule.ItemRule);
		itemRule.SetForbiddenItemTags(isOn ? itemRule.ForbiddenItemTags | tag : itemRule.ForbiddenItemTags & ~tag);
		workingRule.SetItemRule(itemRule);
		RefreshRuleEditor();
	}

	private void HandleRequiredWorkerAbilityChanged(WorkerAbility ability, bool isOn)
	{
		if (suppressRuleEvents)
			return;

		FacilityWorkerRule workerRule = new(workingRule.WorkerRule);
		workerRule.SetRequiredWorkerAbility(isOn
			? workerRule.RequiredWorkerAbility | ability
			: workerRule.RequiredWorkerAbility & ~ability);
		workingRule.SetWorkerRule(workerRule);
		RefreshRuleEditor();
	}

	private void HandleRequiredHumanTypeChanged(HumanType humanType, bool isOn)
	{
		if (suppressRuleEvents)
			return;

		FacilityWorkerRule workerRule = new(workingRule.WorkerRule);
		List<HumanType> values = new(workerRule.RequiredHumanTypes);
		SetListValue(values, humanType, isOn);
		workerRule.SetRequiredHumanTypes(values);
		workingRule.SetWorkerRule(workerRule);
		RefreshRuleEditor();
	}

	private void HandleForbiddenHumanTypeChanged(HumanType humanType, bool isOn)
	{
		if (suppressRuleEvents)
			return;

		FacilityWorkerRule workerRule = new(workingRule.WorkerRule);
		List<HumanType> values = new(workerRule.ForbiddenHumanTypes);
		SetListValue(values, humanType, isOn);
		workerRule.SetForbiddenHumanTypes(values);
		workingRule.SetWorkerRule(workerRule);
		RefreshRuleEditor();
	}

	private void HandleRequiredRobotTypeChanged(RobotType robotType, bool isOn)
	{
		if (suppressRuleEvents)
			return;

		FacilityWorkerRule workerRule = new(workingRule.WorkerRule);
		List<RobotType> values = new(workerRule.RequiredRobotTypes);
		SetListValue(values, robotType, isOn);
		workerRule.SetRequiredRobotTypes(values);
		workingRule.SetWorkerRule(workerRule);
		RefreshRuleEditor();
	}

	private void HandleForbiddenRobotTypeChanged(RobotType robotType, bool isOn)
	{
		if (suppressRuleEvents)
			return;

		FacilityWorkerRule workerRule = new(workingRule.WorkerRule);
		List<RobotType> values = new(workerRule.ForbiddenRobotTypes);
		SetListValue(values, robotType, isOn);
		workerRule.SetForbiddenRobotTypes(values);
		workingRule.SetWorkerRule(workerRule);
		RefreshRuleEditor();
	}

	private void HandleRequiredDestinationChanged(OrderDestination destination, bool isOn)
	{
		if (suppressRuleEvents)
			return;

		FacilityManifestRule manifestRule = new(workingRule.ManifestRule);
		List<OrderDestination> values = new(manifestRule.RequiredDestinations);
		SetListValue(values, destination, isOn);
		manifestRule.SetRequiredDestinations(values);
		workingRule.SetManifestRule(manifestRule);
		RefreshRuleEditor();
	}

	private void HandleClearWhiteListClicked()
	{
		FacilityItemRule itemRule = new(workingRule.ItemRule);
		itemRule.SetWhiteList(Array.Empty<ItemDefinition>());
		workingRule.SetItemRule(itemRule);
		RefreshRuleEditor();
	}

	private void HandleClearBlackListClicked()
	{
		FacilityItemRule itemRule = new(workingRule.ItemRule);
		itemRule.SetBlackList(Array.Empty<ItemDefinition>());
		workingRule.SetItemRule(itemRule);
		RefreshRuleEditor();
	}

	private void HandleResetRuleClicked()
	{
		workingRule.Clear();
		RefreshRuleEditor();
	}

	private void ApplyWorkerKindVisibility(WorkerKind workerKind)
	{
		FacilityRuleEditorView editorView = RuleEditorView;
		if (editorView == null)
			return;

		bool showHuman = workerKind == WorkerKind.None || workerKind == WorkerKind.Human;
		bool showRobot = workerKind == WorkerKind.None || workerKind == WorkerKind.Robot;

		SetRuleDropdownVisible(editorView.RequiredHumanTypesDropdown, showHuman);
		SetRuleDropdownVisible(editorView.ForbiddenHumanTypesDropdown, showHuman);
		SetRuleDropdownVisible(editorView.RequiredRobotTypesDropdown, showRobot);
		SetRuleDropdownVisible(editorView.ForbiddenRobotTypesDropdown, showRobot);
	}

	private string BuildDefaultPresetName()
	{
		int nextIndex = RuleManager != null ? RuleManager.Presets.Count + 1 : 1;
		return $"Rule {nextIndex}";
	}

	private static void BindDropdown(DropdownRowView rowView, UnityEngine.Events.UnityAction<int> onChanged)
	{
		if (rowView?.Dropdown == null)
			return;

		rowView.Dropdown.onValueChanged.RemoveAllListeners();
		if (onChanged != null)
			rowView.Dropdown.onValueChanged.AddListener(onChanged);
	}

	private static void BindMultiSelectDropdown<T>(MultiSelectDropdownRowView dropdownView, IReadOnlyList<T> values, Action<T, bool> onChanged)
	{
		ToggleRowView[] rows = dropdownView?.OptionRows;
		if (rows == null)
			return;

		for (int i = 0; i < rows.Length; ++i)
		{
			ToggleRowView row = rows[i];
			if (row == null)
				continue;

			bool isVisible = i < values.Count;
			row.gameObject.SetActive(isVisible);
			if (isVisible == false || row.Toggle == null)
				continue;

			T value = values[i];
			if (row.LabelText != null)
				row.LabelText.text = value.ToString();

			row.Toggle.onValueChanged.RemoveAllListeners();
			row.Toggle.onValueChanged.AddListener(isOn => onChanged?.Invoke(value, isOn));
		}
	}

	private static void ApplyFlagDropdown<TEnum>(MultiSelectDropdownRowView dropdownView, IReadOnlyList<TEnum> values, TEnum currentFlags)
		where TEnum : Enum
	{
		ToggleRowView[] rows = dropdownView?.OptionRows;
		if (rows == null)
			return;

		long currentValue = Convert.ToInt64(currentFlags);
		List<string> selectedLabels = new();
		for (int i = 0; i < rows.Length && i < values.Count; ++i)
		{
			ToggleRowView row = rows[i];
			if (row?.Toggle == null)
				continue;

			long flagValue = Convert.ToInt64(values[i]);
			bool isOn = flagValue != 0 && (currentValue & flagValue) == flagValue;
			row.Toggle.SetIsOnWithoutNotify(isOn);
			if (isOn)
				selectedLabels.Add(values[i].ToString());
		}

		SetDropdownSummary(dropdownView, selectedLabels);
	}

	private static void ApplyListDropdown<T>(MultiSelectDropdownRowView dropdownView, IReadOnlyList<T> values, IReadOnlyList<T> selectedValues)
	{
		ToggleRowView[] rows = dropdownView?.OptionRows;
		if (rows == null)
			return;

		HashSet<T> selectedSet = selectedValues != null ? new HashSet<T>(selectedValues) : null;
		List<string> selectedLabels = new();
		for (int i = 0; i < rows.Length && i < values.Count; ++i)
		{
			ToggleRowView row = rows[i];
			if (row?.Toggle == null)
				continue;

			bool isSelected = selectedSet != null && selectedSet.Contains(values[i]);
			row.Toggle.SetIsOnWithoutNotify(isSelected);
			if (isSelected)
				selectedLabels.Add(values[i].ToString());
		}

		SetDropdownSummary(dropdownView, selectedLabels);
	}

	private static void SetDropdownOptions<T>(DropdownRowView rowView, IReadOnlyList<T> values)
	{
		if (rowView?.Dropdown == null)
			return;

		rowView.Dropdown.ClearOptions();
		List<string> options = new();
		for (int i = 0; i < values.Count; ++i)
			options.Add(values[i].ToString());

		rowView.Dropdown.AddOptions(options);
	}

	private static void SetDropdownIndex<T>(DropdownRowView rowView, IReadOnlyList<T> values, T currentValue)
	{
		if (rowView?.Dropdown == null)
			return;

		int selectedIndex = 0;
		EqualityComparer<T> comparer = EqualityComparer<T>.Default;
		for (int i = 0; i < values.Count; ++i)
		{
			if (comparer.Equals(values[i], currentValue))
			{
				selectedIndex = i;
				break;
			}
		}

		rowView.Dropdown.SetValueWithoutNotify(selectedIndex);
	}

	private static void SetListValue<T>(List<T> values, T value, bool isOn)
	{
		if (isOn)
		{
			if (values.Contains(value) == false)
				values.Add(value);
			return;
		}

		values.Remove(value);
	}

	private static void SetDropdownSummary(MultiSelectDropdownRowView dropdownView, IReadOnlyList<string> selectedLabels)
	{
		if (dropdownView?.SummaryText == null)
			return;

		if (selectedLabels == null || selectedLabels.Count == 0)
		{
			dropdownView.SummaryText.text = "None";
			return;
		}

		if (selectedLabels.Count <= 2)
		{
			dropdownView.SummaryText.text = string.Join(", ", selectedLabels);
			return;
		}

		dropdownView.SummaryText.text = $"{selectedLabels[0]}, {selectedLabels[1]} +{selectedLabels.Count - 2}";
	}

	private static void SetRuleDropdownVisible(MultiSelectDropdownRowView dropdownView, bool isVisible)
	{
		if (dropdownView == null)
			return;

		if (isVisible == false)
			dropdownView.Collapse();

		dropdownView.gameObject.SetActive(isVisible);
	}

	private static string BuildItemListSummary(string label, IReadOnlyList<ItemDefinition> items)
	{
		if (items == null || items.Count == 0)
			return $"{label}: none";

		StringBuilder builder = new();
		builder.Append(label);
		builder.Append(": ");
		builder.Append(items.Count);
		builder.Append(" item(s)");

		int printed = 0;
		for (int i = 0; i < items.Count && printed < 3; ++i)
		{
			ItemDefinition item = items[i];
			if (item == null)
				continue;

			builder.Append(printed == 0 ? " - " : ", ");
			builder.Append(item.name);
			printed++;
		}

		if (items.Count > printed)
			builder.Append(", ...");

		return builder.ToString();
	}
}
