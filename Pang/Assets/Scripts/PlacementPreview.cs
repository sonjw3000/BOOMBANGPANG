using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

public class PlacementPreview : MonoBehaviour
{
	[Header("Preview Material")]
	[SerializeField] private Material[] previewMat;

	[Header("Possible")]
	[SerializeField] private int possiblePrePool;
	[SerializeField] private GameObject possiblePrefab;

	[Header("Blocked")]
	[SerializeField] private int blockedPrePool;
	[SerializeField] private GameObject blockedPrefab;

	[Header("Space Overlay")]
	[SerializeField] private int spaceOverlayPrePool = 128;
	[SerializeField] private float spaceOverlayHeight = 0.02f;
	[SerializeField] private Color indoorOverlayColor = new(0.65f, 1f, 0.65f, 0.28f);
	
	private GameObject previewPoolRoot;
	
	private GameObject possibleRoot;
	private GameObject blockedRoot;
	private GameObject spaceOverlayRoot;

	// previewCellPos
	private int3 previewCenter = new(0);
	private readonly List<int3> possibleCells = new();
	private readonly List<int3> blockedCells = new();

	// 
	private GameObject currentPreview = null;
	private PlaceableDefinition curToBePlaced = null;
	
	// item pools
	private readonly Dictionary<string, GameObject> pollingPreview = new();
	private GameObjectPool possibleTiles = null;
	private GameObjectPool blockedTiles= null;
	private GameObjectPool spaceOverlayTiles = null;

	private InteractionContext Interaction => GameContext.Instance.InteractionCtx;
	private GridService GridService => GameContext.Instance.GridService;

	private void Start()
	{
		Interaction.OnMouseChangedOnPlacement += SelectedPosChanged;
		Interaction.OnPlacementChanged += OnPrefabChanged;

		previewPoolRoot = new GameObject("Preview Root");
		possibleRoot = new GameObject("possibleRoot");
		blockedRoot = new GameObject("blockedRoot");
		spaceOverlayRoot = new GameObject("spaceOverlayRoot");
		
		previewPoolRoot.transform.parent = transform;
		possibleRoot.transform.parent = transform;
		blockedRoot.transform.parent = transform;
		spaceOverlayRoot.transform.parent = transform;

		possibleTiles = new(possiblePrePool, () => { return Instantiate(possiblePrefab, possibleRoot.transform); });
		blockedTiles = new(blockedPrePool, () => { return Instantiate(blockedPrefab, blockedRoot.transform); });
		spaceOverlayTiles = new(spaceOverlayPrePool, () => CreateSpaceOverlayQuad(spaceOverlayRoot.transform));
		spaceOverlayRoot.SetActive(false);

		if (GridService != null)
			GridService.OnSpaceRegionsChanged += HandleSpaceRegionsChanged;
	}

	private void OnDestroy()
	{
		if (GameContext.HasInstance && GameContext.Instance.InteractionCtx != null)
		{
			Interaction.OnMouseChangedOnPlacement -= SelectedPosChanged;
			Interaction.OnPlacementChanged -= OnPrefabChanged;
		}

		if (GameContext.HasInstance && GridService != null)
			GridService.OnSpaceRegionsChanged -= HandleSpaceRegionsChanged;
	}

	public void SelectedPosChanged(int3 pos)
	{
		bool floorChanged = previewCenter.y != pos.y;
		previewCenter = pos;

		if (floorChanged)
			RefreshSpaceOverlay();

		UpdatePlacings();
	}

	public void OnPrefabChanged(PlaceableDefinition pd)
	{
		curToBePlaced = pd;
		//Debug.Log($"SelectionChanged: {pd.name}");
		ChangeCurrentPreview();
		RefreshSpaceOverlay();
		UpdatePlacings();
	}

	private void UpdatePlacings()
	{
		possibleCells.Clear();
		blockedCells.Clear();

		possibleTiles.ReleaseAll();
		blockedTiles.ReleaseAll();

		if (curToBePlaced == null)
			return;

		PlacementContext ctx = new(
			center: previewCenter,
			dir: Interaction.Direction,
			def: curToBePlaced
		);

		GridService.OnCheckInstallable(ctx, possibleCells, blockedCells);

		// update possible cells
		for (int i = 0; i < possibleCells.Count; ++i)
		{
			var possible = possibleTiles.Get();
			possible.transform.position = new Vector3(possibleCells[i].x, possibleCells[i].y, possibleCells[i].z);
		}

		for (int i = 0; i < blockedCells.Count; ++i)
		{
			var blocked = blockedTiles.Get();
			blocked.transform.position = new Vector3(blockedCells[i].x, blockedCells[i].y, blockedCells[i].z);
		}

		// position
		if (currentPreview != null)
		{
			currentPreview.transform.position = new Vector3(previewCenter.x, previewCenter.y, previewCenter.z);
		}
	}

	private void ChangeCurrentPreview()
	{
		if (currentPreview != null)
			currentPreview.SetActive(false);

		if (curToBePlaced == null)
		{
			currentPreview = null;
			return;
		}

		if (pollingPreview.ContainsKey(curToBePlaced.placeableID) == false)
		{
			pollingPreview[curToBePlaced.placeableID] = Instantiate(curToBePlaced.prefab, previewPoolRoot.transform);
			var renderers = pollingPreview[curToBePlaced.placeableID].GetComponentsInChildren<Renderer>();

			Debug.Log($"[PlacementPreview] Preview Prefab Instantiated: {curToBePlaced.prefab.name}");

			for (int i = 0; i < renderers.Length; ++i)
				renderers[i].materials = previewMat;
		}

		currentPreview = pollingPreview[curToBePlaced.placeableID];
		currentPreview.SetActive(true);
	}

	private void HandleSpaceRegionsChanged()
	{
		RefreshSpaceOverlay();
	}

	private void RefreshSpaceOverlay()
	{
		spaceOverlayTiles.ReleaseAll();

		if (curToBePlaced == null || GridService == null || GridService.IsReady == false)
		{
			HideSpaceOverlay();
			return;
		}

		int floor = Mathf.Clamp(previewCenter.y, 0, GridService.MapSize.y - 1);
		spaceOverlayRoot.SetActive(true);

		for (int x = 0; x < GridService.MapSize.x; ++x)
		{
			for (int z = 0; z < GridService.MapSize.z; ++z)
			{
				GridCell cell = GridService.GetCell(new int3(x, floor, z));
				if (cell == null || cell.IsIndoor == false)
					continue;

				GameObject overlay = spaceOverlayTiles.Get();
				overlay.transform.position = new Vector3(x, spaceOverlayHeight, z);
				overlay.GetComponent<MeshRenderer>().material.color = indoorOverlayColor;
			}
		}
	}

	private void HideSpaceOverlay()
	{
		spaceOverlayTiles?.ReleaseAll();
		if (spaceOverlayRoot != null)
			spaceOverlayRoot.SetActive(false);
	}

	private GameObject CreateSpaceOverlayQuad(Transform parent)
	{
		GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
		quad.name = "SpaceOverlayQuad";
		quad.transform.SetParent(parent, false);
		quad.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
		quad.transform.localScale = Vector3.one;

		var collider = quad.GetComponent<Collider>();
		if (collider != null)
			Destroy(collider);

		var renderer = quad.GetComponent<MeshRenderer>();
		renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
		renderer.receiveShadows = false;
		renderer.material = CreateOverlayMaterial();

		return quad;
	}

	private Material CreateOverlayMaterial()
	{
		Shader shader = Shader.Find("Sprites/Default");
		if (shader == null)
			shader = Shader.Find("Unlit/Color");

		Material material = new(shader);
		material.renderQueue = 3000;
		return material;
	}
}
