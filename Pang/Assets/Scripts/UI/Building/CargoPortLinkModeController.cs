using System.Collections.Generic;
using AYellowpaper.SerializedCollections;
using TMPro;
using UnityEngine;

public sealed class CargoPortLinkModeController : MonoBehaviour
{
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
	private string lastStatusMessage = string.Empty;
	private bool isEditing;

	private InteractionContext Interaction => GameContext.HasInstance ? GameContext.Instance.InteractionCtx : null;
	private GridService GridService => GameContext.HasInstance ? GameContext.Instance.GridService : null;
	private BuildingManager BuildingManager => GameContext.HasInstance ? GameContext.Instance.BuildingMgr : null;
	private BuildingFootprintService BuildingFootprintService => GameContext.HasInstance ? GameContext.Instance.BuildingFootprintService : null;

	public bool IsEditing => isEditing;
	public Building SourceBuilding => sourceBuilding;
	public string StatusText => BuildStatusText();
	public bool HasStatusMessage => string.IsNullOrWhiteSpace(lastStatusMessage) == false;
	public event System.Action StateChanged;
	public event System.Action<Building, Building> LinkCreated;

	public void Configure(GameObject targetOverlayQuadPrefab, GameObject targetOverlayLabelPrefab)
	{
		overlayQuadPrefab = targetOverlayQuadPrefab;
		overlayLabelPrefab = targetOverlayLabelPrefab;
	}

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
			lastStatusMessage = "Select a building before linking buildings.";
			return false;
		}

		if (HasOutboundPorts(building) == false)
		{
			lastStatusMessage = $"{building.DisplayName} has no outbound cargo ports.";
			return false;
		}

		sourceBuilding = building;
		isEditing = true;
		lastStatusMessage = string.Empty;
		Interaction?.EnterBuildingLinkMode();
		RefreshOverlay();
		StateChanged?.Invoke();
		return true;
	}

	public bool BeginLinkEdit()
	{
		sourceBuilding = null;
		isEditing = true;
		lastStatusMessage = string.Empty;
		Interaction?.EnterBuildingLinkMode();
		RefreshOverlay();
		StateChanged?.Invoke();
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

		if (TryGetBuildingAt(pos, out Building targetBuilding) == false || targetBuilding == null)
			return false;

		if (sourceBuilding == null)
		{
			if (HasOutboundPorts(targetBuilding) == false)
			{
				lastStatusMessage = $"{targetBuilding.DisplayName} has no outbound cargo ports.";
				StateChanged?.Invoke();
				return true;
			}

			sourceBuilding = targetBuilding;
			lastStatusMessage = $"Source set to {sourceBuilding.DisplayName}. Select a target building with inbound cargo ports.";
			RefreshOverlay();
			StateChanged?.Invoke();
			return true;
		}

		if (targetBuilding == sourceBuilding)
		{
			lastStatusMessage = "Select a different building as the target.";
			StateChanged?.Invoke();
			return true;
		}

		if (HasInboundPorts(targetBuilding) == false)
		{
			lastStatusMessage = $"{targetBuilding.DisplayName} has no inbound cargo ports.";
			StateChanged?.Invoke();
			return true;
		}

		if (BuildingManager == null)
			return false;

		if (BuildingManager.CanLinkBuildings(sourceBuilding, targetBuilding, out string reason) == false)
		{
			lastStatusMessage = reason;
			StateChanged?.Invoke();
			return true;
		}

		if (BuildingManager.TryLinkBuildings(sourceBuilding, targetBuilding))
		{
			lastStatusMessage = $"Linked {sourceBuilding.DisplayName} -> {targetBuilding.DisplayName}. Select another target building or right click to finish.";
			LinkCreated?.Invoke(sourceBuilding, targetBuilding);
			Interaction?.ClearSelection();
		}
		else
		{
			lastStatusMessage = "Unable to create the building link.";
		}

		RefreshOverlay();
		StateChanged?.Invoke();
		return true;
	}

	private void RefreshOverlay()
	{
		ClearOverlay();
		if (isEditing == false || Interaction == null || Interaction.Mode != InteractionContext.InteractionMode.BuildingLinkEdit)
			return;

		if (sourceBuilding == null)
			CreateSourceBuildingMarkers();
		else
		{
			CreateSourceBuildingMarker();
			CreateTargetBuildingMarkers();
		}
	}

	private void CreateSourceBuildingMarkers()
	{
		if (BuildingManager == null)
			return;

		LinkMarkerVisualConfig visual = GetMarkerVisual(LinkMarkerType.SourcePort);
		IReadOnlyList<Building> buildings = BuildingManager.RegisteredBuildings;
		for (int i = 0; i < buildings.Count; ++i)
		{
			Building building = buildings[i];
			if (building != null && HasOutboundPorts(building))
				CreateBuildingMarker(building, visual, building.DisplayName);
		}
	}

	private void CreateSourceBuildingMarker()
	{
		CreateBuildingMarker(sourceBuilding, GetMarkerVisual(LinkMarkerType.SourcePort), sourceBuilding != null ? sourceBuilding.DisplayName : "Source");
	}

	private void CreateTargetBuildingMarkers()
	{
		if (BuildingManager == null || sourceBuilding == null)
			return;

		LinkMarkerVisualConfig visual = GetMarkerVisual(LinkMarkerType.TargetBuilding);
		IReadOnlyList<Building> buildings = BuildingManager.RegisteredBuildings;
		for (int i = 0; i < buildings.Count; ++i)
		{
			Building building = buildings[i];
			if (building == null || building == sourceBuilding || HasInboundPorts(building) == false)
				continue;

			if (BuildingManager.CanLinkBuildings(sourceBuilding, building, out _) == false)
				continue;

			CreateBuildingMarker(building, visual, building.DisplayName);
		}
	}

	private void CreateBuildingMarker(Building building, LinkMarkerVisualConfig visual, string labelText)
	{
		if (building == null || BuildingFootprintService == null)
			return;

		if (BuildingFootprintService.TryGetFootprint(building.RuntimeBuildingId, out BuildingFootprintRecord footprint) == false || footprint == null)
			return;

		RectInt bounds = footprint.Bounds;

		GameObject marker = CreateQuadObject("BuildingLinkMarker");
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

		GameObject label = CreateLabelObject("BuildingLinkLabel", labelText, visual.LabelColor);
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
				0.03f,
				0.045f,
				0.28f,
				new Color(0.9f, 0.42f, 0.2f, 0.35f),
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

	private bool TryGetBuildingAt(Unity.Mathematics.int3 pos, out Building building)
	{
		building = null;
		GridCell cell = GridService?.GetCell(pos);
		if (cell == null || cell.BuildingId == 0 || BuildingManager == null)
			return false;

		return BuildingManager.TryGetBuilding(cell.BuildingId, out building) && building != null;
	}

	private static bool HasOutboundPorts(Building building)
	{
		return HasPortType<OutboundCargoPort>(building);
	}

	private static bool HasInboundPorts(Building building)
	{
		return HasPortType<InboundCargoPort>(building);
	}

	private static bool HasPortType<TPort>(Building building) where TPort : CargoPort
	{
		if (building == null)
			return false;

		IReadOnlyList<CargoPort> ports = building.OccupiedCargoPorts;
		for (int i = 0; i < ports.Count; ++i)
		{
			if (ports[i] is TPort)
				return true;
		}

		return false;
	}

	private string BuildStatusText()
	{
		if (string.IsNullOrWhiteSpace(lastStatusMessage) == false)
			return lastStatusMessage;

		if (isEditing == false)
			return "Select a building, then start building linking.";

		if (sourceBuilding == null)
			return "Select a source building with outbound cargo ports.";

		return $"Select an output target building for {sourceBuilding.DisplayName}.";
	}

	private void ResetLinkMode(bool exitInteractionMode)
	{
		isEditing = false;
		sourceBuilding = null;
		lastStatusMessage = string.Empty;
		ClearOverlay();

		if (exitInteractionMode && Interaction != null && Interaction.Mode == InteractionContext.InteractionMode.BuildingLinkEdit)
			Interaction.ExitBuildingLinkMode();

		StateChanged?.Invoke();
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
}
