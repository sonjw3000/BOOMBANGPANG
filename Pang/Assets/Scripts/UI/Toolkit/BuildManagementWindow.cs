using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace UniverseLogistics.UI.Toolkit
{
	public sealed class BuildManagementWindow : MonoBehaviour
	{
		private const string SelectedTabClass = "build-tab-button--selected";
		private const string SelectedCategoryClass = "build-category-button--selected";
		private static readonly BuildingType[] BuildingTypes =
		{
			BuildingType.Staging,
			BuildingType.Storage,
			BuildingType.Packing,
			BuildingType.Launch,
		};
		private static readonly ItemStatus[] RuleItemStatuses = { ItemStatus.NotDefined, ItemStatus.None, ItemStatus.Labeled, ItemStatus.Packed };
		private static readonly WorkerKind[] RuleWorkerKinds = { WorkerKind.None, WorkerKind.Human, WorkerKind.Robot };

		private UIWindow window;
		private VisualTreeAsset contentTemplate;
		private VisualTreeAsset placeableRowTemplate;
		private VisualTreeAsset ruleRowTemplate;
		private BuildingPlacementOverlayController buildingPlacementOverlay;
		private Button buildingsButton;
		private Button facilitiesButton;
		private Button rulesButton;
		private VisualElement buildingsTab;
		private VisualElement facilitiesTab;
		private VisualElement rulesTab;
		private DropdownField buildingTypeField;
		private DropdownField footprintField;
		private Label buildingSelectionName;
		private Label buildingSelectionDetails;
		private Label buildingMessage;
		private Button createBuildingButton;
		private ScrollView categoryList;
		private Label catalogTitle;
		private Label catalogMessage;
		private ScrollView placeableList;
		private Label placeableEmpty;
		private Button createRuleButton;
		private ScrollView ruleList;
		private Label ruleEmpty;
		private Label ruleMessage;
		private VisualElement ruleLibrary;
		private ScrollView ruleEditor;
		private Label ruleEditorTitle;
		private TextField ruleNameField;
		private IntegerField rulePriorityField;
		private Slider ruleRedSlider;
		private Slider ruleGreenSlider;
		private Slider ruleBlueSlider;
		private VisualElement ruleColorPreview;
		private DropdownField ruleItemStatusField;
		private DropdownField ruleWorkerKindField;
		private DropdownField ruleItemField;
		private Label whiteListSummary;
		private Label blackListSummary;
		private readonly List<ItemDefinition> ruleItems = new();
		private FacilityRule workingRule = new();
		private Color workingRuleColor = Color.white;
		private uint editingRuleId;
		private bool suppressRuleEditorEvents;
		private bool applyModeActive;
		private uint applyingRuleId;
		private readonly List<BuildingFootprintPreset> footprintPresets = new();
		private BuildPlaceableSection selectedSection;
		private BuildPlaceableCatalog catalog;
		private BuildingFootprintService footprintService;
		private EconomyService economyService;
		private FacilityRuleManager ruleManager;
		private bool initialized;
		private bool started;

		public void Configure(UIWindow targetWindow, VisualTreeAsset targetContentTemplate,
			VisualTreeAsset targetPlaceableRowTemplate, VisualTreeAsset targetRuleRowTemplate,
			BuildingPlacementOverlayController targetBuildingPlacementOverlay)
		{
			window = targetWindow;
			contentTemplate = targetContentTemplate;
			placeableRowTemplate = targetPlaceableRowTemplate;
			ruleRowTemplate = targetRuleRowTemplate;
			buildingPlacementOverlay = targetBuildingPlacementOverlay;
		}

		private void OnEnable()
		{
			InitializeView();
			if (started) BindServices();
		}

		private void Start()
		{
			started = true;
			BindServices();
		}

		private void Update()
		{
			if (applyModeActive && Input.GetMouseButtonDown(1)) EndApplyMode();
		}

		private void OnDisable()
		{
			EndApplyMode();
			UnbindControls();
			UnbindServices();
			initialized = false;
		}

		public void Open()
		{
			if (InitializeView() == false) return;
			EndApplyMode();
			if (catalog == null || footprintService == null) BindServices();
			RefreshAll();
			window.Open();
		}

		private bool InitializeView()
		{
			if (initialized) return true;
			if (window == null || contentTemplate == null || placeableRowTemplate == null || ruleRowTemplate == null || window.Initialize() == false)
			{
				Debug.LogError("[BuildManagementWindow] Window or VisualTreeAsset references are missing.", this);
				return false;
			}

			TemplateContainer content = contentTemplate.CloneTree();
			buildingsButton = content.Q<Button>("build-buildings-button");
			facilitiesButton = content.Q<Button>("build-facilities-button");
			rulesButton = content.Q<Button>("build-rules-button");
			buildingsTab = content.Q<VisualElement>("build-buildings-tab");
			facilitiesTab = content.Q<VisualElement>("build-facilities-tab");
			rulesTab = content.Q<VisualElement>("build-rules-tab");
			buildingTypeField = content.Q<DropdownField>("building-type-field");
			footprintField = content.Q<DropdownField>("building-footprint-field");
			buildingSelectionName = content.Q<Label>("building-selection-name");
			buildingSelectionDetails = content.Q<Label>("building-selection-details");
			buildingMessage = content.Q<Label>("building-build-message");
			createBuildingButton = content.Q<Button>("create-building-button");
			categoryList = content.Q<ScrollView>("build-category-list");
			catalogTitle = content.Q<Label>("build-catalog-title");
			catalogMessage = content.Q<Label>("build-catalog-message");
			placeableList = content.Q<ScrollView>("build-placeable-list");
			placeableEmpty = content.Q<Label>("build-placeable-empty");
			createRuleButton = content.Q<Button>("create-rule-preset-button");
			ruleList = content.Q<ScrollView>("build-rule-list");
			ruleEmpty = content.Q<Label>("build-rule-empty");
			ruleMessage = content.Q<Label>("build-rule-message");
			ruleLibrary = content.Q<VisualElement>("build-rule-library");
			ruleEditor = content.Q<ScrollView>("build-rule-editor");
			ruleEditorTitle = content.Q<Label>("rule-editor-title");
			ruleNameField = content.Q<TextField>("rule-editor-name");
			rulePriorityField = content.Q<IntegerField>("rule-editor-priority");
			ruleRedSlider = content.Q<Slider>("rule-editor-red");
			ruleGreenSlider = content.Q<Slider>("rule-editor-green");
			ruleBlueSlider = content.Q<Slider>("rule-editor-blue");
			ruleColorPreview = content.Q<VisualElement>("rule-editor-color-preview");
			ruleItemStatusField = content.Q<DropdownField>("rule-editor-item-status");
			ruleWorkerKindField = content.Q<DropdownField>("rule-editor-worker-kind");
			ruleItemField = content.Q<DropdownField>("rule-editor-item");
			whiteListSummary = content.Q<Label>("rule-whitelist-summary");
			blackListSummary = content.Q<Label>("rule-blacklist-summary");

			if (buildingsButton == null || facilitiesButton == null || rulesButton == null || buildingsTab == null ||
				facilitiesTab == null || rulesTab == null ||
				buildingTypeField == null || footprintField == null || buildingSelectionName == null ||
				buildingSelectionDetails == null || buildingMessage == null || createBuildingButton == null ||
				categoryList == null || catalogTitle == null || catalogMessage == null || placeableList == null ||
				placeableEmpty == null || createRuleButton == null || ruleList == null || ruleEmpty == null || ruleMessage == null ||
				ruleLibrary == null || ruleEditor == null || ruleNameField == null || rulePriorityField == null ||
				ruleColorPreview == null || ruleItemStatusField == null || ruleWorkerKindField == null || ruleItemField == null)
			{
				Debug.LogError("[BuildManagementWindow] Required UXML elements are missing.", this);
				return false;
			}

			window.SetTitle("Build Management");
			window.SetContent(content);
			buildingsButton.clicked += OpenBuildings;
			facilitiesButton.clicked += OpenFacilities;
			rulesButton.clicked += OpenRules;
			createBuildingButton.clicked += BeginBuildingPlacement;
			createRuleButton.clicked += CreateRule;
			buildingTypeField.choices = BuildTypeChoices();
			buildingTypeField.SetValueWithoutNotify(buildingTypeField.choices[0]);
			buildingTypeField.RegisterValueChangedCallback(OnBuildingSelectionChanged);
			footprintField.RegisterValueChangedCallback(OnFootprintChanged);
			BindRuleEditor(content);
			initialized = true;
			SelectTab(0);
			ShowRuleLibrary();
			return true;
		}

		private void UnbindControls()
		{
			if (buildingsButton != null) buildingsButton.clicked -= OpenBuildings;
			if (facilitiesButton != null) facilitiesButton.clicked -= OpenFacilities;
			if (rulesButton != null) rulesButton.clicked -= OpenRules;
			if (createBuildingButton != null) createBuildingButton.clicked -= BeginBuildingPlacement;
			if (createRuleButton != null) createRuleButton.clicked -= CreateRule;
			buildingTypeField?.UnregisterValueChangedCallback(OnBuildingSelectionChanged);
			footprintField?.UnregisterValueChangedCallback(OnFootprintChanged);
		}

		private void BindServices()
		{
			UnbindServices();
			if (GameContext.HasInstance == false) return;
			catalog = GameContext.Instance.BuildPlaceableCatalog;
			footprintService = GameContext.Instance.BuildingFootprintService;
			economyService = GameContext.Instance.EconomyService;
			ruleManager = GameContext.Instance.FacilityRuleMgr;
			if (economyService != null) economyService.OnMoneyChanged += OnMoneyChanged;
			if (ruleManager != null)
			{
				ruleManager.OnPresetCreated += OnRuleChanged;
				ruleManager.OnPresetChanged += OnRuleChanged;
				ruleManager.OnPresetDeleted += OnRuleDeleted;
				ruleManager.OnFacilityRulePresetApplied += OnRuleApplied;
				ruleManager.OnPresetsRebuilt += RefreshRules;
			}
		}

		private void UnbindServices()
		{
			if (economyService != null) economyService.OnMoneyChanged -= OnMoneyChanged;
			if (ruleManager != null)
			{
				ruleManager.OnPresetCreated -= OnRuleChanged;
				ruleManager.OnPresetChanged -= OnRuleChanged;
				ruleManager.OnPresetDeleted -= OnRuleDeleted;
				ruleManager.OnFacilityRulePresetApplied -= OnRuleApplied;
				ruleManager.OnPresetsRebuilt -= RefreshRules;
			}
			catalog = null;
			footprintService = null;
			economyService = null;
			ruleManager = null;
		}

		private void OpenBuildings() => SelectTab(0);
		private void OpenFacilities() => SelectTab(1);
		private void OpenRules() => SelectTab(2);

		private void SelectTab(int index)
		{
			buildingsTab.style.display = index == 0 ? DisplayStyle.Flex : DisplayStyle.None;
			facilitiesTab.style.display = index == 1 ? DisplayStyle.Flex : DisplayStyle.None;
			rulesTab.style.display = index == 2 ? DisplayStyle.Flex : DisplayStyle.None;
			buildingsButton.EnableInClassList(SelectedTabClass, index == 0);
			facilitiesButton.EnableInClassList(SelectedTabClass, index == 1);
			rulesButton.EnableInClassList(SelectedTabClass, index == 2);
		}

		private void RefreshAll()
		{
			RefreshBuildingOptions();
			RefreshCategories();
			DisplaySelectedSection();
			RefreshRules();
		}

		private void RefreshBuildingOptions()
		{
			footprintPresets.Clear();
			List<string> choices = new();
			if (footprintService != null)
			{
				foreach (BuildingFootprintPreset preset in footprintService.AvailablePresets)
				{
					if (preset == null || preset.IsValid == false) continue;
					footprintPresets.Add(preset);
					choices.Add(preset.DisplayName);
				}
			}

			footprintField.choices = choices;
			int selectedIndex = Mathf.Max(0, footprintPresets.IndexOf(footprintService?.ActivePreset));
			if (choices.Count > 0) footprintField.SetValueWithoutNotify(choices[Mathf.Clamp(selectedIndex, 0, choices.Count - 1)]);
			RefreshBuildingSelection();
		}

		private void OnBuildingSelectionChanged(ChangeEvent<string> _) => RefreshBuildingSelection();

		private void OnFootprintChanged(ChangeEvent<string> _)
		{
			if (footprintService != null && footprintField.index >= 0 && footprintField.index < footprintPresets.Count)
				footprintService.SetActivePreset(footprintPresets[footprintField.index]);
			RefreshBuildingSelection();
		}

		private void RefreshBuildingSelection()
		{
			BuildingFootprintPreset preset = GetSelectedPreset();
			BuildingType type = GetSelectedBuildingType();
			bool available = preset != null && footprintService != null;
			createBuildingButton.SetEnabled(available);
			buildingSelectionName.text = available
				? $"{BuildingTypeUtility.ToDisplayString(type)} · {preset.DisplayName}"
				: "No footprint available";
			buildingSelectionDetails.text = available
				? $"{preset.Width} × {preset.Height} footprint. Wall costs are charged only when placement succeeds."
				: "Building placement requires a valid footprint preset.";
		}

		private void BeginBuildingPlacement()
		{
			BuildingFootprintPreset preset = GetSelectedPreset();
			if (preset == null || footprintService == null || footprintService.SetActivePreset(preset) == false)
			{
				buildingMessage.text = "Building placement is unavailable.";
				return;
			}

			if (buildingPlacementOverlay == null)
			{
				buildingMessage.text = "Building placement overlay is unavailable.";
				return;
			}

			buildingPlacementOverlay.SetSelectedBuildingType(GetSelectedBuildingType());
			window.Close();
			buildingPlacementOverlay.BeginCreateOneShot();
		}

		private void RefreshCategories()
		{
			categoryList.Clear();
			IReadOnlyList<BuildPlaceableSection> sections = catalog?.Sections;
			if (ContainsSection(sections, selectedSection) == false) selectedSection = FirstSection(sections);
			if (sections == null) return;
			foreach (BuildPlaceableSection section in sections)
			{
				if (section == null) continue;
				BuildPlaceableSection captured = section;
				Button button = new(() => SelectSection(captured)) { text = GetSectionName(section) };
				button.AddToClassList("build-category-button");
				button.EnableInClassList(SelectedCategoryClass, section == selectedSection);
				categoryList.Add(button);
			}
		}

		private void SelectSection(BuildPlaceableSection section)
		{
			selectedSection = section;
			RefreshCategories();
			DisplaySelectedSection();
		}

		private void DisplaySelectedSection()
		{
			placeableList.Clear();
			catalogTitle.text = selectedSection != null ? GetSectionName(selectedSection) : "Facilities";
			int count = 0;
			if (selectedSection?.placeables != null)
			{
				foreach (PlaceableDefinition definition in selectedSection.placeables)
				{
					if (definition == null || definition.prefab == null || definition.gridFootprint == null) continue;
					placeableList.Add(CreatePlaceableRow(definition));
					++count;
				}
			}
			catalogMessage.text = count > 0 ? $"{count} buildable facilities. Select one to enter placement mode." : "No available facilities.";
			placeableEmpty.style.display = count == 0 ? DisplayStyle.Flex : DisplayStyle.None;
		}

		private VisualElement CreatePlaceableRow(PlaceableDefinition definition)
		{
			TemplateContainer row = placeableRowTemplate.CloneTree();
			VisualElement icon = row.Q<VisualElement>("build-placeable-icon");
			row.Q<Label>("build-placeable-name").text = GetPlaceableName(definition);
			row.Q<Label>("build-placeable-type").text = definition.definitionType.ToString();
			row.Q<Label>("build-placeable-size").text = $"{definition.gridFootprint.width} × {definition.gridFootprint.height}";
			row.Q<Label>("build-placeable-environment").text = GetEnvironmentName(definition.placementEnvironment);
			row.Q<Label>("build-placeable-cost").text = $"${definition.Cost:N0}";
			if (definition.icon != null) icon.style.backgroundImage = new StyleBackground(definition.icon);
			Button placeButton = row.Q<Button>("build-placeable-button");
			placeButton.SetEnabled(economyService != null && economyService.CanAfford(Mathf.Max(0, definition.Cost)));
			placeButton.clicked += () => BeginFacilityPlacement(definition);
			return row;
		}

		private void BeginFacilityPlacement(PlaceableDefinition definition)
		{
			if (definition == null || GameContext.HasInstance == false || GameContext.Instance.InteractionCtx == null) return;
			window.Close();
			GameContext.Instance.InteractionCtx.EnterPlacementMode(definition);
		}

		private void RefreshRules()
		{
			if (ruleList == null) return;
			ruleList.Clear();
			IReadOnlyList<FacilityRulePreset> presets = ruleManager?.Presets;
			int count = presets?.Count ?? 0;
			for (int i = 0; i < count; ++i)
			{
				FacilityRulePreset preset = presets[i];
				if (preset != null) ruleList.Add(CreateRuleRow(preset));
			}
			ruleEmpty.style.display = count == 0 ? DisplayStyle.Flex : DisplayStyle.None;
			ruleMessage.text = ruleManager != null
				? $"{count} presets · {ruleManager.GetNoRuleFacilityCount()} facilities currently have no Rule."
				: "FacilityRuleManager is unavailable.";
		}

		private VisualElement CreateRuleRow(FacilityRulePreset preset)
		{
			TemplateContainer row = ruleRowTemplate.CloneTree();
			row.Q<VisualElement>("build-rule-color").style.backgroundColor = preset.Color;
			row.Q<Label>("build-rule-name").text = preset.DisplayName;
			row.Q<Label>("build-rule-summary").text = BuildRuleSummary(preset.Rule);
			row.Q<Label>("build-rule-priority").text = $"Priority {preset.Rule?.Priority ?? 0}";
			row.Q<Label>("build-rule-applied").text = $"{ruleManager?.GetAppliedFacilityCount(preset.Id) ?? 0} facilities";
			Button duplicateButton = row.Q<Button>("build-rule-duplicate-button");
			Button applyButton = row.Q<Button>("build-rule-apply-button");
			Button editButton = row.Q<Button>("build-rule-edit-button");
			Button deleteButton = row.Q<Button>("build-rule-delete-button");
			duplicateButton.clicked += () => DuplicateRule(preset);
			applyButton.clicked += () => BeginApplyMode(preset.Id);
			editButton.clicked += () => EditRule(preset);
			bool deleteConfirmed = false;
			deleteButton.clicked += () =>
			{
				if (deleteConfirmed == false)
				{
					deleteConfirmed = true;
					deleteButton.text = "Confirm";
					return;
				}
				ruleManager?.DeletePreset(preset.Id);
			};
			return row;
		}

		private void CreateRule()
		{
			editingRuleId = FacilityRuleManager.NoRulePresetId;
			workingRule = new FacilityRule();
			workingRuleColor = Color.white;
			ShowRuleEditor("Create Facility Rule", $"Rule {(ruleManager?.Presets.Count ?? 0) + 1}");
		}

		private void EditRule(FacilityRulePreset preset)
		{
			if (preset == null) return;
			editingRuleId = preset.Id;
			workingRule = new FacilityRule(preset.Rule);
			workingRuleColor = preset.Color;
			ShowRuleEditor("Edit Facility Rule", preset.DisplayName);
		}

		private void DuplicateRule(FacilityRulePreset preset)
		{
			if (preset == null || ruleManager == null) return;
			ruleManager.CreatePreset($"{preset.DisplayName} Copy", new FacilityRule(preset.Rule), preset.Color);
		}

		private void OnRuleChanged(FacilityRulePreset _) => RefreshRules();
		private void OnRuleDeleted(uint _) => RefreshRules();
		private void OnRuleApplied(IFacility facility, uint previousPresetId, uint nextPresetId) => RefreshRules();

		private void BindRuleEditor(VisualElement content)
		{
			ruleItemStatusField.choices = EnumNames(RuleItemStatuses);
			ruleWorkerKindField.choices = EnumNames(RuleWorkerKinds);
			ruleItems.Clear();
			List<string> itemChoices = new();
			ItemDatabase itemDatabase = GameContext.HasInstance ? GameContext.Instance.ItemDB : null;
			if (itemDatabase != null)
			{
				for (int i = 0; itemDatabase.TryGetItemBySortedIndex(i, out ItemDefinition item); ++i)
				{
					ruleItems.Add(item);
					itemChoices.Add($"{item.ItemID} · {item.name}");
				}
			}
			ruleItemField.choices = itemChoices;
			if (itemChoices.Count > 0) ruleItemField.SetValueWithoutNotify(itemChoices[0]);

			rulePriorityField.RegisterValueChangedCallback(evt => { if (!suppressRuleEditorEvents) workingRule.SetPriority(Mathf.Max(0, evt.newValue)); });
			ruleRedSlider.RegisterValueChangedCallback(_ => OnRuleColorChanged());
			ruleGreenSlider.RegisterValueChangedCallback(_ => OnRuleColorChanged());
			ruleBlueSlider.RegisterValueChangedCallback(_ => OnRuleColorChanged());
			ruleItemStatusField.RegisterValueChangedCallback(_ => { if (!suppressRuleEditorEvents && ruleItemStatusField.index >= 0) workingRule.ItemRule.SetRequiredItemStatus(RuleItemStatuses[ruleItemStatusField.index]); });
			ruleWorkerKindField.RegisterValueChangedCallback(_ => { if (!suppressRuleEditorEvents && ruleWorkerKindField.index >= 0) workingRule.WorkerRule.SetRequiredWorkerKind(RuleWorkerKinds[ruleWorkerKindField.index]); });

			BindFlagToggle(content, "rule-required-tag-fragile", ItemTag.Fragile, true);
			BindFlagToggle(content, "rule-required-tag-food", ItemTag.Food, true);
			BindFlagToggle(content, "rule-required-tag-danger", ItemTag.Danger, true);
			BindFlagToggle(content, "rule-required-tag-electric", ItemTag.Electric, true);
			BindFlagToggle(content, "rule-forbidden-tag-fragile", ItemTag.Fragile, false);
			BindFlagToggle(content, "rule-forbidden-tag-food", ItemTag.Food, false);
			BindFlagToggle(content, "rule-forbidden-tag-danger", ItemTag.Danger, false);
			BindFlagToggle(content, "rule-forbidden-tag-electric", ItemTag.Electric, false);
			BindAbilityToggle(content, "rule-ability-carrybox", WorkerAbility.CarryBox);
			BindAbilityToggle(content, "rule-ability-picking", WorkerAbility.PickingStoring);
			BindAbilityToggle(content, "rule-ability-packing", WorkerAbility.Packing);
			BindAbilityToggle(content, "rule-ability-labeling", WorkerAbility.Labeling);
			BindAbilityToggle(content, "rule-ability-cargo", WorkerAbility.CargoHandling);
			BindHumanToggle(content, "rule-required-human-fulltime", HumanType.FullTime, true);
			BindHumanToggle(content, "rule-required-human-parttime", HumanType.PartTime, true);
			BindHumanToggle(content, "rule-required-human-illegal", HumanType.Illegal, true);
			BindHumanToggle(content, "rule-forbidden-human-fulltime", HumanType.FullTime, false);
			BindHumanToggle(content, "rule-forbidden-human-parttime", HumanType.PartTime, false);
			BindHumanToggle(content, "rule-forbidden-human-illegal", HumanType.Illegal, false);
			BindRobotToggle(content, "rule-required-robot-transfer", true);
			BindRobotToggle(content, "rule-forbidden-robot-transfer", false);
			BindDestinationToggle(content, "rule-destination-none", OrderDestination.None);
			BindDestinationToggle(content, "rule-destination-mars", OrderDestination.Mars);
			BindDestinationToggle(content, "rule-destination-titan", OrderDestination.Titan);

			content.Q<Button>("rule-add-whitelist").clicked += () => AddSelectedRuleItem(true);
			content.Q<Button>("rule-add-blacklist").clicked += () => AddSelectedRuleItem(false);
			content.Q<Button>("rule-clear-whitelist").clicked += () => { workingRule.ItemRule.SetWhiteList(Array.Empty<ItemDefinition>()); RefreshRuleItemLists(); };
			content.Q<Button>("rule-clear-blacklist").clicked += () => { workingRule.ItemRule.SetBlackList(Array.Empty<ItemDefinition>()); RefreshRuleItemLists(); };
			content.Q<Button>("rule-editor-save").clicked += SaveRuleEditor;
			content.Q<Button>("rule-editor-cancel").clicked += ShowRuleLibrary;
		}

		private void ShowRuleEditor(string title, string displayName)
		{
			ruleLibrary.style.display = DisplayStyle.None;
			ruleEditor.style.display = DisplayStyle.Flex;
			ruleEditorTitle.text = title;
			ruleNameField.SetValueWithoutNotify(displayName);
			RefreshRuleEditor();
		}

		private void ShowRuleLibrary()
		{
			if (ruleLibrary == null || ruleEditor == null) return;
			ruleLibrary.style.display = DisplayStyle.Flex;
			ruleEditor.style.display = DisplayStyle.None;
			RefreshRules();
		}

		private void SaveRuleEditor()
		{
			if (ruleManager == null) return;
			string displayName = ruleNameField.value;
			if (editingRuleId == FacilityRuleManager.NoRulePresetId)
				ruleManager.CreatePreset(displayName, workingRule, workingRuleColor);
			else
			{
				ruleManager.RenamePreset(editingRuleId, displayName);
				ruleManager.SetPresetColor(editingRuleId, workingRuleColor);
				ruleManager.SetPresetRule(editingRuleId, workingRule);
			}
			ShowRuleLibrary();
		}

		private void RefreshRuleEditor()
		{
			suppressRuleEditorEvents = true;
			rulePriorityField.SetValueWithoutNotify(workingRule.Priority);
			ruleRedSlider.SetValueWithoutNotify(workingRuleColor.r);
			ruleGreenSlider.SetValueWithoutNotify(workingRuleColor.g);
			ruleBlueSlider.SetValueWithoutNotify(workingRuleColor.b);
			ruleColorPreview.style.backgroundColor = workingRuleColor;
			ruleItemStatusField.SetValueWithoutNotify(workingRule.ItemRule.RequiredItemStatus.ToString());
			ruleWorkerKindField.SetValueWithoutNotify(workingRule.WorkerRule.RequiredWorkerKind.ToString());
			SetToggle("rule-required-tag-fragile", workingRule.ItemRule.RequiredItemTags.HasFlag(ItemTag.Fragile));
			SetToggle("rule-required-tag-food", workingRule.ItemRule.RequiredItemTags.HasFlag(ItemTag.Food));
			SetToggle("rule-required-tag-danger", workingRule.ItemRule.RequiredItemTags.HasFlag(ItemTag.Danger));
			SetToggle("rule-required-tag-electric", workingRule.ItemRule.RequiredItemTags.HasFlag(ItemTag.Electric));
			SetToggle("rule-forbidden-tag-fragile", workingRule.ItemRule.ForbiddenItemTags.HasFlag(ItemTag.Fragile));
			SetToggle("rule-forbidden-tag-food", workingRule.ItemRule.ForbiddenItemTags.HasFlag(ItemTag.Food));
			SetToggle("rule-forbidden-tag-danger", workingRule.ItemRule.ForbiddenItemTags.HasFlag(ItemTag.Danger));
			SetToggle("rule-forbidden-tag-electric", workingRule.ItemRule.ForbiddenItemTags.HasFlag(ItemTag.Electric));
			SetToggle("rule-ability-carrybox", workingRule.WorkerRule.RequiredWorkerAbility.HasFlag(WorkerAbility.CarryBox));
			SetToggle("rule-ability-picking", workingRule.WorkerRule.RequiredWorkerAbility.HasFlag(WorkerAbility.PickingStoring));
			SetToggle("rule-ability-packing", workingRule.WorkerRule.RequiredWorkerAbility.HasFlag(WorkerAbility.Packing));
			SetToggle("rule-ability-labeling", workingRule.WorkerRule.RequiredWorkerAbility.HasFlag(WorkerAbility.Labeling));
			SetToggle("rule-ability-cargo", workingRule.WorkerRule.RequiredWorkerAbility.HasFlag(WorkerAbility.CargoHandling));
			SetToggle("rule-required-human-fulltime", workingRule.WorkerRule.RequiredHumanTypes.Contains(HumanType.FullTime));
			SetToggle("rule-required-human-parttime", workingRule.WorkerRule.RequiredHumanTypes.Contains(HumanType.PartTime));
			SetToggle("rule-required-human-illegal", workingRule.WorkerRule.RequiredHumanTypes.Contains(HumanType.Illegal));
			SetToggle("rule-forbidden-human-fulltime", workingRule.WorkerRule.ForbiddenHumanTypes.Contains(HumanType.FullTime));
			SetToggle("rule-forbidden-human-parttime", workingRule.WorkerRule.ForbiddenHumanTypes.Contains(HumanType.PartTime));
			SetToggle("rule-forbidden-human-illegal", workingRule.WorkerRule.ForbiddenHumanTypes.Contains(HumanType.Illegal));
			SetToggle("rule-required-robot-transfer", workingRule.WorkerRule.RequiredRobotTypes.Contains(RobotType.Transfer));
			SetToggle("rule-forbidden-robot-transfer", workingRule.WorkerRule.ForbiddenRobotTypes.Contains(RobotType.Transfer));
			SetToggle("rule-destination-none", workingRule.ManifestRule.RequiredDestinations.Contains(OrderDestination.None));
			SetToggle("rule-destination-mars", workingRule.ManifestRule.RequiredDestinations.Contains(OrderDestination.Mars));
			SetToggle("rule-destination-titan", workingRule.ManifestRule.RequiredDestinations.Contains(OrderDestination.Titan));
			suppressRuleEditorEvents = false;
			RefreshRuleItemLists();
		}

		private void OnRuleColorChanged()
		{
			if (suppressRuleEditorEvents) return;
			workingRuleColor = new Color(ruleRedSlider.value, ruleGreenSlider.value, ruleBlueSlider.value, 1f);
			ruleColorPreview.style.backgroundColor = workingRuleColor;
		}

		private void BindFlagToggle(VisualElement root, string name, ItemTag tag, bool required)
		{
			root.Q<Toggle>(name).RegisterValueChangedCallback(evt =>
			{
				if (suppressRuleEditorEvents) return;
				if (required)
				{
					ItemTag value = workingRule.ItemRule.RequiredItemTags;
					workingRule.ItemRule.SetRequiredItemTags(evt.newValue ? value | tag : value & ~tag);
				}
				else
				{
					ItemTag value = workingRule.ItemRule.ForbiddenItemTags;
					workingRule.ItemRule.SetForbiddenItemTags(evt.newValue ? value | tag : value & ~tag);
				}
			});
		}

		private void BindAbilityToggle(VisualElement root, string name, WorkerAbility ability)
		{
			root.Q<Toggle>(name).RegisterValueChangedCallback(evt =>
			{
				if (suppressRuleEditorEvents) return;
				WorkerAbility value = workingRule.WorkerRule.RequiredWorkerAbility;
				workingRule.WorkerRule.SetRequiredWorkerAbility(evt.newValue ? value | ability : value & ~ability);
			});
		}

		private void BindHumanToggle(VisualElement root, string name, HumanType type, bool required)
		{
			root.Q<Toggle>(name).RegisterValueChangedCallback(evt =>
			{
				if (suppressRuleEditorEvents) return;
				List<HumanType> values = new(required ? workingRule.WorkerRule.RequiredHumanTypes : workingRule.WorkerRule.ForbiddenHumanTypes);
				SetListValue(values, type, evt.newValue);
				if (required) workingRule.WorkerRule.SetRequiredHumanTypes(values);
				else workingRule.WorkerRule.SetForbiddenHumanTypes(values);
			});
		}

		private void BindRobotToggle(VisualElement root, string name, bool required)
		{
			root.Q<Toggle>(name).RegisterValueChangedCallback(evt =>
			{
				if (suppressRuleEditorEvents) return;
				List<RobotType> values = new(required ? workingRule.WorkerRule.RequiredRobotTypes : workingRule.WorkerRule.ForbiddenRobotTypes);
				SetListValue(values, RobotType.Transfer, evt.newValue);
				if (required) workingRule.WorkerRule.SetRequiredRobotTypes(values);
				else workingRule.WorkerRule.SetForbiddenRobotTypes(values);
			});
		}

		private void BindDestinationToggle(VisualElement root, string name, OrderDestination destination)
		{
			root.Q<Toggle>(name).RegisterValueChangedCallback(evt =>
			{
				if (suppressRuleEditorEvents) return;
				List<OrderDestination> values = new(workingRule.ManifestRule.RequiredDestinations);
				SetListValue(values, destination, evt.newValue);
				workingRule.ManifestRule.SetRequiredDestinations(values);
			});
		}

		private void AddSelectedRuleItem(bool whiteList)
		{
			if (ruleItemField.index < 0 || ruleItemField.index >= ruleItems.Count) return;
			ItemDefinition item = ruleItems[ruleItemField.index];
			List<ItemDefinition> values = new(whiteList ? workingRule.ItemRule.WhiteList : workingRule.ItemRule.BlackList);
			if (values.Contains(item) == false) values.Add(item);
			if (whiteList) workingRule.ItemRule.SetWhiteList(values);
			else workingRule.ItemRule.SetBlackList(values);
			RefreshRuleItemLists();
		}

		private void RefreshRuleItemLists()
		{
			whiteListSummary.text = BuildItemSummary("White List", workingRule.ItemRule.WhiteList, "Any");
			blackListSummary.text = BuildItemSummary("Black List", workingRule.ItemRule.BlackList, "None");
		}

		private void SetToggle(string name, bool value) => ruleEditor.Q<Toggle>(name).SetValueWithoutNotify(value);

		private static string BuildItemSummary(string label, IReadOnlyList<ItemDefinition> items, string empty)
		{
			if (items == null || items.Count == 0) return $"{label}: {empty}";
			return $"{label}: {string.Join(", ", items.Where(item => item != null).Select(item => item.name))}";
		}

		private static List<string> EnumNames<T>(IReadOnlyList<T> values)
		{
			List<string> names = new();
			for (int i = 0; i < values.Count; ++i) names.Add(values[i].ToString());
			return names;
		}

		private static void SetListValue<T>(List<T> values, T value, bool enabled)
		{
			if (enabled)
			{
				if (values.Contains(value) == false) values.Add(value);
			}
			else values.Remove(value);
		}

		private void BeginApplyMode(uint presetId)
		{
			if (ruleManager == null || GameContext.HasInstance == false || GameContext.Instance.InteractionCtx == null) return;
			EndApplyMode();
			GameContext.Instance.InteractionCtx.ExitBuildingMode();
			applyingRuleId = presetId;
			applyModeActive = true;
			GameContext.Instance.InteractionCtx.OnItemSelected += OnApplyTargetSelected;
			window.Close();
		}

		private void EndApplyMode()
		{
			if (applyModeActive == false) return;
			if (GameContext.HasInstance && GameContext.Instance.InteractionCtx != null)
				GameContext.Instance.InteractionCtx.OnItemSelected -= OnApplyTargetSelected;
			applyModeActive = false;
			applyingRuleId = FacilityRuleManager.NoRulePresetId;
		}

		private void OnApplyTargetSelected(GameObject selectedObject)
		{
			if (TryGetFacility(selectedObject, out IFacility facility) == false)
			{
				GameContext.Instance.FloatingTextManager?.ShowScreen(FloatingTextPreset.Error, "Select a Facility", Input.mousePosition);
				return;
			}
			if (ruleManager.ApplyPreset(facility, applyingRuleId))
				Debug.Log($"Applied Facility Rule preset {applyingRuleId} to {selectedObject.name}.");
		}

		private static bool TryGetFacility(GameObject selectedObject, out IFacility facility)
		{
			facility = null;
			if (selectedObject == null) return false;
			foreach (Component component in selectedObject.GetComponents<Component>())
			{
				if (component is not IFacility found) continue;
				facility = found;
				return true;
			}
			return false;
		}

		private static string BuildRuleSummary(FacilityRule rule)
		{
			if (rule == null || rule.IsEmpty) return "No restrictions";
			List<string> parts = new();
			if (rule.ItemRule != null && rule.ItemRule.IsEmpty == false) parts.Add("Item");
			if (rule.WorkerRule != null && rule.WorkerRule.IsEmpty == false) parts.Add("Worker");
			if (rule.ManifestRule != null && rule.ManifestRule.IsEmpty == false) parts.Add("Destination");
			return string.Join(" · ", parts);
		}

		private void OnMoneyChanged(int _) => DisplaySelectedSection();

		private BuildingFootprintPreset GetSelectedPreset() => footprintField.index >= 0 && footprintField.index < footprintPresets.Count ? footprintPresets[footprintField.index] : footprintService?.ActivePreset;
		private BuildingType GetSelectedBuildingType() => buildingTypeField.index >= 0 && buildingTypeField.index < BuildingTypes.Length ? BuildingTypes[buildingTypeField.index] : BuildingTypes[0];
		private static List<string> BuildTypeChoices()
		{
			List<string> choices = new();
			foreach (BuildingType type in BuildingTypes) choices.Add(BuildingTypeUtility.ToDisplayString(type));
			return choices;
		}
		private static string GetPlaceableName(PlaceableDefinition definition) => string.IsNullOrWhiteSpace(definition.displayName) ? definition.name : definition.displayName;
		private static string GetSectionName(BuildPlaceableSection section) => string.IsNullOrWhiteSpace(section.displayName) ? section.sectionId : section.displayName;
		private static string GetEnvironmentName(PlacementEnvironmentRequirement environment)
		{
			bool indoor = environment.HasFlag(PlacementEnvironmentRequirement.Indoor);
			bool outdoor = environment.HasFlag(PlacementEnvironmentRequirement.Outdoor);
			if (indoor && outdoor) return "Any";
			if (indoor) return "Indoor";
			if (outdoor) return "Outdoor";
			return "Restricted";
		}
		private static bool ContainsSection(IReadOnlyList<BuildPlaceableSection> sections, BuildPlaceableSection target)
		{
			if (sections == null || target == null) return false;
			for (int i = 0; i < sections.Count; ++i) if (sections[i] == target) return true;
			return false;
		}
		private static BuildPlaceableSection FirstSection(IReadOnlyList<BuildPlaceableSection> sections)
		{
			if (sections == null) return null;
			for (int i = 0; i < sections.Count; ++i) if (sections[i] != null) return sections[i];
			return null;
		}
	}
}
