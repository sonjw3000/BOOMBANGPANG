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
		private Building targetBuilding;
		private bool initialized;
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

			if (addonService == null || buildingManager == null || economyService == null)
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

			if (addonService != null)
			{
				addonService.OnAddonInstalled += OnAddonChanged;
				addonService.OnAddonRemoved += OnAddonChanged;
			}

			if (economyService != null)
				economyService.OnMoneyChanged += OnMoneyChanged;
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

			addonService = null;
			buildingManager = null;
			economyService = null;
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
			for (int i = 0; i < definitions.Count; ++i)
			{
				BuildingAddonDefinition definition = definitions[i];
				if (definition == null || definition.IsAllowedFor(targetBuilding.Type) == false)
					continue;

				catalogList.Add(CreateCatalogRow(definition));
				compatibleCount += 1;
			}

			emptyLabel.style.display = compatibleCount == 0 ? DisplayStyle.Flex : DisplayStyle.None;
		}

		private VisualElement CreateCatalogRow(BuildingAddonDefinition definition)
		{
			TemplateContainer row = rowTemplate.CloneTree();
			VisualElement rowRoot = row.Q<VisualElement>("addon-catalog-row");
			VisualElement thumbnail = row.Q<VisualElement>("addon-catalog-row-thumbnail");
			Label fallback = row.Q<Label>("addon-catalog-row-thumbnail-fallback");
			Label name = row.Q<Label>("addon-catalog-row-name");
			Label function = row.Q<Label>("addon-catalog-row-function");
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
				_ => "Adds a building-level operational function.",
			};
		}

		private static string BuildOutputSummary(BuildingAddonDefinition definition)
		{
			return definition.AddonType switch
			{
				BuildingAddonType.OxygenSupply => $"O₂  {definition.OxygenSupplyPerTick:0.##}/tick",
				_ => "Output  —",
			};
		}

		private static string BuildThumbnailFallback(BuildingAddonDefinition definition)
		{
			return definition.AddonType switch
			{
				BuildingAddonType.OxygenSupply => "O₂",
				_ => "+",
			};
		}
	}
}
