using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace UniverseLogistics.UI.Toolkit
{
	public sealed class BuildingAddonCatalogWindow : MonoBehaviour
	{
		private UIWindow window;
		private VisualTreeAsset contentTemplate;
		private VisualTreeAsset rowTemplate;
		private Label buildingName;
		private Label slotSummary;
		private Label moneySummary;
		private Label message;
		private ScrollView catalogList;
		private Label emptyLabel;
		private BuildingAddonService addonService;
		private BuildingManager buildingManager;
		private EconomyService economyService;
		private ResearchService researchService;
		private Building targetBuilding;
		[System.NonSerialized] private bool initialized;
		private bool started;
		private bool installing;

		public void Configure(
			UIWindow targetWindow,
			VisualTreeAsset targetContentTemplate,
			VisualTreeAsset targetRowTemplate)
		{
			window = targetWindow;
			contentTemplate = targetContentTemplate;
			rowTemplate = targetRowTemplate;
		}

		private void OnEnable()
		{
			InitializeView();
			if (started)
				BindServices();
		}

		private void Start()
		{
			started = true;
			BindServices();
		}

		private void Update()
		{
			if (window == null || window.IsOpen == false || targetBuilding == null)
				return;

			if (TryValidateTarget(out _) == false)
				window.Close();
		}

		private void OnDisable()
		{
			UnbindControls();
			UnbindServices();
			targetBuilding = null;
			initialized = false;
		}

		public bool Open(Building building)
		{
			if (InitializeView() == false)
				return false;

			if (addonService == null || buildingManager == null || economyService == null || researchService == null)
				BindServices();

			targetBuilding = building;
			if (TryValidateTarget(out string reason) == false)
			{
				targetBuilding = null;
				Debug.LogWarning($"[BuildingAddonCatalogWindow] {reason}", this);
				return false;
			}

			message.text = "Choose an add-on to install in the available slot.";
			window.SetTitle($"Add-on Catalog · {targetBuilding.DisplayName}");
			RefreshAll();
			window.Open();
			return true;
		}

		private bool InitializeView()
		{
			if (initialized)
				return true;

			if (window == null || contentTemplate == null || rowTemplate == null || window.Initialize() == false)
			{
				Debug.LogError("[BuildingAddonCatalogWindow] Window or templates are missing.", this);
				return false;
			}

			TemplateContainer content = contentTemplate.CloneTree();
			buildingName = content.Q<Label>("addon-catalog-building-name");
			slotSummary = content.Q<Label>("addon-catalog-slot-summary");
			moneySummary = content.Q<Label>("addon-catalog-money-summary");
			message = content.Q<Label>("addon-catalog-message");
			catalogList = content.Q<ScrollView>("addon-catalog-list");
			emptyLabel = content.Q<Label>("addon-catalog-empty");

			if (buildingName == null || slotSummary == null || moneySummary == null ||
				message == null || catalogList == null || emptyLabel == null)
			{
				Debug.LogError("[BuildingAddonCatalogWindow] Required UXML elements are missing.", this);
				return false;
			}

			window.SetTitle("Add-on Catalog");
			window.SetContent(content);
			window.Closed += OnWindowClosed;
			initialized = true;
			return true;
		}

		private void UnbindControls()
		{
			if (window != null)
				window.Closed -= OnWindowClosed;
		}

		private void BindServices()
		{
			UnbindServices();
			if (GameContext.HasInstance == false)
				return;

			GameContext context = GameContext.Instance;
			addonService = context.BuildingAddonSvc;
			buildingManager = context.BuildingMgr;
			economyService = context.EconomyService;
			researchService = context.ResearchService;

			if (addonService != null)
			{
				addonService.OnAddonInstalled += OnAddonChanged;
				addonService.OnAddonRemoved += OnAddonChanged;
			}

			if (economyService != null)
				economyService.OnMoneyChanged += OnMoneyChanged;

			if (researchService != null)
				researchService.OnResearchStateChanged += OnResearchStateChanged;
		}

		private void UnbindServices()
		{
			if (addonService != null)
			{
				addonService.OnAddonInstalled -= OnAddonChanged;
				addonService.OnAddonRemoved -= OnAddonChanged;
			}

			if (economyService != null)
				economyService.OnMoneyChanged -= OnMoneyChanged;

			if (researchService != null)
				researchService.OnResearchStateChanged -= OnResearchStateChanged;

			addonService = null;
			buildingManager = null;
			economyService = null;
			researchService = null;
		}

		private void OnWindowClosed()
		{
			targetBuilding = null;
		}

		private void OnAddonChanged(Building building, BuildingAddon _)
		{
			if (installing == false &&
				ReferenceEquals(building, targetBuilding) &&
				window != null &&
				window.IsOpen)
			{
				RefreshAll();
			}
		}

		private void OnMoneyChanged(int _)
		{
			if (installing == false && window != null && window.IsOpen)
				RefreshAll();
		}

		private void OnResearchStateChanged()
		{
			if (installing == false && window != null && window.IsOpen)
				RefreshAll();
		}

		private void RefreshAll()
		{
			if (catalogList == null)
				return;

			if (TryValidateTarget(out string reason) == false)
			{
				message.text = reason;
				catalogList.Clear();
				emptyLabel.style.display = DisplayStyle.Flex;
				return;
			}

			buildingName.text = targetBuilding.DisplayName;
			slotSummary.text =
				$"{targetBuilding.InstalledAddons.Count} / {targetBuilding.AddonSlotCapacity} slots used · " +
				$"{targetBuilding.AvailableAddonSlots} free";
			moneySummary.text = $"Available funds  ${economyService.Money:N0}";

			catalogList.Clear();
			int compatibleCount = 0;
			IReadOnlyList<BuildingAddonDefinition> definitions = addonService.Definitions;
			compatibleCount += AppendCategory(
				definitions,
				BuildingAddonCategory.LifeSupport,
				"LIFE SUPPORT");
			compatibleCount += AppendCategory(
				definitions,
				BuildingAddonCategory.ClimateControl,
				"CLIMATE CONTROL");

			emptyLabel.style.display = compatibleCount == 0 ? DisplayStyle.Flex : DisplayStyle.None;
		}

		private int AppendCategory(
			IReadOnlyList<BuildingAddonDefinition> definitions,
			BuildingAddonCategory category,
			string heading)
		{
			if (definitions == null)
				return 0;

			int categoryCount = 0;
			for (int i = 0; i < definitions.Count; ++i)
			{
				BuildingAddonDefinition definition = definitions[i];
				if (definition == null ||
					definition.Category != category)
				{
					continue;
				}

				if (categoryCount == 0)
					catalogList.Add(CreateCategoryHeading(category, heading));

				catalogList.Add(CreateCatalogRow(definition));
				categoryCount += 1;
			}

			return categoryCount;
		}

		private static Label CreateCategoryHeading(BuildingAddonCategory category, string heading)
		{
			Label label = new(heading);
			label.AddToClassList("addon-catalog-category");
			label.EnableInClassList(
				"addon-catalog-category--climate",
				category == BuildingAddonCategory.ClimateControl);
			return label;
		}

		private VisualElement CreateCatalogRow(BuildingAddonDefinition definition)
		{
			TemplateContainer row = rowTemplate.CloneTree();
			VisualElement rowRoot = row.Q<VisualElement>("addon-catalog-row");
			VisualElement thumbnail = row.Q<VisualElement>("addon-catalog-row-thumbnail");
			Label fallback = row.Q<Label>("addon-catalog-row-thumbnail-fallback");
			Label name = row.Q<Label>("addon-catalog-row-name");
			Label function = row.Q<Label>("addon-catalog-row-function");
			Label research = row.Q<Label>("addon-catalog-row-research");
			Label power = row.Q<Label>("addon-catalog-row-power");
			Label output = row.Q<Label>("addon-catalog-row-output");
			Label cost = row.Q<Label>("addon-catalog-row-cost");
			Label unavailable = row.Q<Label>("addon-catalog-row-unavailable");
			Button installButton = row.Q<Button>("addon-catalog-row-install");

			if (definition.Icon != null)
			{
				thumbnail.style.backgroundImage = new StyleBackground(definition.Icon);
				fallback.style.display = DisplayStyle.None;
			}
			else
			{
				thumbnail.style.backgroundImage = StyleKeyword.None;
				fallback.text = BuildThumbnailFallback(definition);
				fallback.style.display = DisplayStyle.Flex;
			}

			name.text = definition.DisplayName;
			function.text = BuildFunctionDescription(definition);
			bool requiresResearch = string.IsNullOrWhiteSpace(definition.RequiredResearchUid) == false;
			bool researchUnlocked =
				requiresResearch == false ||
				researchService?.IsResearched(definition.RequiredResearchUid) == true;
			research.text = requiresResearch
				? $"Research  {GetResearchDisplayName(definition.RequiredResearchUid)} · " +
					$"{(researchUnlocked ? "Unlocked" : "Required")}"
				: "Research  None";
			research.EnableInClassList(
				"addon-catalog-row__research--required",
				requiresResearch && researchUnlocked == false);
			power.text = $"Power  {definition.PowerConsumption:N0}";
			output.text = BuildOutputSummary(definition);
			cost.text = $"${definition.Cost:N0}";

			bool canInstall = addonService.CanInstall(targetBuilding, definition, out string reason);
			installButton.SetEnabled(canInstall);
			unavailable.text = canInstall ? "Ready to install" : reason;
			unavailable.EnableInClassList("addon-catalog-row__availability--ready", canInstall);
			rowRoot.EnableInClassList("addon-catalog-row--unavailable", canInstall == false);
			installButton.clicked += () => Install(definition);
			return row;
		}

		private void Install(BuildingAddonDefinition definition)
		{
			if (TryValidateTarget(out string validationReason) == false)
			{
				message.text = validationReason;
				window.Close();
				return;
			}

			bool installed = false;
			string reason = string.Empty;
			installing = true;
			try
			{
				installed = addonService.TryInstall(targetBuilding, definition, out reason);
			}
			finally
			{
				installing = false;
			}

			if (installed == false)
			{
				message.text = string.IsNullOrWhiteSpace(reason) ? "Installation failed." : reason;
				RefreshAll();
				return;
			}

			window.Close();
		}

		private bool TryValidateTarget(out string reason)
		{
			if (targetBuilding == null)
			{
				reason = "The target building is unavailable.";
				return false;
			}

			if (addonService == null || buildingManager == null || economyService == null)
			{
				reason = "Add-on services are unavailable.";
				return false;
			}

			if (targetBuilding.RuntimeBuildingId == 0 ||
				buildingManager.TryGetBuilding(targetBuilding.RuntimeBuildingId, out Building registered) == false ||
				ReferenceEquals(registered, targetBuilding) == false)
			{
				reason = "The target building is no longer registered.";
				return false;
			}

			reason = string.Empty;
			return true;
		}

		private static string BuildFunctionDescription(BuildingAddonDefinition definition)
		{
			return definition.AddonType switch
			{
				BuildingAddonType.OxygenSupply => "Supplies breathable air to this building.",
				BuildingAddonType.TemperatureControl =>
					$"Target range {FormatTemperatureRange(definition)} · {BuildDirectionDescription(definition)}",
				_ => "Adds a building-level operational function.",
			};
		}

		private static string BuildOutputSummary(BuildingAddonDefinition definition)
		{
			return definition.AddonType switch
			{
				BuildingAddonType.OxygenSupply => $"O₂  {definition.OxygenSupplyPerTick:0.##}/tick",
				BuildingAddonType.TemperatureControl =>
					$"Control  {definition.TemperatureControlDegreesPerQuarterWeek:0.##} °C/quarter-week",
				_ => "Output  —",
			};
		}

		private static string BuildThumbnailFallback(BuildingAddonDefinition definition)
		{
			return definition.AddonType switch
			{
				BuildingAddonType.OxygenSupply => "O₂",
				BuildingAddonType.TemperatureControl => "°C",
				_ => "+",
			};
		}

		private static string FormatTemperatureRange(BuildingAddonDefinition definition)
		{
			return
				$"{definition.MinimumTargetTemperatureCelsius:0.#}–" +
				$"{definition.MaximumTargetTemperatureCelsius:0.#} °C";
		}

		private static string BuildDirectionDescription(BuildingAddonDefinition definition)
		{
			if (definition.CanCool && definition.CanHeat)
				return "Cooling + Heating";
			if (definition.CanCool)
				return "Cooling";
			if (definition.CanHeat)
				return "Heating";
			return "No temperature control";
		}

		private string GetResearchDisplayName(string researchUid)
		{
			if (researchService?.Catalog != null &&
				researchService.Catalog.TryGet(researchUid, out ResearchDefinition definition) &&
				string.IsNullOrWhiteSpace(definition.DisplayName) == false)
			{
				return definition.DisplayName;
			}

			if (string.IsNullOrWhiteSpace(researchUid))
				return "None";

			string[] words = researchUid.Split('_');
			for (int i = 0; i < words.Length; ++i)
			{
				if (words[i].Length <= 0)
					continue;

				words[i] =
					char.ToUpperInvariant(words[i][0]).ToString() +
					words[i].Substring(1);
			}

			return string.Join(" ", words);
		}
	}
}
