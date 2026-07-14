using System;
using System.Collections.Generic;
using Assets.Scripts.UI;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public sealed class BuildingDetailContent : DetailContent<BuildingSelectionProxy>
{
	protected override bool UseDefaultTabs => false;

	[SerializeField] private BuildingDetailLayoutView layoutPrefab = null;
	[SerializeField] private DetailInfoRowView infoRowPrefab = null;
	[SerializeField] private TextRowView summaryRowPrefab = null;

	private UIWindow window;
	private BuildingDetailLayoutView layoutView;
	private readonly List<GameObject> tabRoots = new();
	private readonly List<WindowTabContentEntry> tabEntries = new();
	private readonly List<GameObject> facilitySummaryRows = new();

	private TextMeshProUGUI nameValue;
	private TextMeshProUGUI typeValue;
	private TextMeshProUGUI stateValue;
	private TextMeshProUGUI workScopeValue;
	private TextMeshProUGUI averageTemperatureValue;
	private TextMeshProUGUI cellCountValue;
	private TextMeshProUGUI facilityCountValue;
	private TextMeshProUGUI cargoPortCountValue;
	private TextMeshProUGUI thresholdValueText;
	private TextMeshProUGUI settingsStatusText;
	private TextMeshProUGUI actionStateValue;
	private Toggle overrideThresholdToggle;
	private Slider thresholdSlider;

	private SelectionUIMaster selectionUIMaster;
	private BuildingPlacementOverlayController buildingOverlayController;
	private bool uiBuilt;
	private int currentTabIndex;

	private BuildingManager BuildingManager => GameContext.HasInstance ? GameContext.Instance.BuildingMgr : null;

	protected override void AddListener()
	{
	}

	protected override void RemoveListeners()
	{
		ClearFacilitySummaryRows();
	}

	protected override void LinkData()
	{
		EnsureUi();
		SetupTabs();
		SetTab(0);
		RefreshAll();
	}

	protected override void UpdateData()
	{
		RefreshSummaryValues();
	}

	private void EnsureUi()
	{
		if (uiBuilt)
			return;

		HideLegacyVisuals();
		window = GetComponentInParent<UIWindow>(true);

		RectTransform selfRect = GetComponent<RectTransform>();
		if (selfRect != null)
		{
			selfRect.anchorMin = Vector2.zero;
			selfRect.anchorMax = Vector2.one;
			selfRect.offsetMin = Vector2.zero;
			selfRect.offsetMax = Vector2.zero;
		}

		if (layoutPrefab == null)
		{
			Debug.LogError("[BuildingDetailContent] Layout prefab is missing.", this);
			return;
		}

		layoutView = Instantiate(layoutPrefab, transform);
		layoutView.name = "BuildingDetailLayout";
		SetTopStretch(layoutView.GetComponent<RectTransform>(), 12f, 12f, 4f);

		CollectTabEntries();

		nameValue = CreateInfoLine(layoutView.OverviewTab.transform, "Name");
		typeValue = CreateInfoLine(layoutView.OverviewTab.transform, "Type");
		stateValue = CreateInfoLine(layoutView.OverviewTab.transform, "State");
		workScopeValue = CreateInfoLine(layoutView.OverviewTab.transform, "Work Scope");
		averageTemperatureValue = CreateInfoLine(layoutView.OverviewTab.transform, "Average Temperature");
		cellCountValue = CreateInfoLine(layoutView.OverviewTab.transform, "Cells");
		facilityCountValue = CreateInfoLine(layoutView.OverviewTab.transform, "Facilities");
		cargoPortCountValue = CreateInfoLine(layoutView.OverviewTab.transform, "Cargo Ports");
		actionStateValue = CreateInfoLine(layoutView.ActionTab.transform, "Current State");

		if (layoutView.FacilitiesSummaryText != null)
			layoutView.FacilitiesSummaryText.fontSize = 20f;

		if (layoutView.PolicyHelpText != null)
		{
			layoutView.PolicyHelpText.fontSize = 20f;
			layoutView.PolicyHelpText.text = "Change how far workers assigned to this building are allowed to operate.";
		}

		if (layoutView.DemolitionNoteText != null)
			layoutView.DemolitionNoteText.text = "Demolition flow is not wired yet. Use the actions below only to mark intent.";

		BindSettingsSection();
		layoutView.WorkScopeButton?.Configure("Cycle Work Scope", HandleWorkScopeButtonClicked);
		layoutView.PendingDemolitionButton?.Configure("Mark Pending Demolition", HandleMarkPendingDemolitionClicked);
		layoutView.RestoreActiveButton?.Configure("Restore Active State", HandleRestoreActiveClicked);
		uiBuilt = true;
	}

	private void SetupTabs()
	{
		if (window == null)
			return;

		window.ClearTabs();
		for (int i = 0; i < tabEntries.Count; i++)
			window.AddTab(tabEntries[i].Label, SetTab);

		window.UpdateTabVisuals(currentTabIndex);
	}

	private void SetTab(int tabIndex)
	{
		if (tabRoots.Count == 0)
			return;

		tabIndex = Mathf.Clamp(tabIndex, 0, tabRoots.Count - 1);
		currentTabIndex = tabIndex;
		for (int i = 0; i < tabRoots.Count; i++)
			tabRoots[i]?.SetActive(i == tabIndex);

		window?.UpdateTabVisuals(tabIndex);
		RefreshFacilitiesSection();
		RefreshSettingsSection();
	}

	private void CollectTabEntries()
	{
		tabRoots.Clear();
		tabEntries.Clear();
		IReadOnlyList<WindowTabContentEntry> entries = layoutView.GetTabContents();
		for (int i = 0; i < entries.Count; i++)
		{
			WindowTabContentEntry entry = entries[i];
			if (entry.ContentRoot == null)
				continue;

			tabEntries.Add(entry);
			tabRoots.Add(entry.ContentRoot);
		}
	}

	private void RefreshAll()
	{
		RefreshSummaryValues();
		RefreshFacilitiesSection();
		RefreshSettingsSection();
	}

	private void RefreshSummaryValues()
	{
		if (provider is not BuildingUIProvider buildingProvider)
			return;

		nameValue.text = buildingProvider.Name;
		typeValue.text = buildingProvider.Subtitle;
		stateValue.text = buildingProvider.StateDisplay;
		workScopeValue.text = buildingProvider.WorkScopeDisplay;
		averageTemperatureValue.text = buildingProvider.AverageTemperatureDisplay;
		cellCountValue.text = buildingProvider.CellCount.ToString();
		facilityCountValue.text = buildingProvider.FacilityCount.ToString();
		cargoPortCountValue.text = buildingProvider.CargoPortCount.ToString();
		actionStateValue.text = buildingProvider.StateDisplay;

		if (layoutView != null && layoutView.WorkScopeButton?.LabelText != null)
			layoutView.WorkScopeButton.LabelText.text = buildingProvider.WorkScopeDisplay;
		if (layoutView != null && layoutView.WorkScopeButton?.Button != null)
			layoutView.WorkScopeButton.Button.interactable = buildingProvider.Target?.Building != null;

		RefreshSettingsSection();
	}

	private void BindSettingsSection()
	{
		if (layoutView == null)
			return;

		settingsStatusText = layoutView.SettingsStatusText;
		overrideThresholdToggle = layoutView.OverrideThresholdToggle;
		thresholdSlider = layoutView.ThresholdSlider;
		thresholdValueText = layoutView.ThresholdValueText;

		if (overrideThresholdToggle != null)
			overrideThresholdToggle.onValueChanged.AddListener(HandleOverrideThresholdChanged);
		if (thresholdSlider != null)
			thresholdSlider.onValueChanged.AddListener(HandleThresholdSliderChanged);
	}

	private void RefreshSettingsSection()
	{
		if (provider is not BuildingUIProvider buildingProvider || buildingProvider.Target?.Building == null)
			return;

		Building building = buildingProvider.Target.Building;
		bool supportsCapsuleThreshold = SupportsCapsuleThreshold(building);
		if (settingsStatusText != null)
		{
			settingsStatusText.text = supportsCapsuleThreshold
				? "Outbound capsule threshold controls when OBStandby capsules become OB."
				: "This building does not use outbound capsule threshold.";
		}

		if (overrideThresholdToggle != null)
		{
			overrideThresholdToggle.SetIsOnWithoutNotify(building.OverrideCapsuleThreshold);
			overrideThresholdToggle.interactable = supportsCapsuleThreshold;
		}

		if (thresholdSlider != null)
		{
			thresholdSlider.SetValueWithoutNotify(building.CapsuleThresholdPercent);
			thresholdSlider.interactable = supportsCapsuleThreshold && building.OverrideCapsuleThreshold;
		}

		if (thresholdValueText != null)
			thresholdValueText.text = $"{Mathf.RoundToInt(building.CapsuleThresholdPercent)}%";
	}

	private void HandleOverrideThresholdChanged(bool isOn)
	{
		if (provider is not BuildingUIProvider buildingProvider || buildingProvider.Target?.Building == null)
			return;

		buildingProvider.Target.Building.SetOverrideCapsuleThreshold(isOn);
		RefreshSettingsSection();
	}

	private void HandleThresholdSliderChanged(float value)
	{
		if (provider is not BuildingUIProvider buildingProvider || buildingProvider.Target?.Building == null)
			return;

		buildingProvider.Target.Building.SetCapsuleThresholdPercent(value);
		RefreshSettingsSection();
	}

	private void RefreshFacilitiesSection()
	{
		if (layoutView == null || layoutView.FacilitiesSummaryText == null || layoutView.SummaryRoot == null)
			return;

		ClearFacilitySummaryRows();

		if (provider is not BuildingUIProvider buildingProvider || buildingProvider.Target?.Building == null)
		{
			layoutView.FacilitiesSummaryText.text = "Building context is unavailable.";
			return;
		}

		Building building = buildingProvider.Target.Building;
		IReadOnlyList<IFacility> facilities = building.OccupiedFacilities;
		layoutView.FacilitiesSummaryText.text = $"Total Facilities: {facilities.Count}";

		if (facilities.Count <= 0)
		{
			AddFacilitySummaryRow("No facilities in this building yet.");
		}
		else
		{
			SortedDictionary<string, int> counts = new(StringComparer.Ordinal);
			for (int i = 0; i < facilities.Count; ++i)
			{
				IFacility facility = facilities[i];
				if (facility == null)
					continue;

				string key = facility.GetType().Name;
				if (counts.TryGetValue(key, out int currentCount) == false)
					currentCount = 0;

				counts[key] = currentCount + 1;
			}

			foreach (KeyValuePair<string, int> pair in counts)
				AddFacilitySummaryRow($"{pair.Key}: {pair.Value}");
		}

		AddFacilitySectionHeader("InputBuilding");
		AddConnectedBuildingRows(building, isInputSection: true);
		AddFacilitySectionHeader("OutputBuilding");
		AddConnectedBuildingRows(building, isInputSection: false);
	}

	private void HandleWorkScopeButtonClicked()
	{
		if (provider is not BuildingUIProvider buildingProvider || buildingProvider.Target?.Building == null)
			return;

		Building building = buildingProvider.Target.Building;
		BuildingWorkScope nextScope = GetNextWorkScope(building.WorkScope);
		buildingProvider.Target.BuildingManager?.SetBuildingWorkScope(building, nextScope);
		RefreshSummaryValues();
	}

	private void HandleMarkPendingDemolitionClicked()
	{
		if (provider is BuildingUIProvider buildingProvider && buildingProvider.Target?.Building != null)
			buildingProvider.Target.BuildingManager?.SetBuildingState(buildingProvider.Target.Building, BuildingState.PendingDemolition);
	}

	private void HandleRestoreActiveClicked()
	{
		if (provider is BuildingUIProvider buildingProvider && buildingProvider.Target?.Building != null)
			buildingProvider.Target.BuildingManager?.SetBuildingState(buildingProvider.Target.Building, BuildingState.Active);
	}

	private void EnsureSelectionUIMaster()
	{
		if (selectionUIMaster == null)
			selectionUIMaster = GetComponentInParent<SelectionUIMaster>(true);

		if (selectionUIMaster == null)
			selectionUIMaster = FindFirstObjectByType<SelectionUIMaster>(FindObjectsInactive.Include);
	}

	private void EnsureBuildingOverlayController()
	{
		if (buildingOverlayController == null)
			buildingOverlayController = FindFirstObjectByType<BuildingPlacementOverlayController>(FindObjectsInactive.Include);
	}

	private void AddFacilitySummaryRow(string text)
	{
		if (summaryRowPrefab == null || layoutView == null)
		{
			Debug.LogError("[BuildingDetailContent] Summary row prefab is missing.", this);
			return;
		}

		TextRowView row = Instantiate(summaryRowPrefab, layoutView.SummaryRoot);
		row.name = "FacilitySummaryRow";
		if (row.Text != null)
		{
			row.Text.fontSize = 20f;
			row.Text.text = text;
		}

		facilitySummaryRows.Add(row.gameObject);
	}

	private void ClearFacilitySummaryRows()
	{
		for (int i = 0; i < facilitySummaryRows.Count; ++i)
		{
			GameObject row = facilitySummaryRows[i];
			if (row != null)
			{
				row.SetActive(false);
				Destroy(row);
			}
		}

		facilitySummaryRows.Clear();
	}

	private void AddFacilitySectionHeader(string text)
	{
		if (summaryRowPrefab == null || layoutView == null)
		{
			Debug.LogError("[BuildingDetailContent] Summary row prefab is missing.", this);
			return;
		}

		TextRowView row = Instantiate(summaryRowPrefab, layoutView.SummaryRoot);
		row.name = text.Replace(" ", string.Empty) + "Header";
		if (row.Text != null)
		{
			row.Text.fontSize = 21f;
			row.Text.fontStyle = FontStyles.Bold;
			row.Text.text = text;
		}

		facilitySummaryRows.Add(row.gameObject);
	}

	private void AddConnectedBuildingRows(Building currentBuilding, bool isInputSection)
	{
		if (currentBuilding == null)
			return;

		if (BuildingManager == null)
		{
			AddFacilitySummaryRow("Building manager is unavailable.");
			return;
		}

		List<Building> linkedBuildings = new();
		bool hasLinks = isInputSection
			? BuildingManager.TryGetInputBuildings(currentBuilding, linkedBuildings)
			: BuildingManager.TryGetOutputBuildings(currentBuilding, linkedBuildings);

		if (hasLinks == false)
		{
			AddFacilitySummaryRow("None");
			return;
		}

		for (int i = 0; i < linkedBuildings.Count; ++i)
		{
			Building linkedBuilding = linkedBuildings[i];
			if (linkedBuilding == null)
				continue;

			CreateConnectedBuildingRow(currentBuilding, linkedBuilding, isInputSection);
		}
	}

	private void CreateConnectedBuildingRow(Building currentBuilding, Building linkedBuilding, bool isInputSection)
	{
		if (layoutView == null || layoutView.SummaryRoot == null || linkedBuilding == null)
			return;

		GameObject rowObject = new($"{linkedBuilding.DisplayName}LinkRow", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
		rowObject.transform.SetParent(layoutView.SummaryRoot, false);
		facilitySummaryRows.Add(rowObject);

		HorizontalLayoutGroup rowLayout = rowObject.GetComponent<HorizontalLayoutGroup>();
		rowLayout.spacing = 10f;
		rowLayout.childAlignment = TextAnchor.MiddleLeft;
		rowLayout.childControlWidth = false;
		rowLayout.childControlHeight = true;
		rowLayout.childForceExpandWidth = false;
		rowLayout.childForceExpandHeight = false;

		LayoutElement rowElement = rowObject.GetComponent<LayoutElement>();
		rowElement.preferredHeight = 34f;

		TextMeshProUGUI label = CreateInlineText("Label", rowObject.transform, linkedBuilding.DisplayName, 20f, TextAlignmentOptions.MidlineLeft);
		LayoutElement labelLayout = label.gameObject.GetComponent<LayoutElement>();
		labelLayout.flexibleWidth = 1f;
		labelLayout.minWidth = 200f;

		CreateInlineButton(rowObject.transform, "Details", () => HandleOpenLinkedBuildingDetailsClicked(linkedBuilding));
		CreateInlineButton(rowObject.transform, "Disconnect", () => HandleDisconnectBuildingLinkClicked(currentBuilding, linkedBuilding, isInputSection));
	}

	private void HandleOpenLinkedBuildingDetailsClicked(Building building)
	{
		if (building == null)
			return;

		EnsureBuildingOverlayController();
		EnsureSelectionUIMaster();
		BuildingSelectionProxy proxy = buildingOverlayController?.GetSelectionProxy(building);
		if (proxy == null)
			return;

		selectionUIMaster?.SelectAndShowDetail(proxy.gameObject);
	}

	private void HandleDisconnectBuildingLinkClicked(Building currentBuilding, Building linkedBuilding, bool isInputSection)
	{
		if (currentBuilding == null || linkedBuilding == null || BuildingManager == null)
			return;

		if (isInputSection)
			BuildingManager.TryUnlinkBuildings(linkedBuilding, currentBuilding);
		else
			BuildingManager.TryUnlinkBuildings(currentBuilding, linkedBuilding);

		RefreshAll();
	}

	private TextMeshProUGUI CreateInfoLine(Transform parent, string label)
	{
		if (infoRowPrefab == null)
		{
			Debug.LogError("[BuildingDetailContent] Info row prefab is missing.", this);
			return null;
		}

		DetailInfoRowView row = Instantiate(infoRowPrefab, parent);
		row.name = label.Replace(" ", string.Empty) + "Row";
		row.SetLabel(label + ":");
		if (row.LabelText != null)
		{
			row.LabelText.fontStyle = FontStyles.Bold;
			row.LabelText.textWrappingMode = TextWrappingModes.NoWrap;
		}

		return row.ValueText;
	}

	private static BuildingWorkScope GetNextWorkScope(BuildingWorkScope currentScope)
	{
		int enumCount = Enum.GetValues(typeof(BuildingWorkScope)).Length;
		int nextIndex = (((int)currentScope) + 1) % enumCount;
		return (BuildingWorkScope)nextIndex;
	}

	private static bool SupportsCapsuleThreshold(Building building)
	{
		return building != null && (building.Type == BuildingType.Storage || building.Type == BuildingType.Packing);
	}

	private static TextMeshProUGUI CreateInlineText(string objectName, Transform parent, string value, float fontSize, TextAlignmentOptions alignment)
	{
		GameObject textObject = new(objectName, typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
		textObject.transform.SetParent(parent, false);

		LayoutElement layout = textObject.GetComponent<LayoutElement>();
		layout.preferredHeight = 28f;

		TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
		text.text = value;
		text.fontSize = fontSize;
		text.color = Color.white;
		text.alignment = alignment;
		text.textWrappingMode = TextWrappingModes.NoWrap;
		text.overflowMode = TextOverflowModes.Ellipsis;

		RectTransform rect = text.rectTransform;
		rect.anchorMin = Vector2.zero;
		rect.anchorMax = Vector2.one;
		rect.offsetMin = Vector2.zero;
		rect.offsetMax = Vector2.zero;

		return text;
	}

	private static Button CreateInlineButton(Transform parent, string label, UnityAction onClick)
	{
		GameObject buttonObject = new($"{label}Button", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
		buttonObject.transform.SetParent(parent, false);

		Image image = buttonObject.GetComponent<Image>();
		image.color = new Color(0.19f, 0.19f, 0.19f, 0.96f);

		LayoutElement layout = buttonObject.GetComponent<LayoutElement>();
		layout.preferredWidth = label == "Disconnect" ? 120f : 96f;
		layout.preferredHeight = 30f;

		Button button = buttonObject.GetComponent<Button>();
		button.targetGraphic = image;
		button.onClick.RemoveAllListeners();
		if (onClick != null)
			button.onClick.AddListener(onClick);

		TextMeshProUGUI text = CreateInlineText("Label", buttonObject.transform, label, 18f, TextAlignmentOptions.Center);
		text.margin = new Vector4(6f, 2f, 6f, 2f);
		return button;
	}
}
