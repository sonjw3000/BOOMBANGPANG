using System.Collections.Generic;
using UnityEngine;

public sealed class WorkflowDestinationLinkModeController : MonoBehaviour
{
	public enum DestinationSelectionType
	{
		None,
		InboundUnloading,
		OutboundLoading,
	}

	[SerializeField] private GameObject overlayQuadPrefab;
	[SerializeField] private GameObject overlayLabelPrefab;
	[SerializeField] private float markerHeight = 0.034f;
	[SerializeField] private float labelHeight = 0.049f;
	[SerializeField] private float labelScale = 0.28f;
	[SerializeField] private Color markerColor = new(1f, 0.53f, 0.14f, 0.34f);
	[SerializeField] private Color selectedMarkerColor = new(0.2f, 0.82f, 0.43f, 0.5f);

	private readonly List<GameObject> overlayObjects = new();
	private GameObject overlayRoot;
	private DestinationSelectionType selectionType;
	private Area selectedLandingArea;
	private AreaOverlayController areaOverlay;
	private bool routingVisible;
	private string lastStatusMessage = string.Empty;
	[System.NonSerialized] private InteractionContext boundInteraction;

	private InteractionContext Interaction => boundInteraction ??
		(GameContext.HasInstance ? GameContext.Instance.InteractionCtx : null);
	private GridService GridService => GameContext.HasInstance ? GameContext.Instance.GridService : null;
	private BuildingManager BuildingManager => GameContext.HasInstance ? GameContext.Instance.BuildingMgr : null;
	private BuildingFootprintService FootprintService => GameContext.HasInstance ? GameContext.Instance.BuildingFootprintService : null;
	private AreaManager AreaManager => GameContext.HasInstance ? GameContext.Instance.AreaMgr : null;
	private OutboundWorkflowService OutboundWorkflow => GameContext.HasInstance ? GameContext.Instance.OBWorkflowSvc : null;

	public bool IsEditing => selectionType != DestinationSelectionType.None;
	public DestinationSelectionType SelectionType => selectionType;
	public bool HasStatusMessage => string.IsNullOrWhiteSpace(lastStatusMessage) == false;
	public string StatusText => HasStatusMessage
		? lastStatusMessage
		: selectionType switch
		{
			DestinationSelectionType.InboundUnloading => selectedLandingArea == null
				? "Select a Landing Area."
				: $"Select a building with inbound cargo ports for {selectedLandingArea.DisplayName}.",
			DestinationSelectionType.OutboundLoading => "Select a building with outbound cargo ports.",
			_ => string.Empty,
		};

	public event System.Action StateChanged;
	public event System.Action DestinationChanged;

	public void Configure(GameObject targetOverlayQuadPrefab, GameObject targetOverlayLabelPrefab)
	{
		overlayQuadPrefab = targetOverlayQuadPrefab;
		overlayLabelPrefab = targetOverlayLabelPrefab;
	}

	private void Awake()
	{
		EnsureOverlayRoot();
	}

	private void OnEnable()
	{
		EnsureOverlayRoot();
		overlayRoot.SetActive(true);
		BindInteraction();
		if (IsEditing)
			RefreshOverlay();
	}

	private void Start()
	{
		BindInteraction();
	}

	private void OnDisable()
	{
		UnbindInteraction();
		ClearOverlay();
		overlayRoot?.SetActive(false);
	}

	private void BindInteraction()
	{
		InteractionContext interaction = GameContext.HasInstance ? GameContext.Instance.InteractionCtx : null;
		if (interaction == null)
			return;

		if (boundInteraction != null && boundInteraction != interaction)
			UnbindInteraction();

		boundInteraction = interaction;
		boundInteraction.OnHandleBuildingLinkSelection -= HandleBuildingSelection;
		boundInteraction.OnModeChanged -= HandleModeChanged;
		boundInteraction.OnHandleBuildingLinkSelection += HandleBuildingSelection;
		boundInteraction.OnModeChanged += HandleModeChanged;
	}

	private void UnbindInteraction()
	{
		if (boundInteraction == null)
			return;

		boundInteraction.OnHandleBuildingLinkSelection -= HandleBuildingSelection;
		boundInteraction.OnModeChanged -= HandleModeChanged;
		boundInteraction = null;
	}

	private void OnDestroy()
	{

		ClearOverlay();
		if (overlayRoot != null) Destroy(overlayRoot);
	}

	public bool BeginInboundSelection()
	{
		return BeginSelection(DestinationSelectionType.InboundUnloading);
	}

	public bool BeginOutboundSelection()
	{
		return BeginSelection(DestinationSelectionType.OutboundLoading);
	}

	public void SetRoutingVisible(bool visible)
	{
		routingVisible = visible;
		RefreshAreaOverlayVisibility();
	}

	public void EndSelection()
	{
		bool hasActiveInteraction = Interaction != null && Interaction.Mode == InteractionContext.InteractionMode.BuildingLinkEdit;
		if (IsEditing == false && hasActiveInteraction == false && HasStatusMessage == false)
			return;

		selectionType = DestinationSelectionType.None;
		selectedLandingArea = null;
		lastStatusMessage = string.Empty;
		ClearOverlay();
		RefreshAreaOverlayVisibility();
		if (hasActiveInteraction)
			Interaction.ExitBuildingLinkMode();
		StateChanged?.Invoke();
	}

	private bool BeginSelection(DestinationSelectionType type)
	{
		if (type == DestinationSelectionType.None || Interaction == null)
			return false;

		selectionType = type;
		selectedLandingArea = null;
		lastStatusMessage = string.Empty;
		Interaction.EnterBuildingLinkMode();
		RefreshAreaOverlayVisibility();
		RefreshOverlay();
		StateChanged?.Invoke();
		return true;
	}

	private bool HandleBuildingSelection(Unity.Mathematics.int3 position)
	{
		if (IsEditing == false || Interaction == null || Interaction.Mode != InteractionContext.InteractionMode.BuildingLinkEdit)
			return false;

		if (selectionType == DestinationSelectionType.InboundUnloading && selectedLandingArea == null)
		{
			if (AreaManager == null || AreaManager.TryGetAreaAt(position, out Area area) == false ||
				area == null || area.Type != AreaType.RocketLanding)
				return false;

			selectedLandingArea = area;
			lastStatusMessage = string.Empty;
			RefreshOverlay();
			StateChanged?.Invoke();
			return true;
		}

		if (TryGetBuildingAt(position, out Building building) == false || building == null)
			return false;

		if (IsValidTarget(building) == false)
		{
			lastStatusMessage = selectionType == DestinationSelectionType.OutboundLoading
				? $"{building.DisplayName} has no outbound cargo ports."
				: $"{building.DisplayName} has no inbound cargo ports.";
			StateChanged?.Invoke();
			return true;
		}

		if (selectionType == DestinationSelectionType.OutboundLoading)
		{
			OutboundWorkflow?.SetLoadingDestinationBuilding(building);
			lastStatusMessage = $"Loading destination set to {building.DisplayName}.";
		}
		else
		{
			if (selectedLandingArea == null || AreaManager == null ||
				AreaManager.TrySetDestinationBuilding(selectedLandingArea, building.RuntimeBuildingId) == false)
			{
				lastStatusMessage = "Unable to link the Landing Area to that building.";
				StateChanged?.Invoke();
				return true;
			}
			lastStatusMessage = $"Linked {selectedLandingArea.DisplayName} → {building.DisplayName}.";
		}

		selectionType = DestinationSelectionType.None;
		selectedLandingArea = null;
		ClearOverlay();
		RefreshAreaOverlayVisibility();
		Interaction.ClearSelection();
		if (Interaction.Mode == InteractionContext.InteractionMode.BuildingLinkEdit)
			Interaction.ExitBuildingLinkMode();
		DestinationChanged?.Invoke();
		StateChanged?.Invoke();
		return true;
	}

	private void HandleModeChanged(InteractionContext.InteractionDomain domain, InteractionContext.InteractionAction action)
	{
		if (IsEditing == false || Interaction == null || Interaction.Mode == InteractionContext.InteractionMode.BuildingLinkEdit)
			return;

		selectionType = DestinationSelectionType.None;
		selectedLandingArea = null;
		lastStatusMessage = string.Empty;
		ClearOverlay();
		RefreshAreaOverlayVisibility();
		StateChanged?.Invoke();
	}

	private void RefreshOverlay()
	{
		ClearOverlay();
		if (IsEditing == false || BuildingManager == null) return;
		if (selectionType == DestinationSelectionType.InboundUnloading)
		{
			if (selectedLandingArea == null) return;
			CreateAreaMarker(selectedLandingArea);
		}

		IReadOnlyList<Building> buildings = BuildingManager.RegisteredBuildings;
		for (int i = 0; i < buildings.Count; ++i)
		{
			Building building = buildings[i];
			if (building != null && IsValidTarget(building))
				CreateBuildingMarker(building, IsSelectedDestination(building));
		}
	}

	private void CreateAreaMarker(Area area)
	{
		RectInt bounds = area.Bounds;
		GameObject marker = CreateQuadObject();
		if (marker == null) return;
		marker.transform.position = new Vector3(
			bounds.xMin + (bounds.width - 1) * 0.5f,
			markerHeight + 0.004f,
			bounds.yMin + (bounds.height - 1) * 0.5f);
		marker.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
		marker.transform.localScale = new Vector3(bounds.width, bounds.height, 1f);
		MeshRenderer renderer = marker.GetComponent<MeshRenderer>();
		if (renderer != null) renderer.material.color = selectedMarkerColor;
		overlayObjects.Add(marker);
	}

	private void CreateBuildingMarker(Building building, bool selected)
	{
		if (FootprintService == null ||
			FootprintService.TryGetFootprint(building.RuntimeBuildingId, out BuildingFootprintRecord footprint) == false || footprint == null)
			return;

		RectInt bounds = footprint.Bounds;
		Color color = selected ? selectedMarkerColor : markerColor;
		for (int z = bounds.yMin; z < bounds.yMax; ++z)
		{
			for (int x = bounds.xMin; x < bounds.xMax; ++x)
			{
				GridCell cell = GridService?.GetCell(x, footprint.Floor, z);
				if (cell == null || cell.BuildingId != building.RuntimeBuildingId) continue;
				GameObject marker = CreateQuadObject();
				if (marker == null) return;
				marker.transform.position = new Vector3(x, markerHeight, z);
				marker.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
				marker.transform.localScale = Vector3.one;
				MeshRenderer renderer = marker.GetComponent<MeshRenderer>();
				if (renderer != null) renderer.material.color = color;
				overlayObjects.Add(marker);
			}
		}

		GameObject label = CreateLabelObject(building.DisplayName, color);
		if (label == null) return;
		label.transform.position = new Vector3(footprint.Center.x, labelHeight, footprint.Center.y);
		label.transform.rotation = Quaternion.Euler(90f, 180f, 0f);
		label.transform.localScale = Vector3.one * labelScale;
		overlayObjects.Add(label);
	}

	private bool IsValidTarget(Building building)
	{
		return selectionType == DestinationSelectionType.OutboundLoading
			? HasPortType<OutboundCargoPort>(building)
			: HasPortType<InboundCargoPort>(building);
	}

	private bool IsSelectedDestination(Building building)
	{
		return selectionType == DestinationSelectionType.OutboundLoading
			? OutboundWorkflow != null && OutboundWorkflow.LoadingDestinationBuildingId == building.RuntimeBuildingId
			: selectedLandingArea != null && selectedLandingArea.DestinationBuildingId == building.RuntimeBuildingId;
	}

	private void RefreshAreaOverlayVisibility()
	{
		if (areaOverlay == null && AreaManager != null)
			AreaManager.TryGetComponent(out areaOverlay);
		areaOverlay ??= FindAnyObjectByType<AreaOverlayController>(FindObjectsInactive.Include);
		areaOverlay?.SetAreaModeActive(
			routingVisible || selectionType == DestinationSelectionType.InboundUnloading,
			AreaType.RocketLanding,
			0);
	}

	private static bool HasPortType<TPort>(Building building) where TPort : CargoPort
	{
		if (building == null) return false;
		IReadOnlyList<CargoPort> ports = building.OccupiedCargoPorts;
		for (int i = 0; i < ports.Count; ++i)
			if (ports[i] is TPort) return true;
		return false;
	}

	private bool TryGetBuildingAt(Unity.Mathematics.int3 position, out Building building)
	{
		building = null;
		GridCell cell = GridService?.GetCell(position);
		return cell != null && cell.BuildingId != 0 && BuildingManager != null &&
			BuildingManager.TryGetBuilding(cell.BuildingId, out building) && building != null;
	}

	private void EnsureOverlayRoot()
	{
		if (overlayRoot != null) return;
		overlayRoot = new GameObject("WorkflowDestinationLinkOverlayRoot");
		Transform parent = GameContext.HasInstance ? GameContext.Instance.transform : transform;
		overlayRoot.transform.SetParent(parent, false);
		overlayRoot.hideFlags = HideFlags.HideInHierarchy;
	}

	private GameObject CreateQuadObject()
	{
		EnsureOverlayRoot();
		if (overlayQuadPrefab == null) return null;
		GameObject marker = Instantiate(overlayQuadPrefab, overlayRoot.transform);
		marker.name = "WorkflowDestinationMarker";
		return marker;
	}

	private GameObject CreateLabelObject(string text, Color color)
	{
		EnsureOverlayRoot();
		if (overlayLabelPrefab == null) return null;
		GameObject label = Instantiate(overlayLabelPrefab, overlayRoot.transform);
		label.name = "WorkflowDestinationLabel";
		TMPro.TextMeshPro textMesh = label.GetComponent<TMPro.TextMeshPro>();
		if (textMesh != null)
		{
			textMesh.text = text;
			textMesh.color = color;
		}
		return label;
	}

	private void ClearOverlay()
	{
		for (int i = 0; i < overlayObjects.Count; ++i)
			if (overlayObjects[i] != null) Destroy(overlayObjects[i]);
		overlayObjects.Clear();
	}
}
