using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

public class PlacementPreview : MonoBehaviour
{
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
	private List<int3> possibleCells = new();
	private List<int3> blockedCells = new();

	// 
	private GameObject currentPreview = null;
	private PlaceableDefinition curToBePlaced = null;
	
	// item pools
	private Dictionary<string, GameObject> pollingPreview = new();
	private GameObjectPool possibleTiles = null;
	private GameObjectPool blockedTiles= null;

	private InteractionContext Interaction => GameContext.Instance.InteractionCtx;
	private PlaceableDefinition ToBePlaced => Interaction.ToBePlaced;
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
		
		UpdatePlacings();
	}

	private void UpdatePlacings()
	{
		possibleCells.Clear();
		blockedCells.Clear();

		PlacementContext ctx = new(
			center: previewCenter,
			dir: Interaction.Direction,
			def: ToBePlaced
		);

		GridService.OnCheckInstallable(ctx, possibleCells, blockedCells);

		possibleTiles.ReleaseAll();
		blockedTiles.ReleaseAll();

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
}

