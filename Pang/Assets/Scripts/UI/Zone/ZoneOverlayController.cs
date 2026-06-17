using System.Collections.Generic;
using TMPro;
using Unity.Mathematics;
using UnityEngine;

public class ZoneOverlayController : MonoBehaviour
{
	private sealed class ZoneVisual
	{
		public GameObject Quad;
		public GameObject Label;
		public ZoneSelectionProxy Proxy;
	}

	[SerializeField] private ZoneManager zoneManager;
	[SerializeField] private int previewPoolSize = 8;
	[SerializeField] private float zoneOverlayHeight = 0.02f;
	[SerializeField] private float labelHeight = 0.03f;
	[SerializeField] private float previewHeight = 0.035f;
	[SerializeField] private float overlayAlpha = 0.25f;
	[SerializeField] private Color invalidPreviewColor = new(1f, 0.25f, 0.25f, 0.4f);
	[SerializeField] private int currentFloor = 0;
	[SerializeField] private ZoneSelectionProxy selectionProxyPrefab;
	[SerializeField] private GameObject overlayQuadPrefab;
	[SerializeField] private GameObject overlayLabelPrefab;

	private readonly Dictionary<ZoneArea, ZoneSelectionProxy> proxies = new();
	private readonly Dictionary<ZoneArea, ZoneVisual> activeVisuals = new();

	private GameObjectPool quadPool;
	private GameObjectPool labelPool;
	private GameObject overlayRoot;
	private GameObject previewRoot;
	private GameObject proxyRoot;
	private GameObject previewQuad;
	private GameObject previewLabel;
	private Building currentBuilding;
	private bool isVisible;
	private bool buildingModeActive;
	private bool globalZoneModeActive;
	private ZoneType globalZoneType = ZoneType.RocketLanding;

	public Building CurrentBuilding => currentBuilding;
	public bool BuildingModeActive => buildingModeActive;

	public event System.Action<Building> ActiveBuildingChanged;
	public event System.Action<bool> BuildingModeChanged;

	private InteractionContext Interaction => GameContext.Instance.InteractionCtx;
	private BuildingFootprintService BuildingFootprintService => GameContext.HasInstance ? GameContext.Instance.BuildingFootprintService : null;
	private GridService GridService => GameContext.HasInstance ? GameContext.Instance.GridService : null;
	private BuildingManager BuildingManager => GameContext.HasInstance ? GameContext.Instance.BuildingMgr : null;
	private bool IsFacilityPlacementActive => GameContext.HasInstance
		&& GameContext.Instance.InteractionCtx != null
		&& Interaction.Mode == InteractionContext.InteractionMode.FacilityPlacement;

	private void Awake()
	{
		if (zoneManager == null)
			zoneManager = GetComponent<ZoneManager>();

		overlayRoot = new GameObject("ZoneOverlayRoot");
		previewRoot = new GameObject("ZonePreviewRoot");
		proxyRoot = new GameObject("ZoneProxyRoot");

		overlayRoot.transform.SetParent(transform, false);
		previewRoot.transform.SetParent(transform, false);
		proxyRoot.transform.SetParent(transform, false);

		quadPool = new GameObjectPool(previewPoolSize, () => CreateQuad("ZoneOverlayQuad", overlayRoot.transform));
		labelPool = new GameObjectPool(previewPoolSize, () => CreateLabel("ZoneOverlayLabel", overlayRoot.transform));

		previewQuad = CreateQuad("ZonePreviewQuad", previewRoot.transform);
		previewLabel = CreateLabel("ZonePreviewLabel", previewRoot.transform);
		previewQuad.SetActive(false);
		previewLabel.SetActive(false);

		overlayRoot.SetActive(false);
		previewRoot.SetActive(false);
		proxyRoot.SetActive(true);

		if (zoneManager != null)
		{
			zoneManager.OnZoneAdded += HandleZoneListChanged;
			zoneManager.OnZoneChanged += HandleZoneListChanged;
			zoneManager.OnZonesRebuilt += RefreshVisibleZones;
			zoneManager.OnZoneRemoved += HandleZoneRemoved;
		}

		Interaction.OnResolveSelectionFallback += ResolveZoneSelection;
		Interaction.OnHandleBuildingSelection += HandleBuildingModeSelection;
		Interaction.OnModeChanged += HandleInteractionModeChanged;
		Interaction.OnZonePlacementPreviewChanged += HandleZonePlacementPreviewChanged;
		Interaction.OnZonePlacementConfirmed += HandleZonePlacementConfirmed;
	}

	private void OnDestroy()
	{
		if (zoneManager != null)
		{
			zoneManager.OnZoneAdded -= HandleZoneListChanged;
			zoneManager.OnZoneChanged -= HandleZoneListChanged;
			zoneManager.OnZonesRebuilt -= RefreshVisibleZones;
			zoneManager.OnZoneRemoved -= HandleZoneRemoved;
		}

		if (GameContext.HasInstance && GameContext.Instance.InteractionCtx != null)
		{
			Interaction.OnResolveSelectionFallback -= ResolveZoneSelection;
			Interaction.OnHandleBuildingSelection -= HandleBuildingModeSelection;
			Interaction.OnModeChanged -= HandleInteractionModeChanged;
			Interaction.OnZonePlacementPreviewChanged -= HandleZonePlacementPreviewChanged;
			Interaction.OnZonePlacementConfirmed -= HandleZonePlacementConfirmed;
		}
	}

	public void SetBuildingModeActive(bool active)
	{
		if (buildingModeActive == active)
			return;

		buildingModeActive = active;
		BuildingModeChanged?.Invoke(active);
		if (active == false)
		{
			HideAllVisuals();
			HidePreview();
			if (Interaction.Mode == InteractionContext.InteractionMode.BuildingZoneEdit)
				Interaction.ExitZonePlacementMode();

			if (Interaction.SelectedObject != null && Interaction.SelectedObject.TryGetComponent<ZoneSelectionProxy>(out _))
				Interaction.ClearSelection();

			SetActiveBuilding(null);
		}

		RefreshOverlayState();
	}

	public void SetGlobalZoneModeActive(bool active, ZoneType zoneType, int floor = 0)
	{
		globalZoneModeActive = active;
		globalZoneType = zoneType;
		currentFloor = floor;
		if (active)
			currentBuilding = null;
		if (active == false)
		{
			HideAllVisuals();
			HidePreview();
			if (Interaction.Mode == InteractionContext.InteractionMode.BuildingZoneEdit)
				Interaction.ExitZonePlacementMode();

			if (Interaction.SelectedObject != null && Interaction.SelectedObject.TryGetComponent<ZoneSelectionProxy>(out _))
				Interaction.ClearSelection();
		}

		RefreshOverlayState();
	}

	public void SetOverlayVisible(bool visible, Building building = null)
	{
		bool buildingChanged = visible && building != null && currentBuilding != building;
		if (visible && building != null)
			SetActiveBuilding(building);

		if (isVisible == visible && buildingChanged == false)
		{
			RefreshOverlayState();
			return;
		}

		isVisible = visible;
		if (visible == false && buildingModeActive == false)
		{
			HideAllVisuals();
			HidePreview();

			if (Interaction.Mode == InteractionContext.InteractionMode.BuildingZoneEdit)
				Interaction.ExitZonePlacementMode();

			if (Interaction.SelectedObject != null && Interaction.SelectedObject.TryGetComponent<ZoneSelectionProxy>(out _))
				Interaction.ClearSelection();

			SetActiveBuilding(null);
		}

		RefreshOverlayState();
	}

	public void BeginCreate(ZoneType zoneType)
	{
		if (currentBuilding == null)
			return;

		BeginCreate(zoneType, currentBuilding);
	}

	public void BeginCreate(ZoneType zoneType, Building building)
	{
		if (building == null)
			return;

		SetBuildingModeActive(true);
		SetOverlayVisible(true, building);
		if (currentBuilding == null)
			return;

		Interaction.EnterZonePlacementMode(zoneType, currentFloor);
	}

	public void BeginCreateGlobal(ZoneType zoneType, int floor)
	{
		globalZoneType = zoneType;
		currentFloor = floor;
		globalZoneModeActive = true;
		currentBuilding = null;
		SetOverlayVisible(true, null);
		Interaction.EnterZonePlacementMode(zoneType, currentFloor);
	}

	public ZoneSelectionProxy GetSelectionProxy(ZoneArea zone)
	{
		if (zone == null)
			return null;

		return GetOrCreateProxy(zone);
	}

	private bool HandleBuildingModeSelection(int3 pos)
	{
		if (buildingModeActive == false || GridService == null || BuildingManager == null)
			return false;

		GridCell cell = GridService.GetCell(pos);
		if (cell == null || cell.BuildingId == 0)
		{
			if (Interaction.SelectedObject != null)
				Interaction.ClearSelection();

			return true;
		}

		if (BuildingManager.TryGetBuilding(cell.BuildingId, out Building building) == false || building == null)
			return true;

		if (currentBuilding == null || currentBuilding.RuntimeBuildingId != building.RuntimeBuildingId)
		{
			SetActiveBuilding(building);
			if (Interaction.SelectedObject != null)
				Interaction.ClearSelection();
			return true;
		}

		if (pos.y != currentFloor)
		{
			if (Interaction.SelectedObject != null)
				Interaction.ClearSelection();
			return true;
		}

		if (zoneManager != null
			&& zoneManager.TryGetZoneAt(pos, out ZoneArea zone)
			&& zone != null
			&& zone.RuntimeBuildingId == currentBuilding.RuntimeBuildingId
			&& zone.Floor == currentFloor)
		{
			Interaction.SelectObject(GetOrCreateProxy(zone).gameObject);
			return true;
		}

		if (Interaction.SelectedObject != null)
			Interaction.ClearSelection();
		return true;
	}

	private void HandleZoneListChanged(ZoneArea zone)
	{
		if (IsOverlayActive)
			RefreshVisibleZones();
	}

	private void HandleInteractionModeChanged(InteractionContext.InteractionDomain domain, InteractionContext.InteractionAction action)
	{
		RefreshOverlayState();
	}

	private void HandleZoneRemoved(ZoneArea zone)
	{
		if (zone != null && proxies.TryGetValue(zone, out ZoneSelectionProxy proxy))
		{
			if (Interaction.SelectedObject == proxy.gameObject)
				Interaction.ClearSelection();

			Destroy(proxy.gameObject);
			proxies.Remove(zone);
		}

		if (IsOverlayActive)
			RefreshVisibleZones();
	}

	private GameObject ResolveZoneSelection(int3 pos)
	{
		if (IsOverlayActive == false || zoneManager == null || pos.y != currentFloor)
			return null;

		if (zoneManager.TryGetZoneAt(pos, out ZoneArea zone) == false)
			return null;

		if (globalZoneModeActive)
		{
			if (zone.RuntimeBuildingId != 0 || zone.Type != globalZoneType)
				return null;
		}
		else
		{
			if (currentBuilding == null || zone.RuntimeBuildingId != currentBuilding.RuntimeBuildingId)
				return null;
		}

		return GetOrCreateProxy(zone).gameObject;
	}

	private void HandleZonePlacementConfirmed(ZoneType zoneType, RectInt bound, int floor)
	{
		if (zoneManager == null || floor != currentFloor)
			return;

		if (currentBuilding != null)
		{
			if (zoneManager.CanPlaceZone(currentBuilding, floor, bound) == false)
			{
				Debug.LogWarning($"Cannot create zone {zoneType}: invalid bounds {bound} for building {currentBuilding.DisplayName}");
				return;
			}

			ZoneArea zone = zoneManager.AddZone(currentBuilding, zoneType, bound, floor);
			if (zone == null)
				return;

			RefreshVisibleZones();
			Interaction.ExitZonePlacementMode();
			Interaction.ClearSelection();
			return;
		}

		if (globalZoneModeActive == false || zoneType != globalZoneType || zoneManager.CanPlaceGlobalZone(zoneType, floor, bound) == false)
		{
			Debug.LogWarning($"Cannot create global zone {zoneType}: invalid bounds {bound}.");
			return;
		}

		ZoneArea createdZone = zoneManager.AddGlobalZone(zoneType, bound, floor);
		if (createdZone == null)
			return;

		RefreshVisibleZones();
		Interaction.ExitZonePlacementMode();
		Interaction.ClearSelection();
	}

	private void HandleZonePlacementPreviewChanged(InteractionContext.ZonePlacementPreview preview)
	{
		if (IsOverlayActive == false || Interaction.Mode != InteractionContext.InteractionMode.BuildingZoneEdit || preview.HasStart == false)
		{
			HidePreview();
			return;
		}

		RectInt bound = BuildRect(preview.Start, preview.End);
		bool canPlace = false;
		if (zoneManager != null)
		{
			if (currentBuilding != null)
				canPlace = zoneManager.CanPlaceZone(currentBuilding, preview.Floor, bound);
			else if (globalZoneModeActive && preview.ZoneType == globalZoneType)
				canPlace = zoneManager.CanPlaceGlobalZone(preview.ZoneType, preview.Floor, bound);
		}

		Color color = canPlace ? zoneManager.GetZoneColor(preview.ZoneType) : invalidPreviewColor;
		color.a = canPlace ? overlayAlpha : invalidPreviewColor.a;

		previewQuad.SetActive(true);
		previewLabel.SetActive(true);
		ConfigureQuad(previewQuad, bound, color, previewHeight);
		ConfigureLabel(
			previewLabel,
			bound,
			$"{preview.ZoneType}\n{bound.width} x {bound.height}",
			color
		);
	}

	private void RefreshVisibleZones()
	{
		HideAllVisuals();
		if (IsOverlayActive == false || zoneManager == null)
			return;

		bool showAllZones = IsFacilityPlacementActive;
		IReadOnlyList<ZoneArea> zonesToRender = globalZoneModeActive
			? zoneManager.RegisteredZones
			: showAllZones
			? zoneManager.RegisteredZones
			: currentBuilding != null
				? zoneManager.GetZonesForBuilding(currentBuilding.RuntimeBuildingId)
				: null;

		if (zonesToRender == null)
			return;

		for (int i = 0; i < zonesToRender.Count; ++i)
		{
			ZoneArea zone = zonesToRender[i];
			if (zone == null || zone.Floor != currentFloor)
				continue;

			if (globalZoneModeActive)
			{
				if (zone.Type != globalZoneType || zone.RuntimeBuildingId != 0)
					continue;
			}
			else if (showAllZones == false && (currentBuilding == null || zone.RuntimeBuildingId != currentBuilding.RuntimeBuildingId))
				continue;

			GameObject quad = quadPool.Get();
			GameObject label = labelPool.Get();
			ZoneSelectionProxy proxy = GetOrCreateProxy(zone);
			Color zoneColor = zoneManager.GetZoneColor(zone.Type);
			zoneColor.a = overlayAlpha;

			ConfigureQuad(quad, zone.Bounds, zoneColor, zoneOverlayHeight);
			ConfigureLabel(label, zone.Bounds, zone.DisplayName, zoneColor);

			activeVisuals[zone] = new ZoneVisual
			{
				Quad = quad,
				Label = label,
				Proxy = proxy,
			};
		}
	}

	private void HideAllVisuals()
	{
		quadPool?.ReleaseAll();
		labelPool?.ReleaseAll();
		activeVisuals.Clear();
	}

	private void HidePreview()
	{
		if (previewQuad != null)
			previewQuad.SetActive(false);

		if (previewLabel != null)
			previewLabel.SetActive(false);
	}

	private void SetActiveBuilding(Building building)
	{
		currentBuilding = building;
		if (building == null || BuildingFootprintService == null)
		{
			ActiveBuildingChanged?.Invoke(currentBuilding);
			RefreshOverlayState();
			return;
		}

		if (BuildingFootprintService.TryGetFootprint(building.RuntimeBuildingId, out BuildingFootprintRecord footprint) && footprint != null)
			currentFloor = footprint.Floor;

		ActiveBuildingChanged?.Invoke(currentBuilding);
		RefreshOverlayState();
	}

	private bool IsOverlayActive => isVisible || (buildingModeActive && currentBuilding != null) || globalZoneModeActive || IsFacilityPlacementActive;

	private void RefreshOverlayState()
	{
		bool shouldShow = IsOverlayActive;
		if (overlayRoot != null)
			overlayRoot.SetActive(shouldShow);

		if (previewRoot != null)
			previewRoot.SetActive(shouldShow);

		if (shouldShow)
		{
			RefreshVisibleZones();
		}
		else
		{
			HideAllVisuals();
			HidePreview();
		}
	}

	private ZoneSelectionProxy GetOrCreateProxy(ZoneArea zone)
	{
		if (proxies.TryGetValue(zone, out ZoneSelectionProxy proxy) && proxy != null)
		{
			proxy.Bind(zoneManager, zone);
			return proxy;
		}

		if (selectionProxyPrefab == null)
		{
			Debug.LogError("[ZoneOverlayController] ZoneSelectionProxy prefab is missing.", this);
			return null;
		}

		proxy = Instantiate(selectionProxyPrefab, proxyRoot.transform);
		proxy.name = $"ZoneSelection_{zone.DisplayName}";
		if (proxy == null)
			return null;

		proxy.gameObject.hideFlags = HideFlags.HideInHierarchy;
		proxy.Bind(zoneManager, zone);
		proxies[zone] = proxy;
		return proxy;
	}

	private GameObject CreateQuad(string objectName, Transform parent)
	{
		if (overlayQuadPrefab == null)
		{
			Debug.LogError("[ZoneOverlayController] Overlay quad prefab is missing.", this);
			return null;
		}

		GameObject quad = Instantiate(overlayQuadPrefab, parent);
		quad.name = objectName;
		return quad;
	}

	private GameObject CreateLabel(string objectName, Transform parent)
	{
		if (overlayLabelPrefab == null)
		{
			Debug.LogError("[ZoneOverlayController] Overlay label prefab is missing.", this);
			return null;
		}

		GameObject label = Instantiate(overlayLabelPrefab, parent);
		label.name = objectName;
		if (label == null)
			return null;

		TextMeshPro text = label.GetComponent<TextMeshPro>();
		text.fontSize = 5f;
		text.color = Color.white;

		return label;
	}

	private void ConfigureQuad(GameObject quad, RectInt bounds, Color color, float height)
	{
		quad.transform.position = new Vector3(
			bounds.xMin + (bounds.width * 0.5f) - 0.5f,
			height,
			bounds.yMin + (bounds.height * 0.5f) - 0.5f
		);
		quad.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
		quad.transform.localScale = new Vector3(bounds.width, bounds.height, 1f);

		MeshRenderer renderer = quad.GetComponent<MeshRenderer>();
		renderer.material.color = color;
	}

	private void ConfigureLabel(GameObject label, RectInt bounds, string textValue, Color zoneColor)
	{
		TextMeshPro text = label.GetComponent<TextMeshPro>();
		text.text = textValue;
		text.color = GetReadableTextColor(zoneColor);

		label.transform.position = new Vector3(
			bounds.xMin + (bounds.width * 0.5f) - 0.5f,
			labelHeight,
			bounds.yMin + (bounds.height * 0.5f) - 0.5f
		);
		label.transform.rotation = Quaternion.Euler(90f, 180f, 0f);

		float scale = Mathf.Clamp(Mathf.Min(bounds.width, bounds.height) / 3f, 0.35f, 1.5f);
		label.transform.localScale = Vector3.one * scale;
	}

	private static RectInt BuildRect(in int3 start, in int3 end)
	{
		int minX = Mathf.Min(start.x, end.x);
		int minZ = Mathf.Min(start.z, end.z);
		int maxX = Mathf.Max(start.x, end.x);
		int maxZ = Mathf.Max(start.z, end.z);
		return new RectInt(minX, minZ, (maxX - minX) + 1, (maxZ - minZ) + 1);
	}

	private static Color GetReadableTextColor(Color background)
	{
		float luminance = (background.r * 0.299f) + (background.g * 0.587f) + (background.b * 0.114f);
		return luminance > 0.55f ? Color.black : Color.white;
	}
}
