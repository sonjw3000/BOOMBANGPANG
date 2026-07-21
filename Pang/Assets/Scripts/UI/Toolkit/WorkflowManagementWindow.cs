using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace UniverseLogistics.UI.Toolkit
{
	public sealed class WorkflowManagementWindow : MonoBehaviour
	{
		private const string SelectedTabClass = "workflow-tab-button--selected";
		private static readonly CollectingPolicyType[] CollectingPolicies =
		{
			CollectingPolicyType.Nearest,
			CollectingPolicyType.LargestQuantityNearest,
		};
		private static readonly PlacingPolicyType[] PlacingPolicies =
		{
			PlacingPolicyType.BelowAverageFilledNearest,
			PlacingPolicyType.Nearest,
		};
		private static readonly PickingPolicyType[] PickingPolicies =
		{
			PickingPolicyType.ManualShelfScan,
			PickingPolicyType.InventoryGuided,
		};

		private UIWindow window;
		private VisualTreeAsset contentTemplate;
		private VisualTreeAsset landingAreaRowTemplate;
		private BuildManagementWindow buildManagementWindow;
		private Button inboundButton;
		private Button outboundButton;
		private VisualElement inboundTab;
		private VisualElement outboundTab;
		private Button addLandingAreaButton;
		private Button linkLandingAreaButton;
		private ScrollView landingAreaList;
		private Label landingAreaEmpty;
		private Label unloadingDestinationSummary;
		private DropdownField collectingPolicyField;
		private DropdownField placingPolicyField;
		private VisualElement pickingPolicyControl;
		private DropdownField pickingPolicyField;
		private Button linkOutboundDestinationButton;
		private Label loadingDestinationSummary;
		private Label messageLabel;
		private AreaManager areaManager;
		private BuildingManager buildingManager;
		private InboundWorkflowService inboundWorkflow;
		private OutboundWorkflowService outboundWorkflow;
		private ResearchService researchService;
		private bool initialized;
		private bool started;

		public void Configure(UIWindow targetWindow, VisualTreeAsset targetContentTemplate,
			VisualTreeAsset targetLandingAreaRowTemplate, BuildManagementWindow targetBuildManagementWindow)
		{
			window = targetWindow;
			contentTemplate = targetContentTemplate;
			landingAreaRowTemplate = targetLandingAreaRowTemplate;
			buildManagementWindow = targetBuildManagementWindow;
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

		private void OnDisable()
		{
			UnbindControls();
			UnbindServices();
			initialized = false;
		}

		public void Open()
		{
			if (InitializeView() == false) return;
			if (areaManager == null) BindServices();
			RefreshAll();
			window.Open();
		}

		private bool InitializeView()
		{
			if (initialized) return true;
			if (window == null || contentTemplate == null || landingAreaRowTemplate == null || window.Initialize() == false)
			{
				Debug.LogError("[WorkflowManagementWindow] Window or templates are missing.", this);
				return false;
			}

			TemplateContainer content = contentTemplate.CloneTree();
			inboundButton = content.Q<Button>("workflow-inbound-button");
			outboundButton = content.Q<Button>("workflow-outbound-button");
			inboundTab = content.Q<VisualElement>("workflow-inbound-tab");
			outboundTab = content.Q<VisualElement>("workflow-outbound-tab");
			addLandingAreaButton = content.Q<Button>("workflow-add-landing-area-button");
			linkLandingAreaButton = content.Q<Button>("workflow-link-landing-area-button");
			landingAreaList = content.Q<ScrollView>("workflow-landing-area-list");
			landingAreaEmpty = content.Q<Label>("workflow-landing-area-empty");
			unloadingDestinationSummary = content.Q<Label>("workflow-unloading-destination");
			collectingPolicyField = content.Q<DropdownField>("workflow-collecting-policy");
			placingPolicyField = content.Q<DropdownField>("workflow-placing-policy");
			pickingPolicyControl = content.Q<VisualElement>("workflow-picking-policy-control");
			pickingPolicyField = content.Q<DropdownField>("workflow-picking-policy");
			linkOutboundDestinationButton = content.Q<Button>("workflow-link-outbound-destination-button");
			loadingDestinationSummary = content.Q<Label>("workflow-loading-destination");
			messageLabel = content.Q<Label>("workflow-message");

			if (inboundButton == null || outboundButton == null || inboundTab == null || outboundTab == null ||
				addLandingAreaButton == null || linkLandingAreaButton == null || landingAreaList == null ||
				landingAreaEmpty == null || unloadingDestinationSummary == null || collectingPolicyField == null ||
				placingPolicyField == null || pickingPolicyControl == null || pickingPolicyField == null ||
				linkOutboundDestinationButton == null || loadingDestinationSummary == null ||
				messageLabel == null)
			{
				Debug.LogError("[WorkflowManagementWindow] Required UXML elements are missing.", this);
				return false;
			}

			collectingPolicyField.choices = BuildCollectingPolicyChoices();
			placingPolicyField.choices = BuildPlacingPolicyChoices();
			pickingPolicyField.choices = BuildPickingPolicyChoices();
			collectingPolicyField.SetTooltip(UITooltipContent.DescriptionOnly(
				"Capsule collecting",
				"Choose whether storing workers prioritize the nearest inbound capsule or the largest movable item quantity."));
			placingPolicyField.SetTooltip(UITooltipContent.DescriptionOnly(
				"Shelf placing",
				"Choose how storing workers select a destination shelf for carried items."));
			pickingPolicyControl.SetTooltip(BuildPickingPolicyTooltip);
			window.SetTitle("Workflow Management");
			window.SetContent(content);
			inboundButton.clicked += OpenInbound;
			outboundButton.clicked += OpenOutbound;
			addLandingAreaButton.clicked += BeginLandingAreaCreation;
			linkLandingAreaButton.clicked += BeginLandingDestinationLink;
			linkOutboundDestinationButton.clicked += BeginOutboundDestinationLink;
			collectingPolicyField.RegisterValueChangedCallback(OnCollectingPolicyChanged);
			placingPolicyField.RegisterValueChangedCallback(OnPlacingPolicyChanged);
			pickingPolicyField.RegisterValueChangedCallback(OnPickingPolicyChanged);
			initialized = true;
			SelectTab(true);
			return true;
		}

		private void UnbindControls()
		{
			if (inboundButton != null) inboundButton.clicked -= OpenInbound;
			if (outboundButton != null) outboundButton.clicked -= OpenOutbound;
			if (addLandingAreaButton != null) addLandingAreaButton.clicked -= BeginLandingAreaCreation;
			if (linkLandingAreaButton != null) linkLandingAreaButton.clicked -= BeginLandingDestinationLink;
			if (linkOutboundDestinationButton != null) linkOutboundDestinationButton.clicked -= BeginOutboundDestinationLink;
			collectingPolicyField?.UnregisterValueChangedCallback(OnCollectingPolicyChanged);
			placingPolicyField?.UnregisterValueChangedCallback(OnPlacingPolicyChanged);
			pickingPolicyField?.UnregisterValueChangedCallback(OnPickingPolicyChanged);
		}

		private void BindServices()
		{
			UnbindServices();
			if (GameContext.HasInstance == false) return;
			areaManager = GameContext.Instance.AreaMgr;
			buildingManager = GameContext.Instance.BuildingMgr;
			inboundWorkflow = GameContext.Instance.IBWorkflowSvc;
			outboundWorkflow = GameContext.Instance.OBWorkflowSvc;
			researchService = GameContext.Instance.ResearchService;
			if (areaManager != null)
			{
				areaManager.OnAreaAdded += OnAreaChanged;
				areaManager.OnAreaRemoved += OnAreaChanged;
				areaManager.OnAreaChanged += OnAreaChanged;
				areaManager.OnAreasRebuilt += RefreshLandingAreas;
			}
			if (researchService != null)
				researchService.OnResearchStateChanged += RefreshPolicies;
		}

		private void UnbindServices()
		{
			if (areaManager != null)
			{
				areaManager.OnAreaAdded -= OnAreaChanged;
				areaManager.OnAreaRemoved -= OnAreaChanged;
				areaManager.OnAreaChanged -= OnAreaChanged;
				areaManager.OnAreasRebuilt -= RefreshLandingAreas;
			}
			if (researchService != null)
				researchService.OnResearchStateChanged -= RefreshPolicies;
			areaManager = null;
			buildingManager = null;
			inboundWorkflow = null;
			outboundWorkflow = null;
			researchService = null;
		}

		private void OpenInbound() => SelectTab(true);
		private void OpenOutbound() => SelectTab(false);

		private void SelectTab(bool inbound)
		{
			inboundTab.style.display = inbound ? DisplayStyle.Flex : DisplayStyle.None;
			outboundTab.style.display = inbound ? DisplayStyle.None : DisplayStyle.Flex;
			inboundButton.EnableInClassList(SelectedTabClass, inbound);
			outboundButton.EnableInClassList(SelectedTabClass, inbound == false);
			messageLabel.text = string.Empty;
		}

		private void RefreshAll()
		{
			RefreshLandingAreas();
			RefreshDestinations();
			RefreshPolicies();
		}

		private void RefreshLandingAreas()
		{
			if (landingAreaList == null || landingAreaEmpty == null) return;
			landingAreaList.Clear();
			int count = 0;
			IReadOnlyList<Area> areas = areaManager?.RegisteredAreas;
			if (areas != null)
			{
				for (int i = 0; i < areas.Count; ++i)
				{
					Area area = areas[i];
					if (area == null || area.Type != AreaType.RocketLanding) continue;
					landingAreaList.Add(CreateLandingAreaRow(area));
					count += 1;
				}
			}

			landingAreaEmpty.style.display = count == 0 ? DisplayStyle.Flex : DisplayStyle.None;
			linkLandingAreaButton.SetEnabled(count > 0 && buildManagementWindow != null);
			RefreshInboundDestinationSummary();
		}

		private VisualElement CreateLandingAreaRow(Area area)
		{
			TemplateContainer row = landingAreaRowTemplate.CloneTree();
			row.Q<Label>("workflow-area-name").text = area.DisplayName;
			RectInt bounds = area.Bounds;
			row.Q<Label>("workflow-area-bounds").text = $"{bounds.width} × {bounds.height} · ({bounds.xMin}, {bounds.yMin})";
			row.Q<Label>("workflow-area-floor").text = $"Floor {area.Floor}";
			row.Q<Label>("workflow-area-destination").text = area.DestinationBuildingId != 0 && buildingManager != null &&
				buildingManager.TryGetBuilding(area.DestinationBuildingId, out Building destination) && destination != null
				? $"→ {destination.DisplayName}"
				: "Not linked";
			Button removeButton = row.Q<Button>("workflow-area-remove");
			removeButton.clicked += () => RemoveLandingArea(area);
			return row;
		}

		private void RefreshDestinations()
		{
			RefreshInboundDestinationSummary();

			if (loadingDestinationSummary != null)
			{
				loadingDestinationSummary.text = outboundWorkflow != null &&
					outboundWorkflow.TryGetLoadingDestinationBuilding(out Building outboundBuilding) && outboundBuilding != null
					? $"Outbound loading → {outboundBuilding.DisplayName}"
					: "Automatic · nearest valid launch destination";
			}
		}

		private void RefreshInboundDestinationSummary()
		{
			if (unloadingDestinationSummary == null) return;
			int total = 0;
			int linked = 0;
			IReadOnlyList<Area> areas = areaManager?.RegisteredAreas;
			if (areas != null)
			{
				for (int i = 0; i < areas.Count; ++i)
				{
					Area area = areas[i];
					if (area == null || area.Type != AreaType.RocketLanding) continue;
					total += 1;
					if (area.DestinationBuildingId != 0) linked += 1;
				}
			}
			unloadingDestinationSummary.text = $"{linked} / {total} Landing Areas linked";
		}

		private void RefreshPolicies()
		{
			if (inboundWorkflow != null)
			{
				int collectingIndex = Array.IndexOf(CollectingPolicies, inboundWorkflow.StoringCollectingPolicyType);
				int placingIndex = Array.IndexOf(PlacingPolicies, inboundWorkflow.StoringPlacingPolicyType);
				collectingPolicyField.SetValueWithoutNotify(collectingPolicyField.choices[Mathf.Max(0, collectingIndex)]);
				placingPolicyField.SetValueWithoutNotify(placingPolicyField.choices[Mathf.Max(0, placingIndex)]);
			}

			if (outboundWorkflow != null)
			{
				int pickingIndex = Array.IndexOf(PickingPolicies, outboundWorkflow.PickingPolicyType);
				pickingPolicyField.SetValueWithoutNotify(pickingPolicyField.choices[Mathf.Max(0, pickingIndex)]);
				pickingPolicyField.SetEnabled(outboundWorkflow.CanUsePickingPolicy(PickingPolicyType.InventoryGuided));
			}
		}

		private void BeginLandingAreaCreation()
		{
			AreaOverlayController overlay = null;
			if (areaManager != null) areaManager.TryGetComponent(out overlay);
			overlay ??= FindAnyObjectByType<AreaOverlayController>(FindObjectsInactive.Include);
			if (overlay == null)
			{
				messageLabel.text = "Landing Area placement is unavailable.";
				return;
			}

			window.Close();
			overlay.BeginCreateOneShot(AreaType.RocketLanding, 0);
		}

		private void RemoveLandingArea(Area area)
		{
			if (areaManager == null || areaManager.RemoveArea(area) == false)
				messageLabel.text = "Unable to remove the Landing Area.";
		}

		private void BeginLandingDestinationLink()
		{
			if (buildManagementWindow == null)
			{
				messageLabel.text = "Build Routing is unavailable.";
				return;
			}

			window.Close();
			buildManagementWindow.OpenRoutingAndBeginInboundDestinationSelection();
		}

		private void BeginOutboundDestinationLink()
		{
			if (buildManagementWindow == null)
			{
				messageLabel.text = "Build Routing is unavailable.";
				return;
			}

			window.Close();
			buildManagementWindow.OpenRoutingAndBeginOutboundDestinationSelection();
		}

		private void OnCollectingPolicyChanged(ChangeEvent<string> evt)
		{
			int index = collectingPolicyField.index;
			if (inboundWorkflow != null && index >= 0 && index < CollectingPolicies.Length)
				inboundWorkflow.SetStoringCollectingPolicy(CollectingPolicies[index]);
		}

		private void OnPlacingPolicyChanged(ChangeEvent<string> evt)
		{
			int index = placingPolicyField.index;
			if (inboundWorkflow != null && index >= 0 && index < PlacingPolicies.Length)
				inboundWorkflow.SetStoringPlacingPolicy(PlacingPolicies[index]);
		}

		private void OnPickingPolicyChanged(ChangeEvent<string> evt)
		{
			int index = pickingPolicyField.index;
			if (outboundWorkflow == null || index < 0 || index >= PickingPolicies.Length)
				return;

			if (outboundWorkflow.TrySetPickingPolicy(PickingPolicies[index]))
			{
				messageLabel.text = string.Empty;
				return;
			}

			messageLabel.text = "Inventory Digitization research is required.";
			RefreshPolicies();
		}

		private void OnAreaChanged(Area area)
		{
			RefreshLandingAreas();
		}

		private static List<string> BuildCollectingPolicyChoices() => new()
		{
			"Nearest",
			"Largest quantity + nearest",
		};

		private static List<string> BuildPlacingPolicyChoices() => new()
		{
			"Below average filled + nearest",
			"Nearest",
		};

		private static List<string> BuildPickingPolicyChoices() => new()
		{
			"Manual shelf scan",
			"Inventory-guided",
		};

		private UITooltipContent BuildPickingPolicyTooltip()
		{
			const string title = "Picking method";
			const string description = "Manual workers search nearby shelves. Inventory-guided workers receive the exact source shelf.";
			return outboundWorkflow?.CanUsePickingPolicy(PickingPolicyType.InventoryGuided) == true
				? UITooltipContent.DescriptionOnly(title, description)
				: UITooltipContent.Locked(title, description, "Required research: Inventory Digitization");
		}
	}
}
