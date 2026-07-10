using System.Collections.Generic;
using TMPro;
using Unity.Mathematics;
using UnityEngine;

public sealed class BuildingPlacementOverlayController : MonoBehaviour
{
	[SerializeField] private BuildingFootprintService footprintService;
	[SerializeField] private BuildingType selectedBuildingType = BuildingType.Staging;
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
	private GameObject previewLabel;
	private GameObject proxyRoot;
	private readonly List<GameObject> previewCells = new();
	private readonly List<MeshRenderer> previewRenderers = new();
	private readonly Dictionary<Building, BuildingSelectionProxy> proxies = new();
	private bool isVisible;

	public BuildingType SelectedBuildingType => NormalizeSelectableBuildingType(selectedBuildingType);

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

		previewLabel = CreateLabel("BuildingPreviewLabel", previewRoot.transform);
		if (previewLabel != null)
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

		if (visible)
			return;

		HidePreview();
		if (Interaction.Mode == InteractionContext.InteractionMode.BuildingPlacement)
			Interaction.ExitBuildingPlacementMode();

		if (Interaction.SelectedObject != null && Interaction.SelectedObject.TryGetComponent<BuildingSelectionProxy>(out _))
			Interaction.ClearSelection();
	}

	public void BeginCreate()
	{
		SetOverlayVisible(true);
		Interaction.EnterBuildingPlacementMode(currentFloor);
	}

	public void SetSelectedBuildingType(BuildingType buildingType)
	{
		selectedBuildingType = NormalizeSelectableBuildingType(buildingType);
	}

	private static BuildingType NormalizeSelectableBuildingType(BuildingType buildingType)
	{
		return buildingType == BuildingType.Generic ? BuildingType.Staging : buildingType;
	}

	public BuildingSelectionProxy GetSelectionProxy(Building building)
	{
		if (building == null)
			return null;

		return GetOrCreateProxy(building);
	}

	private void HandleBuildingPlacementPreviewChanged(InteractionContext.BuildingPlacementPreview preview)
	{
		BuildingFootprintPreset preset = FootprintService != null ? FootprintService.ActivePreset : null;
		if (isVisible == false ||
			Interaction.Mode != InteractionContext.InteractionMode.BuildingPlacement ||
			preview.IsActive == false ||
			preset == null ||
			preset.IsValid == false)
		{
			HidePreview();
			return;
		}

		bool canCreate = FootprintService.CanCreateFootprint(preview.Floor, preview.Center, out _);
		Color cellColor = canCreate ? previewColor : invalidPreviewColor;
		cellColor.a = canCreate ? overlayAlpha : invalidPreviewColor.a;
		ShowPreviewCells(preset, preview.Center, cellColor);
		ConfigureLabel(
			previewLabel,
			preview.Center,
			$"{BuildingTypeUtility.ToDisplayString(selectedBuildingType)}\nDiameter {preset.Width}",
			cellColor,
			preset.Width);
	}

	private void HandleBuildingPlacementConfirmed(int3 center, int floor)
	{
		if (FootprintService == null)
			return;

		if (FootprintService.TryCreateFootprint(floor, center, selectedBuildingType, out string reason) == false)
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

	private void ShowPreviewCells(BuildingFootprintPreset preset, in int3 center, Color color)
	{
		int visibleCellIndex = 0;
		for (int z = 0; z < preset.Height; ++z)
		{
			for (int x = 0; x < preset.Width; ++x)
			{
				BuildingFootprintCell footprintCell = preset.Get(x, z);
				if (footprintCell.IsOwned == false)
					continue;

				EnsurePreviewCellCount(visibleCellIndex + 1);
				if (visibleCellIndex >= previewCells.Count)
					return;

				GameObject quad = previewCells[visibleCellIndex];
				MeshRenderer quadRenderer = previewRenderers[visibleCellIndex];
				quad.SetActive(true);
				quad.transform.position = new Vector3(
					center.x + x - preset.Pivot.x,
					previewHeight,
					center.z + z - preset.Pivot.y);
				quad.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
				quad.transform.localScale = Vector3.one;
				quadRenderer.material.color = footprintCell.IsWall
					? Color.Lerp(color, Color.black, 0.28f)
					: color;
				visibleCellIndex += 1;
			}
		}

		for (int i = visibleCellIndex; i < previewCells.Count; ++i)
			previewCells[i].SetActive(false);
	}

	private void EnsurePreviewCellCount(int count)
	{
		while (previewCells.Count < count)
		{
			GameObject quad = CreateQuad($"BuildingPreviewCell_{previewCells.Count}", previewRoot.transform);
			if (quad == null)
				return;

			previewCells.Add(quad);
			previewRenderers.Add(quad.GetComponent<MeshRenderer>());
		}
	}

	private void HidePreview()
	{
		for (int i = 0; i < previewCells.Count; ++i)
			previewCells[i].SetActive(false);

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

		if (BuildingManager.TryGetBuilding(cell.BuildingId, out Building building) == false || building == null)
			return null;

		return GetOrCreateProxy(building).gameObject;
	}

	private BuildingSelectionProxy GetOrCreateProxy(Building building)
	{
		if (proxies.TryGetValue(building, out BuildingSelectionProxy proxy) && proxy != null)
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
		TextMeshPro text = label.GetComponent<TextMeshPro>();
		text.fontSize = 5f;
		text.color = Color.white;
		return label;
	}

	private void ConfigureLabel(GameObject label, in int3 center, string textValue, Color backgroundColor, int diameter)
	{
		if (label == null)
			return;

		TextMeshPro text = label.GetComponent<TextMeshPro>();
		text.text = textValue;
		text.color = GetReadableTextColor(backgroundColor);
		label.transform.position = new Vector3(center.x, labelHeight, center.z);
		label.transform.rotation = Quaternion.Euler(90f, 180f, 0f);
		float scale = Mathf.Clamp(diameter / 3f, 0.35f, 1.5f);
		label.transform.localScale = Vector3.one * scale;
		label.SetActive(true);
	}

	private static Color GetReadableTextColor(Color background)
	{
		float luminance = (background.r * 0.299f) + (background.g * 0.587f) + (background.b * 0.114f);
		return luminance > 0.55f ? Color.black : Color.white;
	}
}
