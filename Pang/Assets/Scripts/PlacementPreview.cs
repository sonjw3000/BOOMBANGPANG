using System.Collections.Generic;
using Unity.Mathematics;
using Unity.VisualScripting;
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
	
	private GameObject previewPoolRoot;
	
	private GameObject possibleRoot;
	private GameObject blockedRoot;

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

	private InteractionContext Interaction => GameContext.Instance.InteractionCtx;
	private GridService GridService => GameContext.Instance.GridService;

	private void Start()
	{
		Interaction.OnMouseChangedOnPlacement += SelectedPosChanged;
		Interaction.OnPlacementChanged += OnPrefabChanged;

		previewPoolRoot = new GameObject("Preview Root");
		possibleRoot = new GameObject("possibleRoot");
		blockedRoot = new GameObject("blockedRoot");
		
		previewPoolRoot.transform.parent = transform;
		possibleRoot.transform.parent = transform;
		blockedRoot.transform.parent = transform;

		possibleTiles = new(possiblePrePool, () => { return Instantiate(possiblePrefab, possibleRoot.transform); });
		blockedTiles = new(blockedPrePool, () => { return Instantiate(blockedPrefab, blockedRoot.transform); });
	}

	public void SelectedPosChanged(int3 pos)
	{
		previewCenter = pos;

		UpdatePlacings();
	}

	public void OnPrefabChanged(PlaceableDefinition pd)
	{
		curToBePlaced = pd;

		ChangeCurrentPreview();
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

			for (int i = 0; i < renderers.Length; ++i)
				renderers[i].materials = previewMat;
		}

		currentPreview = pollingPreview[curToBePlaced.placeableID];
		currentPreview.SetActive(true);
	}
}

