using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.Scripts.UI
{
	public class PolicyControlWindow : MonoBehaviour
	{
		private enum TabType
		{
			Inbound,
			Outbound,
		}

		private sealed class ActionButtonControls
		{
			public Button Button;
			public TMP_Text LabelText;
		}

		private static readonly PlacingPolicyType[] PlacingPolicyOptions =
		{
			PlacingPolicyType.BelowAverageFilledNearest,
			PlacingPolicyType.Nearest,
		};

		private static readonly CollectingPolicyType[] CollectingPolicyOptions =
		{
			CollectingPolicyType.Nearest,
			CollectingPolicyType.LargestQuantityNearest,
		};

		private static Font defaultFont;

		[SerializeField] private UIWindow window;
		[SerializeField] private string windowTitle = "Workflow Control";

		private Dropdown storingCollectingPolicyDropdown;
		private Dropdown placingPolicyDropdown;
		private GameObject inboundTabRoot;
		private GameObject outboundTabRoot;
		private TMP_Text unloadingDestinationSummaryText;
		private TMP_Text unloadingSelectionStatusText;
		private TMP_Text outboundPlaceholderText;
		private ActionButtonControls landingZoneButton;
		private ActionButtonControls unloadingDestinationButton;
		private TabType currentTab;
		private bool initialized;
		private bool isSelectingUnloadingDestination;
		private string unloadingSelectionStatusMessage = string.Empty;

		private readonly List<GameObject> overlayObjects = new();
		private GameObject overlayRoot;

		private InteractionContext Interaction => GameContext.HasInstance ? GameContext.Instance.InteractionCtx : null;
		private InboundWorkflowService InboundWorkflowService => GameContext.HasInstance ? GameContext.Instance.IBWorkflowSvc : null;
		private OutboundWorkflowService OutboundWorkflowService => GameContext.HasInstance ? GameContext.Instance.OBWorkflowSvc : null;
		private BuildingManager BuildingManager => GameContext.HasInstance ? GameContext.Instance.BuildingMgr : null;
		private BuildingFootprintService BuildingFootprintService => GameContext.HasInstance ? GameContext.Instance.BuildingFootprintService : null;
		private CargoPortService CargoPortService => InboundWorkflowService != null ? InboundWorkflowService.CargoPortService : null;

		private ZoneControlWindow zoneControlWindow;

		private void Awake()
		{
			EnsureInitialized();
		}

		private void OnEnable()
		{
			EnsureInitialized();
			EnsureInteractionSubscriptions();
			RefreshFromState();
		}

		private void OnDestroy()
		{
			if (window != null)
			{
				window.Opened -= HandleWindowOpened;
				window.Closed -= HandleWindowClosed;
			}

			ReleaseInteractionSubscriptions();

			ClearOverlay();
			if (overlayRoot != null)
				Destroy(overlayRoot);
		}

		public void ToggleWindow()
		{
			EnsureInitialized();
			if (window == null)
				return;

			bool shouldOpen = gameObject.activeSelf == false || window.IsOpen == false;
			if (shouldOpen)
			{
				EnsureHostActive();
				RefreshFromState();
				window.Open();
			}
			else
			{
				window.Close();
			}
		}

		public void Open()
		{
			EnsureInitialized();
			EnsureHostActive();
			RefreshFromState();
			window?.Open();
		}

		public void Close()
		{
			EnsureInitialized();
			window?.Close();
		}

		private void EnsureInitialized()
		{
			if (initialized)
				return;

			window ??= GetComponent<UIWindow>();
			window ??= GetComponentInChildren<UIWindow>(true);
			if (window == null)
				return;

			window.SetTitle(windowTitle);
			BuildContent();
			SetupTabs();
			SetTab((int)TabType.Inbound);
			window.Opened -= HandleWindowOpened;
			window.Closed -= HandleWindowClosed;
			window.Opened += HandleWindowOpened;
			window.Closed += HandleWindowClosed;
			EnsureInteractionSubscriptions();

			EnsureOverlayRoot();
			window.Close();
			initialized = true;
		}

		private void EnsureInteractionSubscriptions()
		{
			if (Interaction == null)
				return;

			Interaction.OnHandleBuildingLinkSelection -= HandleUnloadingDestinationSelection;
			Interaction.OnHandleBuildingLinkSelection += HandleUnloadingDestinationSelection;
			Interaction.OnModeChanged -= HandleInteractionModeChanged;
			Interaction.OnModeChanged += HandleInteractionModeChanged;
		}

		private void ReleaseInteractionSubscriptions()
		{
			if (Interaction == null)
				return;

			Interaction.OnHandleBuildingLinkSelection -= HandleUnloadingDestinationSelection;
			Interaction.OnModeChanged -= HandleInteractionModeChanged;
		}

		private void HandleWindowOpened()
		{
			RefreshFromState();
		}

		private void HandleWindowClosed()
		{
			EndUnloadingDestinationSelection(true);
		}

		private void HandleInteractionModeChanged(InteractionContext.InteractionDomain domain, InteractionContext.InteractionAction action)
		{
			if (isSelectingUnloadingDestination == false)
				return;

			if (Interaction == null || Interaction.Mode != InteractionContext.InteractionMode.BuildingLinkEdit)
			{
				EndUnloadingDestinationSelection(false);
				return;
			}

			RefreshOverlay();
			UpdateInboundStatusText();
		}

		private bool HandleUnloadingDestinationSelection(Unity.Mathematics.int3 pos)
		{
			if (isSelectingUnloadingDestination == false || Interaction == null || Interaction.Mode != InteractionContext.InteractionMode.BuildingLinkEdit)
				return false;

			if (TryGetBuildingAt(pos, out Building building) == false || building == null)
				return false;

			if (HasInboundPorts(building) == false)
			{
				unloadingSelectionStatusMessage = $"{building.DisplayName} has no inbound cargo ports.";
				UpdateInboundStatusText();
				return true;
			}

			InboundWorkflowService?.SetUnloadingDestinationBuilding(building);
			unloadingSelectionStatusMessage = $"Unloading destination set to {building.DisplayName}.";
			EndUnloadingDestinationSelection(true);
			RefreshFromState();
			return true;
		}

		private void SetupTabs()
		{
			window.ClearTabs();
			window.AddTab("Inbound", SetTab);
			window.AddTab("Outbound", SetTab);
			window.UpdateTabVisuals((int)currentTab);
		}

		private void SetTab(int tabIndex)
		{
			currentTab = (TabType)tabIndex;

			if (inboundTabRoot != null)
				inboundTabRoot.SetActive(currentTab == TabType.Inbound);
			if (outboundTabRoot != null)
				outboundTabRoot.SetActive(currentTab == TabType.Outbound);

			window?.UpdateTabVisuals(tabIndex);
		}

		private void BuildContent()
		{
			RectTransform contentRoot = window.ContentRoot;
			if (contentRoot == null)
				return;

			ClearChildren(contentRoot);

			GameObject container = CreateVerticalContainer("WorkflowControlContent", contentRoot, 12f);
			inboundTabRoot = CreateVerticalContainer("InboundTab", container.transform, 10f);
			outboundTabRoot = CreateVerticalContainer("OutboundTab", container.transform, 10f);

			BuildInboundTab(inboundTabRoot.transform);
			BuildOutboundTab(outboundTabRoot.transform);
		}

		private void BuildInboundTab(Transform parent)
		{
			CreateSectionHeader(parent, "Inbound Routing");
			CreateHelpText(parent, "Configure rocket landing zones and which building receives inbound unloading.");

			landingZoneButton = CreateButtonRow(parent, "Landing Zones", "Edit Landing Zones", HandleLandingZoneButtonClicked);
			unloadingDestinationButton = CreateButtonRow(parent, "Unloading Destination", "Select Building", HandleUnloadingDestinationButtonClicked);
			unloadingDestinationSummaryText = CreateBodyText("UnloadingDestinationSummary", parent, string.Empty);
			unloadingSelectionStatusText = CreateBodyText("UnloadingSelectionStatus", parent, string.Empty);

			CreateSectionHeader(parent, "Storing");
			storingCollectingPolicyDropdown = CreateDropdownRow(parent, "Collecting Policy", HandleStoringCollectingPolicyChanged);
			storingCollectingPolicyDropdown.ClearOptions();
			storingCollectingPolicyDropdown.AddOptions(new List<string>
			{
				GetCollectingPolicyLabel(CollectingPolicyType.Nearest),
				GetCollectingPolicyLabel(CollectingPolicyType.LargestQuantityNearest),
			});

			placingPolicyDropdown = CreateDropdownRow(parent, "Placing Policy", HandlePlacingPolicyChanged);
			placingPolicyDropdown.ClearOptions();
			placingPolicyDropdown.AddOptions(new List<string>
			{
				GetPlacingPolicyLabel(PlacingPolicyType.BelowAverageFilledNearest),
				GetPlacingPolicyLabel(PlacingPolicyType.Nearest),
			});
		}

		private void BuildOutboundTab(Transform parent)
		{
			CreateSectionHeader(parent, "Outbound");
			outboundPlaceholderText = CreateBodyText("OutboundPlaceholder", parent, "Outbound workflow settings will be added here.");
		}

		private void RefreshFromState()
		{
			if (initialized == false)
				return;

			if (InboundWorkflowService != null)
			{
				if (storingCollectingPolicyDropdown != null)
				{
					int collectingIndex = Array.IndexOf(CollectingPolicyOptions, InboundWorkflowService.StoringCollectingPolicyType);
					storingCollectingPolicyDropdown.SetValueWithoutNotify(Mathf.Max(0, collectingIndex));
				}

				if (placingPolicyDropdown != null)
				{
					int placingIndex = Array.IndexOf(PlacingPolicyOptions, InboundWorkflowService.StoringPlacingPolicyType);
					placingPolicyDropdown.SetValueWithoutNotify(Mathf.Max(0, placingIndex));
				}
			}

			if (unloadingDestinationSummaryText != null)
				unloadingDestinationSummaryText.text = BuildUnloadingDestinationSummary();

			UpdateInboundStatusText();

			if (outboundPlaceholderText != null)
				outboundPlaceholderText.text = "Outbound workflow settings will be added here.";
		}

		private void HandleLandingZoneButtonClicked()
		{
			EnsureZoneControlWindow();
			zoneControlWindow?.OpenForGlobalZoneType(ZoneType.RocketLanding);
		}

		private void HandleUnloadingDestinationButtonClicked()
		{
			if (isSelectingUnloadingDestination)
			{
				EndUnloadingDestinationSelection(true);
				return;
			}

			BeginUnloadingDestinationSelection();
		}

		private void BeginUnloadingDestinationSelection()
		{
			if (Interaction == null)
				return;

			isSelectingUnloadingDestination = true;
			unloadingSelectionStatusMessage = "Select a building with inbound cargo ports. Right click to cancel.";
			Interaction.EnterBuildingLinkMode();
			RefreshOverlay();
			UpdateInboundStatusText();
		}

		private void EndUnloadingDestinationSelection(bool exitInteractionMode)
		{
			if (isSelectingUnloadingDestination == false)
				return;

			isSelectingUnloadingDestination = false;
			ClearOverlay();
			if (exitInteractionMode && Interaction != null && Interaction.Mode == InteractionContext.InteractionMode.BuildingLinkEdit)
				Interaction.ExitBuildingLinkMode();

			UpdateInboundStatusText();
		}

		private void UpdateInboundStatusText()
		{
			if (unloadingSelectionStatusText == null)
				return;

			if (isSelectingUnloadingDestination)
			{
				unloadingSelectionStatusText.text = string.IsNullOrWhiteSpace(unloadingSelectionStatusMessage)
					? "Select a building with inbound cargo ports. Right click to cancel."
					: unloadingSelectionStatusMessage;
				return;
			}

			unloadingSelectionStatusText.text = string.IsNullOrWhiteSpace(unloadingSelectionStatusMessage)
				? "Choose which building should receive unloading from landed rockets."
				: unloadingSelectionStatusMessage;
		}

		private void HandlePlacingPolicyChanged(int optionIndex)
		{
			if (InboundWorkflowService == null || optionIndex < 0 || optionIndex >= PlacingPolicyOptions.Length)
				return;

			InboundWorkflowService.SetStoringPlacingPolicy(PlacingPolicyOptions[optionIndex]);
		}

		private void HandleStoringCollectingPolicyChanged(int optionIndex)
		{
			if (InboundWorkflowService == null || optionIndex < 0 || optionIndex >= CollectingPolicyOptions.Length)
				return;

			InboundWorkflowService.SetStoringCollectingPolicy(CollectingPolicyOptions[optionIndex]);
		}

		private string BuildUnloadingDestinationSummary()
		{
			if (InboundWorkflowService == null)
				return "Unloading destination building: unavailable.";

			if (InboundWorkflowService.TryGetUnloadingDestinationBuilding(out Building building) && building != null)
				return $"Unloading destination building: {building.DisplayName}";

			return "Unloading destination building: automatic (nearest valid inbound cargo port)";
		}

		private bool TryGetBuildingAt(Unity.Mathematics.int3 pos, out Building building)
		{
			building = null;
			if (GameContext.HasInstance == false || GameContext.Instance.GridService == null || BuildingManager == null)
				return false;

			GridCell cell = GameContext.Instance.GridService.GetCell(pos);
			if (cell == null || cell.BuildingId == 0)
				return false;

			return BuildingManager.TryGetBuilding(cell.BuildingId, out building) && building != null;
		}

		private bool HasInboundPorts(Building building)
		{
			if (building == null || CargoPortService == null)
				return false;

			List<CargoPort> ports = new();
			return CargoPortService.TryQueryPorts(building.RuntimeBuildingId, ports, port => port != null && port.IsInbound);
		}

		private void EnsureZoneControlWindow()
		{
			if (zoneControlWindow == null)
				zoneControlWindow = FindFirstObjectByType<ZoneControlWindow>(FindObjectsInactive.Include);
		}

		private void EnsureHostActive()
		{
			if (gameObject.activeSelf == false)
				gameObject.SetActive(true);
		}

		private void EnsureOverlayRoot()
		{
			if (overlayRoot != null)
				return;

			overlayRoot = new GameObject("WorkflowControlOverlayRoot");
			Transform parent = GameContext.HasInstance ? GameContext.Instance.transform : transform;
			overlayRoot.transform.SetParent(parent, false);
			overlayRoot.hideFlags = HideFlags.HideInHierarchy;
		}

		private void RefreshOverlay()
		{
			ClearOverlay();
			if (isSelectingUnloadingDestination == false || BuildingManager == null || BuildingFootprintService == null)
				return;

			IReadOnlyList<Building> buildings = BuildingManager.RegisteredBuildings;
			uint selectedBuildingId = InboundWorkflowService != null ? InboundWorkflowService.UnloadingDestinationBuildingId : 0;
			for (int i = 0; i < buildings.Count; ++i)
			{
				Building building = buildings[i];
				if (building == null || HasInboundPorts(building) == false)
					continue;

				bool isSelected = selectedBuildingId != 0 && building.RuntimeBuildingId == selectedBuildingId;
				CreateBuildingMarker(building, isSelected);
			}
		}

		private void CreateBuildingMarker(Building building, bool isSelected)
		{
			if (building == null || BuildingFootprintService.TryGetInteriorBounds(building.RuntimeBuildingId, out RectInt bounds, out _) == false)
				return;

			GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
			quad.name = "WorkflowTargetBuildingMarker";
			quad.transform.SetParent(overlayRoot.transform, false);
			quad.transform.position = new Vector3(
				bounds.xMin + (bounds.width * 0.5f) - 0.5f,
				0.03f,
				bounds.yMin + (bounds.height * 0.5f) - 0.5f);
			quad.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
			quad.transform.localScale = new Vector3(bounds.width, bounds.height, 1f);
			MeshRenderer renderer = quad.GetComponent<MeshRenderer>();
			renderer.material.color = isSelected
				? new Color(0.26f, 0.74f, 0.98f, 0.42f)
				: new Color(0.22f, 0.85f, 0.48f, 0.28f);

			Collider quadCollider = quad.GetComponent<Collider>();
			if (quadCollider != null)
				Destroy(quadCollider);

			overlayObjects.Add(quad);

			GameObject labelObject = new("WorkflowTargetBuildingLabel");
			labelObject.transform.SetParent(overlayRoot.transform, false);
			labelObject.transform.position = new Vector3(
				bounds.xMin + (bounds.width * 0.5f) - 0.5f,
				0.045f,
				bounds.yMin + (bounds.height * 0.5f) - 0.5f);
			labelObject.transform.rotation = Quaternion.Euler(90f, 180f, 0f);
			labelObject.transform.localScale = Vector3.one * 0.32f;

			TextMeshPro label = labelObject.AddComponent<TextMeshPro>();
			label.fontSize = 5f;
			label.alignment = TextAlignmentOptions.Center;
			label.text = building.DisplayName;
			label.color = Color.white;
			if (TMP_Settings.defaultFontAsset != null)
				label.font = TMP_Settings.defaultFontAsset;

			overlayObjects.Add(labelObject);
		}

		private void ClearOverlay()
		{
			for (int i = 0; i < overlayObjects.Count; ++i)
			{
				if (overlayObjects[i] != null)
					Destroy(overlayObjects[i]);
			}

			overlayObjects.Clear();
		}

		private static GameObject CreateVerticalContainer(string objectName, Transform parent, float spacing)
		{
			GameObject container = new(objectName, typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
			container.transform.SetParent(parent, false);

			VerticalLayoutGroup layout = container.GetComponent<VerticalLayoutGroup>();
			layout.spacing = spacing;
			layout.padding = new RectOffset(8, 8, 8, 8);
			layout.childForceExpandHeight = false;
			layout.childForceExpandWidth = true;
			layout.childControlHeight = true;
			layout.childControlWidth = true;

			ContentSizeFitter fitter = container.GetComponent<ContentSizeFitter>();
			fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
			fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
			return container;
		}

		private static TMP_Text CreateSectionHeader(Transform parent, string title)
		{
			TMP_Text text = CreateText($"{title}Header", parent, title);
			text.fontSize = 24f;
			return text;
		}

		private static TMP_Text CreateHelpText(Transform parent, string value)
		{
			TMP_Text text = CreateBodyText("HelpText", parent, value);
			text.fontSize = 18f;
			text.color = new Color(0.85f, 0.85f, 0.85f, 1f);
			return text;
		}

		private static TMP_Text CreateBodyText(string objectName, Transform parent, string value)
		{
			TMP_Text text = CreateText(objectName, parent, value);
			text.fontSize = 19f;
			text.textWrappingMode = TextWrappingModes.Normal;
			text.overflowMode = TextOverflowModes.Overflow;
			text.GetComponent<LayoutElement>().preferredHeight = 46f;
			return text;
		}

		private static TMP_Text CreateText(string objectName, Transform parent, string value)
		{
			GameObject textObject = new(objectName, typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
			textObject.transform.SetParent(parent, false);

			LayoutElement layout = textObject.GetComponent<LayoutElement>();
			layout.preferredHeight = 28f;

			TMP_Text text = textObject.GetComponent<TextMeshProUGUI>();
			text.text = value;
			text.fontSize = 20f;
			text.color = Color.white;
			text.alignment = TextAlignmentOptions.MidlineLeft;
			text.textWrappingMode = TextWrappingModes.NoWrap;
			text.overflowMode = TextOverflowModes.Ellipsis;

			RectTransform rect = text.rectTransform;
			rect.anchorMin = Vector2.zero;
			rect.anchorMax = Vector2.one;
			rect.offsetMin = Vector2.zero;
			rect.offsetMax = Vector2.zero;

			return text;
		}

		private static Dropdown CreateDropdownRow(Transform parent, string label, UnityEngine.Events.UnityAction<int> onChanged)
		{
			GameObject row = new($"{label}Row", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
			row.transform.SetParent(parent, false);

			HorizontalLayoutGroup rowLayout = row.GetComponent<HorizontalLayoutGroup>();
			rowLayout.spacing = 12f;
			rowLayout.childAlignment = TextAnchor.MiddleLeft;
			rowLayout.childControlHeight = true;
			rowLayout.childControlWidth = true;
			rowLayout.childForceExpandWidth = false;
			rowLayout.childForceExpandHeight = false;

			row.GetComponent<LayoutElement>().preferredHeight = 42f;

			TMP_Text labelText = CreateText("Label", row.transform, label);
			labelText.fontSize = 19f;
			labelText.GetComponent<LayoutElement>().preferredWidth = 180f;

			GameObject dropdownObject = new("Dropdown", typeof(RectTransform), typeof(Image), typeof(Dropdown), typeof(LayoutElement));
			dropdownObject.transform.SetParent(row.transform, false);

			Image dropdownImage = dropdownObject.GetComponent<Image>();
			dropdownImage.color = new Color(0.2f, 0.2f, 0.2f, 0.95f);

			LayoutElement dropdownLayout = dropdownObject.GetComponent<LayoutElement>();
			dropdownLayout.preferredHeight = 36f;
			dropdownLayout.preferredWidth = 320f;

			Dropdown dropdown = dropdownObject.GetComponent<Dropdown>();

			Text captionText = CreateLegacyText("Label", dropdownObject.transform, "Option");
			captionText.alignment = TextAnchor.MiddleLeft;
			captionText.rectTransform.offsetMin = new Vector2(10f, 0f);
			captionText.rectTransform.offsetMax = new Vector2(-30f, 0f);

			Text arrowText = CreateLegacyText("Arrow", dropdownObject.transform, "v");
			arrowText.alignment = TextAnchor.MiddleCenter;
			arrowText.rectTransform.anchorMin = new Vector2(1f, 0f);
			arrowText.rectTransform.anchorMax = new Vector2(1f, 1f);
			arrowText.rectTransform.pivot = new Vector2(1f, 0.5f);
			arrowText.rectTransform.sizeDelta = new Vector2(24f, 0f);
			arrowText.rectTransform.anchoredPosition = new Vector2(-6f, 0f);

			GameObject templateObject = new("Template", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
			templateObject.transform.SetParent(dropdownObject.transform, false);
			templateObject.SetActive(false);

			RectTransform templateRect = templateObject.GetComponent<RectTransform>();
			templateRect.anchorMin = new Vector2(0f, 0f);
			templateRect.anchorMax = new Vector2(1f, 0f);
			templateRect.pivot = new Vector2(0.5f, 1f);
			templateRect.anchoredPosition = new Vector2(0f, 2f);
			templateRect.sizeDelta = new Vector2(0f, 150f);

			Image templateImage = templateObject.GetComponent<Image>();
			templateImage.color = new Color(0.18f, 0.18f, 0.18f, 1f);

			ScrollRect scrollRect = templateObject.GetComponent<ScrollRect>();
			scrollRect.horizontal = false;
			scrollRect.movementType = ScrollRect.MovementType.Clamped;

			GameObject viewportObject = new("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
			viewportObject.transform.SetParent(templateObject.transform, false);

			RectTransform viewportRect = viewportObject.GetComponent<RectTransform>();
			viewportRect.anchorMin = Vector2.zero;
			viewportRect.anchorMax = Vector2.one;
			viewportRect.offsetMin = Vector2.zero;
			viewportRect.offsetMax = Vector2.zero;

			Image viewportImage = viewportObject.GetComponent<Image>();
			viewportImage.color = new Color(1f, 1f, 1f, 0.02f);
			Mask viewportMask = viewportObject.GetComponent<Mask>();
			viewportMask.showMaskGraphic = false;

			GameObject contentObject = new("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
			contentObject.transform.SetParent(viewportObject.transform, false);

			RectTransform contentRect = contentObject.GetComponent<RectTransform>();
			contentRect.anchorMin = new Vector2(0f, 1f);
			contentRect.anchorMax = new Vector2(1f, 1f);
			contentRect.pivot = new Vector2(0.5f, 1f);
			contentRect.offsetMin = Vector2.zero;
			contentRect.offsetMax = Vector2.zero;

			VerticalLayoutGroup contentLayout = contentObject.GetComponent<VerticalLayoutGroup>();
			contentLayout.childForceExpandHeight = false;
			contentLayout.childForceExpandWidth = true;
			contentLayout.childControlHeight = true;
			contentLayout.childControlWidth = true;
			contentLayout.spacing = 2f;

			ContentSizeFitter contentFitter = contentObject.GetComponent<ContentSizeFitter>();
			contentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
			contentFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

			GameObject itemObject = new("Item", typeof(RectTransform), typeof(Toggle), typeof(Image), typeof(LayoutElement));
			itemObject.transform.SetParent(contentObject.transform, false);

			LayoutElement itemLayout = itemObject.GetComponent<LayoutElement>();
			itemLayout.preferredHeight = 28f;

			Image itemImage = itemObject.GetComponent<Image>();
			itemImage.color = new Color(0.25f, 0.25f, 0.25f, 1f);

			Toggle itemToggle = itemObject.GetComponent<Toggle>();
			itemToggle.targetGraphic = itemImage;
			itemToggle.isOn = true;

			Text itemLabel = CreateLegacyText("Item Label", itemObject.transform, "Option");
			itemLabel.alignment = TextAnchor.MiddleLeft;
			itemLabel.rectTransform.offsetMin = new Vector2(10f, 0f);
			itemLabel.rectTransform.offsetMax = new Vector2(-10f, 0f);

			scrollRect.viewport = viewportRect;
			scrollRect.content = contentRect;

			dropdown.targetGraphic = dropdownImage;
			dropdown.captionText = captionText;
			dropdown.template = templateRect;
			dropdown.itemText = itemLabel;

			dropdown.onValueChanged.RemoveAllListeners();
			if (onChanged != null)
				dropdown.onValueChanged.AddListener(onChanged);

			return dropdown;
		}

		private static ActionButtonControls CreateButtonRow(Transform parent, string label, string buttonLabel, UnityEngine.Events.UnityAction onClick)
		{
			GameObject row = new($"{label}Row", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
			row.transform.SetParent(parent, false);

			HorizontalLayoutGroup rowLayout = row.GetComponent<HorizontalLayoutGroup>();
			rowLayout.spacing = 12f;
			rowLayout.childAlignment = TextAnchor.MiddleLeft;
			rowLayout.childControlHeight = true;
			rowLayout.childControlWidth = true;
			rowLayout.childForceExpandWidth = false;
			rowLayout.childForceExpandHeight = false;

			row.GetComponent<LayoutElement>().preferredHeight = 42f;

			TMP_Text labelText = CreateText("Label", row.transform, label);
			labelText.fontSize = 19f;
			labelText.GetComponent<LayoutElement>().preferredWidth = 180f;

			GameObject buttonObject = new("Button", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
			buttonObject.transform.SetParent(row.transform, false);

			Image buttonImage = buttonObject.GetComponent<Image>();
			buttonImage.color = new Color(0.22f, 0.44f, 0.72f, 1f);

			LayoutElement buttonLayout = buttonObject.GetComponent<LayoutElement>();
			buttonLayout.preferredHeight = 34f;
			buttonLayout.preferredWidth = 220f;

			Button button = buttonObject.GetComponent<Button>();
			button.onClick.RemoveAllListeners();
			if (onClick != null)
				button.onClick.AddListener(onClick);

			TMP_Text buttonText = CreateText("ButtonLabel", buttonObject.transform, buttonLabel);
			buttonText.fontSize = 18f;
			buttonText.alignment = TextAlignmentOptions.Center;

			return new ActionButtonControls
			{
				Button = button,
				LabelText = buttonText,
			};
		}

		private static Text CreateLegacyText(string objectName, Transform parent, string value)
		{
			GameObject textObject = new(objectName, typeof(RectTransform), typeof(Text));
			textObject.transform.SetParent(parent, false);

			Text text = textObject.GetComponent<Text>();
			text.font = GetDefaultFont();
			text.text = value;
			text.color = Color.white;
			text.alignment = TextAnchor.MiddleLeft;

			RectTransform rect = text.rectTransform;
			rect.anchorMin = Vector2.zero;
			rect.anchorMax = Vector2.one;
			rect.offsetMin = Vector2.zero;
			rect.offsetMax = Vector2.zero;

			return text;
		}

		private static Font GetDefaultFont()
		{
			if (defaultFont == null)
			{
				defaultFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
				if (defaultFont == null)
					defaultFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
			}

			return defaultFont;
		}

		private static string GetPlacingPolicyLabel(PlacingPolicyType type)
		{
			return type switch
			{
				PlacingPolicyType.Nearest => "Nearest",
				_ => "Below Avg Filled + Nearest",
			};
		}

		private static string GetCollectingPolicyLabel(CollectingPolicyType type)
		{
			return type switch
			{
				CollectingPolicyType.LargestQuantityNearest => "Largest Qty + Nearest",
				_ => "Nearest",
			};
		}

		private static void ClearChildren(Transform parent)
		{
			for (int i = parent.childCount - 1; i >= 0; i--)
				Destroy(parent.GetChild(i).gameObject);
		}
	}
}
