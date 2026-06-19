using System.Collections.Generic;
using AYellowpaper.SerializedCollections;
using TMPro;
using UnityEngine;

public sealed class CargoPortLinkModeController : MonoBehaviour
{
	private enum LinkPhase
	{
		SelectSourcePort,
		SelectTargetBuilding,
		SelectTargetPort,
	}

	private enum LinkMarkerType
	{
		SourcePort,
		TargetBuilding,
		TargetPort,
	}

	[System.Serializable]
	private struct LinkMarkerVisualConfig
	{
		public float MarkerHeight;
		public float LabelHeight;
		public float LabelScale;
		public Color MarkerColor;
		public Color LabelColor;

		public LinkMarkerVisualConfig(float markerHeight, float labelHeight, float labelScale, Color markerColor, Color labelColor)
		{
			MarkerHeight = markerHeight;
			LabelHeight = labelHeight;
			LabelScale = labelScale;
			MarkerColor = markerColor;
			LabelColor = labelColor;
		}
	}

	[SerializedDictionary("Marker", "Visual")]
	[SerializeField] private SerializedDictionary<LinkMarkerType, LinkMarkerVisualConfig> markerVisuals = new();
	[SerializeField] private GameObject overlayQuadPrefab;
	[SerializeField] private GameObject overlayLabelPrefab;

	private readonly List<GameObject> overlayObjects = new();
	private GameObject overlayRoot;
	private Building sourceBuilding;
	private CargoPort sourcePort;
	private Building targetBuilding;
	private string lastStatusMessage = string.Empty;
	private bool isEditing;

	private InteractionContext Interaction => GameContext.HasInstance ? GameContext.Instance.InteractionCtx : null;
	private GridService GridService => GameContext.HasInstance ? GameContext.Instance.GridService : null;
	private BuildingManager BuildingManager => GameContext.HasInstance ? GameContext.Instance.BuildingMgr : null;
	private BuildingFootprintService BuildingFootprintService => GameContext.HasInstance ? GameContext.Instance.BuildingFootprintService : null;
	private CargoPortService CargoPortService => GameContext.HasInstance ? GameContext.Instance.CargoPortSvc : null;

	public bool IsEditing => isEditing;
	public Building SourceBuilding => sourceBuilding;
	public string StatusText => BuildStatusText();
	public bool HasStatusMessage => string.IsNullOrWhiteSpace(lastStatusMessage) == false;

	private void Awake()
	{
		EnsureMarkerVisuals();
		EnsureOverlayRoot();

		if (Interaction != null)
		{
			Interaction.OnHandleBuildingLinkSelection += HandleLinkSelection;
			Interaction.OnModeChanged += HandleModeChanged;
		}
	}

	private void OnDestroy()
	{
		if (Interaction != null)
		{
			Interaction.OnHandleBuildingLinkSelection -= HandleLinkSelection;
			Interaction.OnModeChanged -= HandleModeChanged;
		}

		ClearOverlay();
		if (overlayRoot != null)
			Destroy(overlayRoot);
	}

	private void OnValidate()
	{
		EnsureMarkerVisuals();
	}

	public bool BeginLinkEdit(Building building)
	{
		if (building == null)
		{
			lastStatusMessage = "Select a building before linking cargo ports.";
			return false;
		}

		if (HasOutboundPorts(building) == false)
		{
			lastStatusMessage = $"{building.DisplayName} has no outbound cargo ports to link.";
			return false;
		}

		sourceBuilding = building;
		sourcePort = null;
		targetBuilding = null;
		isEditing = true;
		lastStatusMessage = string.Empty;
		Interaction?.EnterBuildingLinkMode();
		RefreshOverlay();
		return true;
	}

	public void EndLinkEdit()
	{
		if (isEditing == false && (Interaction == null || Interaction.Mode != InteractionContext.InteractionMode.BuildingLinkEdit))
			return;

		ResetLinkMode(true);
	}

	private void HandleModeChanged(InteractionContext.InteractionDomain domain, InteractionContext.InteractionAction action)
	{
		if (Interaction == null)
			return;

		if (Interaction.Mode == InteractionContext.InteractionMode.BuildingLinkEdit)
		{
			RefreshOverlay();
			return;
		}

		if (isEditing)
			ResetLinkMode(false);
	}

	private bool HandleLinkSelection(Unity.Mathematics.int3 pos)
	{
		if (isEditing == false || Interaction == null || Interaction.Mode != InteractionContext.InteractionMode.BuildingLinkEdit)
			return false;

		switch (GetPhase())
		{
			case LinkPhase.SelectSourcePort:
				return TrySelectSourcePort(pos);
			case LinkPhase.SelectTargetBuilding:
				return TrySelectTargetBuilding(pos);
			case LinkPhase.SelectTargetPort:
				return TrySelectTargetPort(pos);
			default:
				return false;
		}
	}

	private LinkPhase GetPhase()
	{
		if (sourcePort == null)
			return LinkPhase.SelectSourcePort;

		if (targetBuilding == null)
			return LinkPhase.SelectTargetBuilding;

		return LinkPhase.SelectTargetPort;
	}

	private bool TrySelectSourcePort(Unity.Mathematics.int3 pos)
	{
		if (TryGetCargoPortAt(pos, out CargoPort cargoPort) == false || cargoPort == null)
			return false;

		if (BuildingHasPort(sourceBuilding, cargoPort) == false)
			return false;

		if (cargoPort is InboundCargoPort)
		{
			lastStatusMessage = "Select an outbound cargo port as the source.";
			return true;
		}

		sourcePort = cargoPort;
		targetBuilding = null;
		Interaction?.SelectObject(sourcePort.gameObject);
		lastStatusMessage = $"Source selected: {GetPortDisplayName(sourcePort)}. Select a target building.";
		RefreshOverlay();
		return true;
	}

	private bool TrySelectTargetBuilding(Unity.Mathematics.int3 pos)
	{
		if (TryGetBuildingAt(pos, out Building building) == false || building == null)
			return false;

		if (building == sourceBuilding)
		{
			lastStatusMessage = "Select a different building as the target.";
			return true;
		}

		if (HasInboundPorts(building) == false)
		{
			lastStatusMessage = $"{building.DisplayName} has no inbound cargo ports.";
			return true;
		}

		targetBuilding = building;
		lastStatusMessage = $"Target building selected: {targetBuilding.DisplayName}. Select an inbound cargo port.";
		RefreshOverlay();
		return true;
	}

	private bool TrySelectTargetPort(Unity.Mathematics.int3 pos)
	{
		if (TryGetCargoPortAt(pos, out CargoPort cargoPort) == false || cargoPort == null)
			return false;

		if (BuildingHasPort(targetBuilding, cargoPort) == false)
		{
			lastStatusMessage = $"Select an inbound cargo port in {targetBuilding?.DisplayName ?? "the target building"}.";
			return true;
		}

		if (cargoPort is not InboundCargoPort)
		{
			lastStatusMessage = "Select an inbound cargo port as the destination.";
			return true;
		}

		if (sourcePort == null)
			return false;

		if (sourcePort.TryAddLinkedPort(cargoPort))
			lastStatusMessage = $"Linked {GetPortDisplayName(sourcePort)} -> {GetPortDisplayName(cargoPort)}. Select another target building or right click to finish.";
		else
			lastStatusMessage = "Unable to create the cargo port link.";

		targetBuilding = null;
		Interaction?.ClearSelection();
		RefreshOverlay();
		return true;
	}

	private void RefreshOverlay()
	{
		ClearOverlay();
		if (isEditing == false || sourceBuilding == null || Interaction == null || Interaction.Mode != InteractionContext.InteractionMode.BuildingLinkEdit)
			return;

		switch (GetPhase())
		{
			case LinkPhase.SelectSourcePort:
				CreateSourcePortMarkers();
				break;

			case LinkPhase.SelectTargetBuilding:
				CreateSourcePortMarkers();
				CreateTargetBuildingMarkers();
				break;

			case LinkPhase.SelectTargetPort:
				CreateSourcePortMarkers();
				CreateSelectedTargetBuildingMarker();
				CreateTargetPortMarkers();
				break;
		}
	}

	private void CreateSourcePortMarkers()
	{
		if (sourceBuilding == null || CargoPortService == null)
			return;

		LinkMarkerVisualConfig visual = GetMarkerVisual(LinkMarkerType.SourcePort);
		IReadOnlyList<CargoPort> ports = CargoPortService.GetCargoPorts(sourceBuilding.RuntimeBuildingId);
		for (int i = 0; i < ports.Count; ++i)
		{
			CargoPort port = ports[i];
			if (port is not OutboundCargoPort || port == sourcePort)
				continue;

			CreatePortMarker(port, visual, "OUT");
		}
	}

	private void CreateTargetBuildingMarkers()
	{
		if (BuildingManager == null)
			return;

		LinkMarkerVisualConfig visual = GetMarkerVisual(LinkMarkerType.TargetBuilding);
		IReadOnlyList<Building> buildings = BuildingManager.RegisteredBuildings;
		for (int i = 0; i < buildings.Count; ++i)
		{
			Building building = buildings[i];
			if (building == null || building == sourceBuilding || HasInboundPorts(building) == false)
				continue;

			CreateBuildingMarker(building, visual, building.DisplayName);
		}
	}

	private void CreateTargetPortMarkers()
	{
		if (targetBuilding == null || CargoPortService == null)
			return;

		LinkMarkerVisualConfig visual = GetMarkerVisual(LinkMarkerType.TargetPort);
		IReadOnlyList<CargoPort> ports = CargoPortService.GetCargoPorts(targetBuilding.RuntimeBuildingId);
		for (int i = 0; i < ports.Count; ++i)
		{
			CargoPort port = ports[i];
			if (port is not InboundCargoPort)
				continue;

			CreatePortMarker(port, visual, "IN");
		}
	}

	private void CreateSelectedTargetBuildingMarker()
	{
		if (targetBuilding != null)
			CreateBuildingMarker(targetBuilding, GetMarkerVisual(LinkMarkerType.TargetBuilding), targetBuilding.DisplayName);
	}

	private void CreatePortMarker(CargoPort port, LinkMarkerVisualConfig visual, string labelText)
	{
		if (port == null)
			return;

		GameObject marker = CreateQuadObject("CargoPortLinkMarker");
		if (marker == null)
			return;

		marker.transform.position = BuildWorldPosition(port.GridPosition, visual.MarkerHeight);
		marker.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
		marker.transform.localScale = new Vector3(0.9f, 0.9f, 1f);
		marker.GetComponent<MeshRenderer>().material.color = visual.MarkerColor;
		overlayObjects.Add(marker);

		GameObject label = CreateLabelObject("CargoPortLinkLabel", labelText, visual.LabelColor);
		if (label == null)
			return;

		label.transform.position = BuildWorldPosition(port.GridPosition, visual.LabelHeight);
		label.transform.rotation = Quaternion.Euler(90f, 180f, 0f);
		label.transform.localScale = Vector3.one * visual.LabelScale;
		overlayObjects.Add(label);
	}

	private void CreateBuildingMarker(Building building, LinkMarkerVisualConfig visual, string labelText)
	{
		if (building == null || BuildingFootprintService == null)
			return;

		if (BuildingFootprintService.TryGetInteriorBounds(building.RuntimeBuildingId, out RectInt bounds, out _) == false)
			return;

		GameObject marker = CreateQuadObject("CargoPortTargetBuildingMarker");
		if (marker == null)
			return;

		marker.transform.position = new Vector3(
			bounds.xMin + (bounds.width * 0.5f) - 0.5f,
			visual.MarkerHeight,
			bounds.yMin + (bounds.height * 0.5f) - 0.5f);
		marker.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
		marker.transform.localScale = new Vector3(bounds.width, bounds.height, 1f);
		marker.GetComponent<MeshRenderer>().material.color = visual.MarkerColor;
		overlayObjects.Add(marker);

		GameObject label = CreateLabelObject("CargoPortTargetBuildingLabel", labelText, visual.LabelColor);
		if (label == null)
			return;

		label.transform.position = new Vector3(
			bounds.xMin + (bounds.width * 0.5f) - 0.5f,
			visual.LabelHeight,
			bounds.yMin + (bounds.height * 0.5f) - 0.5f);
		label.transform.rotation = Quaternion.Euler(90f, 180f, 0f);
		label.transform.localScale = Vector3.one * visual.LabelScale;
		overlayObjects.Add(label);
	}

	private void EnsureMarkerVisuals()
	{
		markerVisuals ??= new SerializedDictionary<LinkMarkerType, LinkMarkerVisualConfig>();

		SetMissingMarkerVisual(
			LinkMarkerType.SourcePort,
			new LinkMarkerVisualConfig(
				0.04f,
				0.045f,
				0.24f,
				new Color(0.9f, 0.42f, 0.2f, 0.8f),
				Color.white));

		SetMissingMarkerVisual(
			LinkMarkerType.TargetBuilding,
			new LinkMarkerVisualConfig(
				0.03f,
				0.045f,
				0.28f,
				new Color(0.2f, 0.62f, 0.95f, 0.28f),
				Color.white));

		SetMissingMarkerVisual(
			LinkMarkerType.TargetPort,
			new LinkMarkerVisualConfig(
				0.04f,
				0.045f,
				0.24f,
				new Color(0.2f, 0.82f, 0.5f, 0.85f),
				Color.white));
	}

	private LinkMarkerVisualConfig GetMarkerVisual(LinkMarkerType markerType)
	{
		EnsureMarkerVisuals();
		return markerVisuals.TryGetValue(markerType, out LinkMarkerVisualConfig visual)
			? visual
			: default;
	}

	private void SetMissingMarkerVisual(LinkMarkerType markerType, LinkMarkerVisualConfig visual)
	{
		if (markerVisuals.ContainsKey(markerType))
			return;

		markerVisuals[markerType] = visual;
	}

	private bool TryGetCargoPortAt(Unity.Mathematics.int3 pos, out CargoPort cargoPort)
	{
		cargoPort = null;
		GameObject targetObject = GridService?.GetObjectOnGrid(pos);
		if (targetObject == null)
			return false;

		return targetObject.TryGetComponent(out cargoPort) && cargoPort != null;
	}

	private bool TryGetBuildingAt(Unity.Mathematics.int3 pos, out Building building)
	{
		building = null;
		GridCell cell = GridService?.GetCell(pos);
		if (cell == null || cell.BuildingId == 0 || BuildingManager == null)
			return false;

		return BuildingManager.TryGetBuilding(cell.BuildingId, out building) && building != null;
	}

	private bool HasOutboundPorts(Building building)
	{
		if (building == null || CargoPortService == null)
			return false;

		List<CargoPort> ports = new();
		return CargoPortService.TryQueryPorts(building.RuntimeBuildingId, ports, port => port is OutboundCargoPort);
	}

	private bool HasInboundPorts(Building building)
	{
		if (building == null || CargoPortService == null)
			return false;

		List<CargoPort> ports = new();
		return CargoPortService.TryQueryPorts(building.RuntimeBuildingId, ports, port => port is InboundCargoPort);
	}

	private string BuildStatusText()
	{
		if (string.IsNullOrWhiteSpace(lastStatusMessage) == false)
			return lastStatusMessage;

		if (isEditing == false)
			return "Select a building, then start cargo port linking.";

		if (sourceBuilding == null)
			return "Select a source building to begin linking cargo ports.";

		return GetPhase() switch
		{
			LinkPhase.SelectSourcePort => $"Select an outbound cargo port in {sourceBuilding.DisplayName}.",
			LinkPhase.SelectTargetBuilding => $"Select a target building for {GetPortDisplayName(sourcePort)}.",
			LinkPhase.SelectTargetPort => $"Select an inbound cargo port in {targetBuilding?.DisplayName ?? "the target building"}.",
			_ => "Select cargo ports to create a link.",
		};
	}

	private void ResetLinkMode(bool exitInteractionMode)
	{
		isEditing = false;
		sourceBuilding = null;
		sourcePort = null;
		targetBuilding = null;
		lastStatusMessage = string.Empty;
		ClearOverlay();

		if (exitInteractionMode && Interaction != null && Interaction.Mode == InteractionContext.InteractionMode.BuildingLinkEdit)
			Interaction.ExitBuildingLinkMode();
	}

	private void EnsureOverlayRoot()
	{
		if (overlayRoot != null)
			return;

		overlayRoot = new GameObject("CargoPortLinkOverlayRoot");
		Transform parent = GameContext.HasInstance ? GameContext.Instance.transform : transform;
		overlayRoot.transform.SetParent(parent, false);
		overlayRoot.hideFlags = HideFlags.HideInHierarchy;
	}

	private void ClearOverlay()
	{
		for (int i = 0; i < overlayObjects.Count; ++i)
		{
			GameObject overlayObject = overlayObjects[i];
			if (overlayObject != null)
				Destroy(overlayObject);
		}

		overlayObjects.Clear();
	}

	private GameObject CreateQuadObject(string objectName)
	{
		EnsureOverlayRoot();
		if (overlayQuadPrefab == null)
		{
			Debug.LogError("[CargoPortLinkModeController] Overlay quad prefab is missing.", this);
			return null;
		}

		GameObject quad = Instantiate(overlayQuadPrefab, overlayRoot.transform);
		quad.name = objectName;
		return quad;
	}

	private GameObject CreateLabelObject(string objectName, string labelText, Color color)
	{
		EnsureOverlayRoot();
		if (overlayLabelPrefab == null)
		{
			Debug.LogError("[CargoPortLinkModeController] Overlay label prefab is missing.", this);
			return null;
		}

		GameObject label = Instantiate(overlayLabelPrefab, overlayRoot.transform);
		label.name = objectName;
		if (label == null)
			return null;

		TextMeshPro text = label.GetComponent<TextMeshPro>();
		text.text = labelText;
		text.fontSize = 4.2f;
		text.color = color;
		return label;
	}

	private static Vector3 BuildWorldPosition(Unity.Mathematics.int3 gridPos, float y)
	{
		return new Vector3(gridPos.x, y, gridPos.z);
	}

	private static string GetPortDisplayName(CargoPort port)
	{
		if (port == null)
			return "CargoPort";

		return string.IsNullOrWhiteSpace(port.name) ? "CargoPort" : port.name;
	}

	private static bool BuildingHasPort(Building building, CargoPort cargoPort)
	{
		if (building == null || cargoPort == null)
			return false;

		IReadOnlyList<CargoPort> ports = building.OccupiedCargoPorts;
		for (int i = 0; i < ports.Count; ++i)
		{
			if (ports[i] == cargoPort)
				return true;
		}

		return false;
	}
}
