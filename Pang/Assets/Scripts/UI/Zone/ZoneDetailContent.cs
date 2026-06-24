using System;
using System.Collections.Generic;
using System.Text;
using Assets.Scripts.UI;
using UnityEngine;
using UnityEngine.UI;

public class ZoneDetailContent : DetailContent<ZoneSelectionProxy>
{
	protected override bool UseDefaultTabs => false;

	private enum ZoneDetailTab
	{
		Info,
		Rules,
		Action,
	}

	private static readonly ItemTag[] ItemTagOptions =
	{
		ItemTag.Fragile,
		ItemTag.Food,
		ItemTag.Danger,
		ItemTag.Electric,
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

	private const int DefaultPriorityOptionMax = 10;

	[SerializeField] private RectTransform infoTabRoot = null;
	[SerializeField] private RectTransform rulesTabRoot = null;
	[SerializeField] private RectTransform actionTabRoot = null;
	[SerializeField] private ZoneDetailLayoutView layoutView = null;
	[SerializeField] private ZoneRuleEditorView ruleEditorView = null;
	[SerializeField] private TextButtonView deleteZoneButton = null;

	private SelectionUIMaster selectionUIMaster;
	private UIWindow window;
	private bool rulesUiBound;
	private bool suppressRuleEvents;
	private string lastFacilityListSignature;
	private int currentPriorityOptionMax = -1;

	protected override void LinkData()
	{
		EnsureWindow();
		BindRuleEditor();
		BindActionTab();
		SetupTabs();
		SetTab((int)ZoneDetailTab.Info);
		UpdateData();
	}

	protected override void UpdateData()
	{
		ZoneArea zone = GetZone();
		if (zone == null)
			return;

		UpdateInfoTab(zone);
		UpdateRulesTab(zone);
	}

	private void EnsureWindow()
	{
		if (window == null)
			window = GetComponentInParent<UIWindow>(true);
	}

	private void SetupTabs()
	{
		if (window == null)
			return;

		window.ClearTabs();
		window.AddTab("Info", SetTab);
		window.AddTab("Rules", SetTab);
		window.AddTab("Action", SetTab);
		window.UpdateTabVisuals(0);
	}

	private void SetTab(int tabIndex)
	{
		if (infoTabRoot != null)
			infoTabRoot.gameObject.SetActive(tabIndex == (int)ZoneDetailTab.Info);
		if (rulesTabRoot != null)
			rulesTabRoot.gameObject.SetActive(tabIndex == (int)ZoneDetailTab.Rules);
		if (actionTabRoot != null)
			actionTabRoot.gameObject.SetActive(tabIndex == (int)ZoneDetailTab.Action);

		window?.UpdateTabVisuals(tabIndex);
	}

	private void BindRuleEditor()
	{
		if (ruleEditorView == null || rulesUiBound)
			return;

		BindDropdown(ruleEditorView.PriorityDropdownRow, HandlePriorityChanged);
		BindDropdown(ruleEditorView.WorkerKindDropdownRow, HandleWorkerKindChanged);
		SetDropdownOptions(ruleEditorView.WorkerKindDropdownRow, WorkerKindOptions);

		BindToggleRows(ruleEditorView.RequiredItemTagToggles, ItemTagOptions, HandleRequiredItemTagChanged);
		BindToggleRows(ruleEditorView.ForbiddenItemTagToggles, ItemTagOptions, HandleForbiddenItemTagChanged);
		BindToggleRows(ruleEditorView.RequiredWorkerAbilityToggles, WorkerAbilityOptions, HandleRequiredWorkerAbilityChanged);
		BindToggleRows(ruleEditorView.RequiredHumanTypeToggles, HumanTypeOptions, HandleRequiredHumanTypeChanged);
		BindToggleRows(ruleEditorView.ForbiddenHumanTypeToggles, HumanTypeOptions, HandleForbiddenHumanTypeChanged);
		BindToggleRows(ruleEditorView.RequiredRobotTypeToggles, RobotTypeOptions, HandleRequiredRobotTypeChanged);
		BindToggleRows(ruleEditorView.ForbiddenRobotTypeToggles, RobotTypeOptions, HandleForbiddenRobotTypeChanged);

		ruleEditorView.ClearWhiteListButton?.Configure("Clear White List", HandleClearWhiteListClicked);
		ruleEditorView.ClearBlackListButton?.Configure("Clear Black List", HandleClearBlackListClicked);
		ruleEditorView.ResetRuleButton?.Configure("Reset Entire Rule", HandleResetRuleClicked);

		rulesUiBound = true;
	}

	private void BindActionTab()
	{
		deleteZoneButton?.Configure("Delete Zone", () => provider?.DeleteObject());
	}

	private void UpdateInfoTab(ZoneArea zone)
	{
		if (layoutView == null)
			return;

		if (layoutView.NameText != null)
			layoutView.NameText.text = zone.DisplayName;
		if (layoutView.TypeText != null)
			layoutView.TypeText.text = zone.Type.ToString();
		if (layoutView.BoundsText != null)
		{
			RectInt bounds = zone.Bounds;
			layoutView.BoundsText.text = $"Bounds: {bounds.width}x{bounds.height} @ {bounds.xMin}, {bounds.yMin}  Floor: {zone.Floor}";
		}
		if (layoutView.FacilitiesHeaderText != null)
			layoutView.FacilitiesHeaderText.text = "Facilities";
		if (layoutView.FacilitiesPlaceholderText != null)
		{
			layoutView.FacilitiesPlaceholderText.text = BuildFacilitiesPlaceholder(zone);
		}

		RefreshFacilityRowsIfNeeded(zone);
	}

	private void UpdateRulesTab(ZoneArea zone)
	{
		if (ruleEditorView == null)
			return;

		ZoneRule rule = zone.Rule ?? new ZoneRule();
		ZoneItemRule itemRule = rule.ItemRule ?? new ZoneItemRule();
		ZoneWorkerRule workerRule = rule.WorkerRule ?? new ZoneWorkerRule();

		suppressRuleEvents = true;
		try
		{
			RefreshPriorityDropdown(rule.Priority);
			SetDropdownIndex(ruleEditorView.WorkerKindDropdownRow, WorkerKindOptions, workerRule.RequiredWorkerKind);

			ApplyFlagToggles(ruleEditorView.RequiredItemTagToggles, ItemTagOptions, itemRule.RequiredItemTags);
			ApplyFlagToggles(ruleEditorView.ForbiddenItemTagToggles, ItemTagOptions, itemRule.ForbiddenItemTags);
			ApplyFlagToggles(ruleEditorView.RequiredWorkerAbilityToggles, WorkerAbilityOptions, workerRule.RequiredWorkerAbility);
			ApplyListToggles(ruleEditorView.RequiredHumanTypeToggles, HumanTypeOptions, workerRule.RequiredHumanTypes);
			ApplyListToggles(ruleEditorView.ForbiddenHumanTypeToggles, HumanTypeOptions, workerRule.ForbiddenHumanTypes);
			ApplyListToggles(ruleEditorView.RequiredRobotTypeToggles, RobotTypeOptions, workerRule.RequiredRobotTypes);
			ApplyListToggles(ruleEditorView.ForbiddenRobotTypeToggles, RobotTypeOptions, workerRule.ForbiddenRobotTypes);

			if (ruleEditorView.WhiteListSummaryRow?.Text != null)
				ruleEditorView.WhiteListSummaryRow.Text.text = BuildItemListSummary("White List", itemRule.WhiteList);
			if (ruleEditorView.BlackListSummaryRow?.Text != null)
				ruleEditorView.BlackListSummaryRow.Text.text = BuildItemListSummary("Black List", itemRule.BlackList);

			if (ruleEditorView.ClearWhiteListButton?.Button != null)
				ruleEditorView.ClearWhiteListButton.Button.interactable = itemRule.WhiteList != null && itemRule.WhiteList.Count > 0;
			if (ruleEditorView.ClearBlackListButton?.Button != null)
				ruleEditorView.ClearBlackListButton.Button.interactable = itemRule.BlackList != null && itemRule.BlackList.Count > 0;
		}
		finally
		{
			suppressRuleEvents = false;
		}
	}

	private void RefreshPriorityDropdown(int priority)
	{
		if (ruleEditorView?.PriorityDropdownRow?.Dropdown == null)
			return;

		int maxPriority = Mathf.Max(DefaultPriorityOptionMax, priority);
		if (currentPriorityOptionMax != maxPriority)
		{
			currentPriorityOptionMax = maxPriority;
			List<string> options = new();
			for (int i = 0; i <= currentPriorityOptionMax; ++i)
				options.Add(i.ToString());

			ruleEditorView.PriorityDropdownRow.Dropdown.ClearOptions();
			ruleEditorView.PriorityDropdownRow.Dropdown.AddOptions(options);
		}

		ruleEditorView.PriorityDropdownRow.Dropdown.SetValueWithoutNotify(Mathf.Clamp(priority, 0, currentPriorityOptionMax));
	}

	private void RefreshFacilityRowsIfNeeded(ZoneArea zone)
	{
		string signature = BuildFacilityListSignature(zone);
		if (lastFacilityListSignature == signature)
			return;

		ApplyFacilityRows(zone);
		lastFacilityListSignature = signature;
	}

	private void ApplyFacilityRows(ZoneArea zone)
	{
		if (layoutView == null)
			return;

		LabelButtonRowView[] rows = layoutView.FacilityRows;
		if (rows == null || rows.Length == 0)
			return;

		for (int i = 0; i < rows.Length; ++i)
		{
			LabelButtonRowView row = rows[i];
			if (row == null)
				continue;

			bool hasFacility = zone != null && i < zone.OccupiedFacilities.Count;
			row.gameObject.SetActive(hasFacility);
			if (hasFacility == false)
				continue;

			IFacility facility = zone.OccupiedFacilities[i];
			if (facility is not Component component || component == null)
			{
				row.gameObject.SetActive(false);
				continue;
			}

			row.name = component.name + "Row";
			if (row.LabelText != null)
				row.LabelText.text = component.name;
			row.ActionButton?.Configure("View Details", () => HandleViewFacilityDetailsClicked(component.gameObject));
			if (row.ActionButton?.Button != null)
				row.ActionButton.Button.interactable = true;
		}
	}

	private void HandleViewFacilityDetailsClicked(GameObject facilityObject)
	{
		if (facilityObject == null)
			return;

		EnsureSelectionUIMaster();
		selectionUIMaster?.OpenDetailWindow(facilityObject);
	}

	private void HandlePriorityChanged(int index)
	{
		if (suppressRuleEvents)
			return;

		ApplyRuleMutation(rule => rule.SetPriority(index));
	}

	private void HandleWorkerKindChanged(int index)
	{
		if (suppressRuleEvents || index < 0 || index >= WorkerKindOptions.Length)
			return;

		ApplyRuleMutation(rule =>
		{
			ZoneWorkerRule workerRule = new(rule.WorkerRule);
			workerRule.SetRequiredWorkerKind(WorkerKindOptions[index]);
			rule.SetWorkerRule(workerRule);
		});
	}

	private void HandleRequiredItemTagChanged(ItemTag tag, bool isOn)
	{
		if (suppressRuleEvents)
			return;

		ApplyRuleMutation(rule =>
		{
			ZoneItemRule itemRule = new(rule.ItemRule);
			itemRule.SetRequiredItemTags(isOn ? itemRule.RequiredItemTags | tag : itemRule.RequiredItemTags & ~tag);
			rule.SetItemRule(itemRule);
		});
	}

	private void HandleForbiddenItemTagChanged(ItemTag tag, bool isOn)
	{
		if (suppressRuleEvents)
			return;

		ApplyRuleMutation(rule =>
		{
			ZoneItemRule itemRule = new(rule.ItemRule);
			itemRule.SetForbiddenItemTags(isOn ? itemRule.ForbiddenItemTags | tag : itemRule.ForbiddenItemTags & ~tag);
			rule.SetItemRule(itemRule);
		});
	}

	private void HandleRequiredWorkerAbilityChanged(WorkerAbility ability, bool isOn)
	{
		if (suppressRuleEvents)
			return;

		ApplyRuleMutation(rule =>
		{
			ZoneWorkerRule workerRule = new(rule.WorkerRule);
			workerRule.SetRequiredWorkerAbility(isOn
				? workerRule.RequiredWorkerAbility | ability
				: workerRule.RequiredWorkerAbility & ~ability);
			rule.SetWorkerRule(workerRule);
		});
	}

	private void HandleRequiredHumanTypeChanged(HumanType humanType, bool isOn)
	{
		if (suppressRuleEvents)
			return;

		ApplyRuleMutation(rule =>
		{
			ZoneWorkerRule workerRule = new(rule.WorkerRule);
			List<HumanType> values = new(workerRule.RequiredHumanTypes);
			SetListValue(values, humanType, isOn);
			workerRule.SetRequiredHumanTypes(values);
			rule.SetWorkerRule(workerRule);
		});
	}

	private void HandleForbiddenHumanTypeChanged(HumanType humanType, bool isOn)
	{
		if (suppressRuleEvents)
			return;

		ApplyRuleMutation(rule =>
		{
			ZoneWorkerRule workerRule = new(rule.WorkerRule);
			List<HumanType> values = new(workerRule.ForbiddenHumanTypes);
			SetListValue(values, humanType, isOn);
			workerRule.SetForbiddenHumanTypes(values);
			rule.SetWorkerRule(workerRule);
		});
	}

	private void HandleRequiredRobotTypeChanged(RobotType robotType, bool isOn)
	{
		if (suppressRuleEvents)
			return;

		ApplyRuleMutation(rule =>
		{
			ZoneWorkerRule workerRule = new(rule.WorkerRule);
			List<RobotType> values = new(workerRule.RequiredRobotTypes);
			SetListValue(values, robotType, isOn);
			workerRule.SetRequiredRobotTypes(values);
			rule.SetWorkerRule(workerRule);
		});
	}

	private void HandleForbiddenRobotTypeChanged(RobotType robotType, bool isOn)
	{
		if (suppressRuleEvents)
			return;

		ApplyRuleMutation(rule =>
		{
			ZoneWorkerRule workerRule = new(rule.WorkerRule);
			List<RobotType> values = new(workerRule.ForbiddenRobotTypes);
			SetListValue(values, robotType, isOn);
			workerRule.SetForbiddenRobotTypes(values);
			rule.SetWorkerRule(workerRule);
		});
	}

	private void HandleClearWhiteListClicked()
	{
		ApplyRuleMutation(rule =>
		{
			ZoneItemRule itemRule = new(rule.ItemRule);
			itemRule.SetWhiteList(Array.Empty<ItemDefinition>());
			rule.SetItemRule(itemRule);
		});
	}

	private void HandleClearBlackListClicked()
	{
		ApplyRuleMutation(rule =>
		{
			ZoneItemRule itemRule = new(rule.ItemRule);
			itemRule.SetBlackList(Array.Empty<ItemDefinition>());
			rule.SetItemRule(itemRule);
		});
	}

	private void HandleResetRuleClicked()
	{
		ApplyRuleMutation(rule => rule.Clear());
	}

	private void ApplyRuleMutation(Action<ZoneRule> mutator)
	{
		if (mutator == null)
			return;

		ZoneArea zone = GetZone();
		ZoneManager zoneManager = GetZoneManager();
		if (zone == null || zoneManager == null)
			return;

		ZoneRule nextRule = new(zone.Rule);
		mutator(nextRule);
		zoneManager.SetZoneRule(zone, nextRule);
	}

	private ZoneArea GetZone()
	{
		return (provider as ZoneUIProvider)?.Target?.Zone;
	}

	private ZoneManager GetZoneManager()
	{
		return (provider as ZoneUIProvider)?.Target?.ZoneManager;
	}

	private void EnsureSelectionUIMaster()
	{
		if (selectionUIMaster == null)
			selectionUIMaster = GetComponentInParent<SelectionUIMaster>(true);

		if (selectionUIMaster == null)
			selectionUIMaster = FindFirstObjectByType<SelectionUIMaster>(FindObjectsInactive.Include);
	}

	private static void BindDropdown(DropdownRowView rowView, UnityEngine.Events.UnityAction<int> onChanged)
	{
		if (rowView?.Dropdown == null)
			return;

		rowView.Dropdown.onValueChanged.RemoveAllListeners();
		if (onChanged != null)
			rowView.Dropdown.onValueChanged.AddListener(onChanged);
	}

	private static void BindToggleRows<T>(ToggleRowView[] rows, IReadOnlyList<T> values, Action<T, bool> onChanged)
	{
		if (rows == null)
			return;

		for (int i = 0; i < rows.Length && i < values.Count; ++i)
		{
			ToggleRowView row = rows[i];
			if (row?.Toggle == null)
				continue;

			T value = values[i];
			row.Toggle.onValueChanged.RemoveAllListeners();
			row.Toggle.onValueChanged.AddListener(isOn => onChanged?.Invoke(value, isOn));
		}
	}

	private static void ApplyFlagToggles<TEnum>(ToggleRowView[] rows, IReadOnlyList<TEnum> values, TEnum currentFlags)
		where TEnum : Enum
	{
		if (rows == null)
			return;

		long currentValue = Convert.ToInt64(currentFlags);
		for (int i = 0; i < rows.Length && i < values.Count; ++i)
		{
			ToggleRowView row = rows[i];
			if (row?.Toggle == null)
				continue;

			long flagValue = Convert.ToInt64(values[i]);
			bool isOn = flagValue != 0 && (currentValue & flagValue) == flagValue;
			row.Toggle.SetIsOnWithoutNotify(isOn);
		}
	}

	private static void ApplyListToggles<T>(ToggleRowView[] rows, IReadOnlyList<T> values, IReadOnlyList<T> selectedValues)
	{
		if (rows == null)
			return;

		HashSet<T> selectedSet = selectedValues != null ? new HashSet<T>(selectedValues) : null;
		for (int i = 0; i < rows.Length && i < values.Count; ++i)
		{
			ToggleRowView row = rows[i];
			if (row?.Toggle == null)
				continue;

			row.Toggle.SetIsOnWithoutNotify(selectedSet != null && selectedSet.Contains(values[i]));
		}
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

	private string BuildFacilitiesPlaceholder(ZoneArea zone)
	{
		if (zone == null || zone.OccupiedFacilities.Count <= 0)
			return "No facilities in this zone.";

		int visibleCapacity = layoutView?.FacilityRows != null ? layoutView.FacilityRows.Length : 0;
		if (visibleCapacity > 0 && zone.OccupiedFacilities.Count > visibleCapacity)
			return $"Showing first {visibleCapacity} of {zone.OccupiedFacilities.Count} facilities.";

		return "Select a facility to inspect details.";
	}

	private static string BuildFacilityListSignature(ZoneArea zone)
	{
		if (zone == null)
			return string.Empty;

		StringBuilder builder = new();
		builder.Append(zone.RuntimeBuildingId);
		builder.Append(':');
		builder.Append(zone.DisplayName);
		builder.Append(':');
		for (int i = 0; i < zone.OccupiedFacilities.Count; ++i)
		{
			IFacility facility = zone.OccupiedFacilities[i];
			if (facility is not Component component || component == null)
				continue;

			if (builder.Length > 0)
				builder.Append('\n');

			builder.Append(component.name);
		}

		return builder.ToString();
	}
}
