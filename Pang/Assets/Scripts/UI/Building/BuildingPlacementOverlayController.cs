using TMPro;
using Unity.Mathematics;
using UnityEngine;
using System.Collections.Generic;

public sealed class BuildingPlacementOverlayController : MonoBehaviour
{
	[SerializeField] private BuildingFootprintService footprintService;
	[SerializeField] private float previewHeight = 0.035f;
	[SerializeField] private float labelHeight = 0.04f;
	[SerializeField] private float overlayAlpha = 0.25f;
	[SerializeField] private Color previewColor = new(0.2f, 0.7f, 0.85f, 0.4f);
	[SerializeField] private Color invalidPreviewColor = new(1f, 0.25f, 0.25f, 0.4f);
	[SerializeField] private int currentFloor = 0;
	[SerializeField] private BuildingSelectionProxy selectionProxyPrefab;
	[SerializeField] private GameObject overlayQuadPrefab;
	[SerializeField] private GameObject overlayLabelPrefab;

	private GameObject previewRoot;
	private GameObject previewQuad;
	private GameObject previewLabel;
	private GameObject proxyRoot;
	private readonly Dictionary<Building, BuildingSelectionProxy> proxies = new();
	private bool isVisible;

	private InteractionContext Interaction => GameContext.Instance.InteractionCtx;
	private BuildingManager BuildingManager => GameContext.Instance.BuildingMgr;
	private GridService GridService => GameContext.Instance.GridService;
	private BuildingFootprintService FootprintService
	{
		get
		{
			if (footprintService == null && GameContext.HasInstance)
				footprintService = GameContext.Instance.BuildingFootprintService;

			return footprintService;
		}
	}

	private void Awake()
	{
		previewRoot = new GameObject("BuildingPreviewRoot");
		proxyRoot = new GameObject("BuildingProxyRoot");
		Transform worldParent = GameContext.HasInstance ? GameContext.Instance.transform : null;
		previewRoot.transform.SetParent(worldParent, false);
		proxyRoot.transform.SetParent(worldParent, false);
		previewRoot.transform.localScale = Vector3.one;
		proxyRoot.transform.localScale = Vector3.one;
		proxyRoot.hideFlags = HideFlags.HideInHierarchy;

		previewQuad = CreateQuad("BuildingPreviewQuad", previewRoot.transform);
		previewLabel = CreateLabel("BuildingPreviewLabel", previewRoot.transform);
		previewQuad.SetActive(false);
		previewLabel.SetActive(false);
		previewRoot.SetActive(false);

		Interaction.OnBuildingPlacementPreviewChanged += HandleBuildingPlacementPreviewChanged;
		Interaction.OnBuildingPlacementConfirmed += HandleBuildingPlacementConfirmed;
		Interaction.OnResolveSelectionFallback += ResolveBuildingSelection;
	}

	private void OnDestroy()
	{
		if (GameContext.HasInstance == false || GameContext.Instance.InteractionCtx == null)
			return;

		Interaction.OnBuildingPlacementPreviewChanged -= HandleBuildingPlacementPreviewChanged;
		Interaction.OnBuildingPlacementConfirmed -= HandleBuildingPlacementConfirmed;
		Interaction.OnResolveSelectionFallback -= ResolveBuildingSelection;
	}

	public void SetOverlayVisible(bool visible)
	{
		isVisible = visible;
		if (previewRoot != null)
			previewRoot.SetActive(visible);

		if (visible == false)
		{
			HidePreview();
			if (Interaction.Mode == InteractionContext.InteractionMode.BuildingPlacement)
				Interaction.ExitBuildingPlacementMode();

			if (Interaction.SelectedObject != null && Interaction.SelectedObject.TryGetComponent<BuildingSelectionProxy>(out _))
				Interaction.ClearSelection();
		}
	}

	public void BeginCreate()
	{
		SetOverlayVisible(true);
		Interaction.EnterBuildingPlacementMode(currentFloor);
	}

	public BuildingSelectionProxy GetSelectionProxy(Building building)
	{
		if (building == null)
			return null;

		return GetOrCreateProxy(building);
	}

	private void HandleBuildingPlacementPreviewChanged(InteractionContext.BuildingPlacementPreview preview)
	{
		if (isVisible == false || Interaction.Mode != InteractionContext.InteractionMode.BuildingPlacement || preview.HasStart == false)
		{
			HidePreview();
			return;
		}

		RectInt bounds = BuildRect(preview.Start, preview.End);
		bool canCreate = FootprintService != null && FootprintService.CanCreateFootprint(preview.Floor, bounds, out _);
		Color color = canCreate ? previewColor : invalidPreviewColor;
		color.a = canCreate ? overlayAlpha : invalidPreviewColor.a;

		previewQuad.SetActive(true);
		previewLabel.SetActive(true);
		ConfigureQuad(previewQuad, bounds, color);
		ConfigureLabel(previewLabel, bounds, $"{bounds.width} x {bounds.height}", color);
	}

	private void HandleBuildingPlacementConfirmed(RectInt bounds, int floor)
	{
		if (FootprintService == null)
			return;

		if (FootprintService.TryCreateFootprint(floor, bounds, out string reason) == false)
		{
			if (string.IsNullOrWhiteSpace(reason) == false)
			{
				GameContext.Instance.FloatingTextManager?.ShowScreen(
					FloatingTextPreset.Error,
					reason,
					Input.mousePosition,
					1.3f);
				Debug.LogWarning(reason);
			}
			return;
		}

		Interaction.ExitBuildingPlacementMode();
		Interaction.ClearSelection();
	}

	private void HidePreview()
	{
		if (previewQuad != null)
			previewQuad.SetActive(false);

		if (previewLabel != null)
			previewLabel.SetActive(false);
	}

	private GameObject ResolveBuildingSelection(int3 pos)
	{
		if (isVisible == false || pos.y != currentFloor || GridService == null || BuildingManager == null)
			return null;

		GridCell cell = GridService.GetCell(pos);
		if (cell == null || cell.BuildingId == 0)
			return null;

		if (BuildingManager.TryGetBuilding(cell.BuildingId, out var building) == false || building == null)
			return null;

		return GetOrCreateProxy(building).gameObject;
	}

	private BuildingSelectionProxy GetOrCreateProxy(Building building)
	{
		if (proxies.TryGetValue(building, out var proxy) && proxy != null)
		{
			proxy.Bind(BuildingManager, building);
			return proxy;
		}

		if (selectionProxyPrefab == null)
		{
			Debug.LogError("[BuildingPlacementOverlayController] BuildingSelectionProxy prefab is missing.", this);
			return null;
		}

		proxy = Instantiate(selectionProxyPrefab, proxyRoot.transform);
		proxy.name = $"BuildingSelection_{building.DisplayName}";
		if (proxy == null)
			return null;

		proxy.gameObject.hideFlags = HideFlags.HideInHierarchy;
		proxy.Bind(BuildingManager, building);
		proxies[building] = proxy;
		return proxy;
	}

	private GameObject CreateQuad(string objectName, Transform parent)
	{
		if (overlayQuadPrefab == null)
		{
			Debug.LogError("[BuildingPlacementOverlayController] Overlay quad prefab is missing.", this);
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
			Debug.LogError("[BuildingPlacementOverlayController] Overlay label prefab is missing.", this);
			return null;
		}

		GameObject label = Instantiate(overlayLabelPrefab, parent);
		label.name = objectName;
		if (label == null)
			return null;

		var text = label.GetComponent<TextMeshPro>();
		text.fontSize = 5f;
		text.color = Color.white;
		return label;
	}

	private void ConfigureQuad(GameObject quad, RectInt bounds, Color color)
	{
		quad.transform.position = new Vector3(
			bounds.xMin + (bounds.width * 0.5f) - 0.5f,
			previewHeight,
			bounds.yMin + (bounds.height * 0.5f) - 0.5f
		);
		quad.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
		quad.transform.localScale = new Vector3(bounds.width, bounds.height, 1f);

		var renderer = quad.GetComponent<MeshRenderer>();
		renderer.material.color = color;
	}

	private void ConfigureLabel(GameObject label, RectInt bounds, string textValue, Color backgroundColor)
	{
		var text = label.GetComponent<TextMeshPro>();
		text.text = textValue;
		text.color = GetReadableTextColor(backgroundColor);

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
