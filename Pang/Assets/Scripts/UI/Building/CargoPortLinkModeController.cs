using System.Collections.Generic;
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

	[SerializeField] private float portMarkerHeight = 0.04f;
	[SerializeField] private float buildingMarkerHeight = 0.03f;
	[SerializeField] private float labelHeight = 0.045f;
	[SerializeField] private Color sourcePortColor = new(0.9f, 0.42f, 0.2f, 0.8f);
	[SerializeField] private Color targetBuildingColor = new(0.2f, 0.62f, 0.95f, 0.28f);
	[SerializeField] private Color targetPortColor = new(0.2f, 0.82f, 0.5f, 0.85f);
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
	private CargoPortService CargoPortService => GameContext.HasInstance
		? (GameContext.Instance.OBWorkflowSvc != null
			? GameContext.Instance.OBWorkflowSvc.CargoPortService
			: GameContext.Instance.IBWorkflowSvc != null
				? GameContext.Instance.IBWorkflowSvc.CargoPortService
				: null)
		: null;

	public bool IsEditing => isEditing;
	public Building SourceBuilding => sourceBuilding;
	public string StatusText => BuildStatusText();
	public bool HasStatusMessage => string.IsNullOrWhiteSpace(lastStatusMessage) == false;

	private void Awake()
	{
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

		if (cargoPort.IsInbound)
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

		if (cargoPort.IsInbound == false)
		{
			lastStatusMessage = "Select an inbound cargo port as the destination.";
			return true;
		}

		if (sourcePort == null)
			return false;

		if (sourcePort.TryAddLinkedPort(cargoPort))
			lastStatusMessage = $"Linked {GetPortDisplayName(sourcePort)} -> {GetPortDisplayName(cargoPort)}.";
		else
			lastStatusMessage = "Unable to create the cargo port link.";

		sourcePort = null;
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

		IReadOnlyList<CargoPort> ports = CargoPortService.GetCargoPorts(sourceBuilding.RuntimeBuildingId);
		for (int i = 0; i < ports.Count; ++i)
		{
			CargoPort port = ports[i];
			if (port == null || port.IsInbound || port == sourcePort)
				continue;

			CreatePortMarker(port, sourcePortColor, "OUT");
		}
	}

	private void CreateTargetBuildingMarkers()
	{
		if (BuildingManager == null)
			return;

		IReadOnlyList<Building> buildings = BuildingManager.RegisteredBuildings;
		for (int i = 0; i < buildings.Count; ++i)
		{
			Building building = buildings[i];
			if (building == null || building == sourceBuilding || HasInboundPorts(building) == false)
				continue;

			CreateBuildingMarker(building, targetBuildingColor, building.DisplayName);
		}
	}

	private void CreateTargetPortMarkers()
	{
		if (targetBuilding == null || CargoPortService == null)
			return;

		IReadOnlyList<CargoPort> ports = CargoPortService.GetCargoPorts(targetBuilding.RuntimeBuildingId);
		for (int i = 0; i < ports.Count; ++i)
		{
			CargoPort port = ports[i];
			if (port == null || port.IsInbound == false)
				continue;

			CreatePortMarker(port, targetPortColor, "IN");
		}
	}

	private void CreateSelectedTargetBuildingMarker()
	{
		if (targetBuilding != null)
			CreateBuildingMarker(targetBuilding, targetBuildingColor, targetBuilding.DisplayName);
	}

	private void CreatePortMarker(CargoPort port, Color color, string labelText)
	{
		if (port == null)
			return;

		GameObject marker = CreateQuadObject("CargoPortLinkMarker");
		marker.transform.position = BuildWorldPosition(port.GridPosition, portMarkerHeight);
		marker.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
		marker.transform.localScale = new Vector3(0.9f, 0.9f, 1f);
		marker.GetComponent<MeshRenderer>().material.color = color;
		overlayObjects.Add(marker);

		GameObject label = CreateLabelObject("CargoPortLinkLabel", labelText, color.a >= 0.6f ? Color.white : Color.black);
		label.transform.position = BuildWorldPosition(port.GridPosition, labelHeight);
		label.transform.rotation = Quaternion.Euler(90f, 180f, 0f);
		label.transform.localScale = Vector3.one * 0.24f;
		overlayObjects.Add(label);
	}

	private void CreateBuildingMarker(Building building, Color color, string labelText)
	{
		if (building == null || BuildingFootprintService == null)
			return;

		if (BuildingFootprintService.TryGetInteriorBounds(building.RuntimeBuildingId, out RectInt bounds, out _) == false)
			return;

		GameObject marker = CreateQuadObject("CargoPortTargetBuildingMarker");
		marker.transform.position = new Vector3(
			bounds.xMin + (bounds.width * 0.5f) - 0.5f,
			buildingMarkerHeight,
			bounds.yMin + (bounds.height * 0.5f) - 0.5f);
		marker.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
		marker.transform.localScale = new Vector3(bounds.width, bounds.height, 1f);
		marker.GetComponent<MeshRenderer>().material.color = color;
		overlayObjects.Add(marker);

		GameObject label = CreateLabelObject("CargoPortTargetBuildingLabel", labelText, Color.white);
		label.transform.position = new Vector3(
			bounds.xMin + (bounds.width * 0.5f) - 0.5f,
			labelHeight,
			bounds.yMin + (bounds.height * 0.5f) - 0.5f);
		label.transform.rotation = Quaternion.Euler(90f, 180f, 0f);
		label.transform.localScale = Vector3.one * 0.28f;
		overlayObjects.Add(label);
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
		return CargoPortService.TryQueryPorts(building.RuntimeBuildingId, ports, port => port != null && port.IsInbound == false);
	}

	private bool HasInboundPorts(Building building)
	{
		if (building == null || CargoPortService == null)
			return false;

		List<CargoPort> ports = new();
		return CargoPortService.TryQueryPorts(building.RuntimeBuildingId, ports, port => port != null && port.IsInbound);
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
