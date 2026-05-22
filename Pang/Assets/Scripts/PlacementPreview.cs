using System.Collections.Generic;
using System.Text;
using TMPro;
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
	[SerializeField] private float spaceOverlayHeight = 0.02f;
	[SerializeField] private Color indoorOverlayColor = new(0.65f, 1f, 0.65f, 0.28f);

	[Header("Interaction Preview")]
	[SerializeField] private float interactionPreviewScale = 0.72f;
	[SerializeField] private float interactionLabelHeight = 0.04f;
	[SerializeField] private float interactionLabelFontSize = 3.4f;
	[SerializeField] private float interactionLabelScale = 0.2f;
	[SerializeField] private Color interactionLabelColor = Color.white;
	
	private GameObject previewPoolRoot;
	
	private GameObject possibleRoot;
	private GameObject blockedRoot;
	private GameObject spaceOverlayRoot;
	private GameObject interactionLabelRoot;

	// previewCellPos
	private int3 previewCenter = new(0);
	private readonly List<int3> possibleCells = new();
	private readonly List<int3> blockedCells = new();
	private readonly Dictionary<int3, InteractionKind> previewInteractionPoints = new();

	// 
	private GameObject currentPreview = null;
	private PlaceableDefinition curToBePlaced = null;
	
	// item pools
	private readonly Dictionary<string, GameObject> pollingPreview = new();
	private GameObjectPool possibleTiles = null;
	private GameObjectPool blockedTiles= null;
	private GameObjectPool spaceOverlayTiles = null;
	private GameObjectPool interactionLabelPool = null;

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
		interactionLabelRoot = new GameObject("interactionLabelRoot");
		
		previewPoolRoot.transform.parent = transform;
		possibleRoot.transform.parent = transform;
		blockedRoot.transform.parent = transform;
		spaceOverlayRoot.transform.parent = transform;
		interactionLabelRoot.transform.parent = transform;

		possibleTiles = new(possiblePrePool, () => { return Instantiate(possiblePrefab, possibleRoot.transform); });
		blockedTiles = new(blockedPrePool, () => { return Instantiate(blockedPrefab, blockedRoot.transform); });
		// spaceOverlayTiles = new(spaceOverlayPrePool, () => CreateSpaceOverlayQuad(spaceOverlayRoot.transform));
		interactionLabelPool = new(possiblePrePool, CreateInteractionLabel);
		spaceOverlayRoot.SetActive(false);
	}

	private void OnDestroy()
	{
		if (GameContext.HasInstance && GameContext.Instance.InteractionCtx != null)
		{
			Interaction.OnMouseChangedOnPlacement -= SelectedPosChanged;
			Interaction.OnPlacementChanged -= OnPrefabChanged;
		}
	}

	public void SelectedPosChanged(int3 pos)
	{
		previewCenter = pos;

		UpdatePlacings();
	}

	public void OnPrefabChanged(PlaceableDefinition pd)
	{
		curToBePlaced = pd;
		GridService?.SetGridBoundaryVisible(pd != null);
		//Debug.Log($"SelectionChanged: {pd.name}");
		ChangeCurrentPreview();
		UpdatePlacings();
	}

	private void UpdatePlacings()
	{
		possibleCells.Clear();
		blockedCells.Clear();
		previewInteractionPoints.Clear();

		possibleTiles.ReleaseAll();
		blockedTiles.ReleaseAll();
		interactionLabelPool?.ReleaseAll();

		if (curToBePlaced == null)
			return;

		PlacementContext ctx = new(
			center: previewCenter,
			dir: Interaction.Direction,
			def: curToBePlaced
		);

		GridService.OnCheckInstallable(ctx, possibleCells, blockedCells);
		CollectPreviewInteractionPoints(ctx);

		// update possible cells
		for (int i = 0; i < possibleCells.Count; ++i)
		{
			var possible = possibleTiles.Get();
			possible.transform.position = new Vector3(possibleCells[i].x, possibleCells[i].y, possibleCells[i].z);
			possible.transform.localScale = GetCellPreviewScale(possibleCells[i]);
		}

		for (int i = 0; i < blockedCells.Count; ++i)
		{
			var blocked = blockedTiles.Get();
			blocked.transform.position = new Vector3(blockedCells[i].x, blockedCells[i].y, blockedCells[i].z);
			blocked.transform.localScale = GetCellPreviewScale(blockedCells[i]);
		}

		foreach (var pair in previewInteractionPoints)
		{
			GameObject label = interactionLabelPool.Get();
			ConfigureInteractionLabel(label, pair.Key, pair.Value);
		}

		// position
		if (currentPreview != null)
		{
			currentPreview.transform.position = new Vector3(previewCenter.x, previewCenter.y, previewCenter.z);
			currentPreview.transform.rotation = GetRotation(Interaction.Direction);
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
		currentPreview.transform.rotation = GetRotation(Interaction.Direction);
	}

	private void CollectPreviewInteractionPoints(in PlacementContext ctx)
	{
		if (ctx.placeableDefinition == null || ctx.placeableDefinition.gridFootprint == null)
			return;

		GridFootprint footprint = ctx.placeableDefinition.gridFootprint;
		Vector2Int pivot = footprint.Pivot;

		for (int z = 0; z < footprint.height; ++z)
		{
			for (int x = 0; x < footprint.width; ++x)
			{
				FootprintCell footprintCell = footprint.Get(x, z);
				if ((footprintCell.flags & GridFlags.Interaction) == 0)
					continue;

				int3 offset = new(x - pivot.x, 0, z - pivot.y);
				int3 rotatedOffset = RotateOffset(offset, ctx.facingDirection);
				int3 target = ctx.center + rotatedOffset;

				if (GridService.GetCell(target) == null)
					continue;

				previewInteractionPoints[target] = footprintCell.interactionKind;
			}
		}
	}

	private Vector3 GetCellPreviewScale(int3 cellPos)
	{
		if (previewInteractionPoints.ContainsKey(cellPos))
			return new Vector3(interactionPreviewScale, 1f, interactionPreviewScale);

		return Vector3.one;
	}

	private GameObject CreateInteractionLabel()
	{
		GameObject label = new("InteractionPreviewLabel");
		label.transform.SetParent(interactionLabelRoot.transform, false);

		var text = label.AddComponent<TextMeshPro>();
		text.alignment = TextAlignmentOptions.Center;
		text.fontSize = interactionLabelFontSize;
		text.textWrappingMode = TextWrappingModes.NoWrap;
		text.color = interactionLabelColor;

		label.SetActive(false);
		return label;
	}

	private void ConfigureInteractionLabel(GameObject label, int3 point, InteractionKind interactionKind)
	{
		if (label == null)
			return;

		var text = label.GetComponent<TextMeshPro>();
		text.text = BuildInteractionLabel(interactionKind);
		text.color = interactionLabelColor;

		label.transform.position = new Vector3(point.x, point.y + interactionLabelHeight, point.z);
		label.transform.rotation = Quaternion.Euler(90f, 180f, 0f);
		label.transform.localScale = Vector3.one * interactionLabelScale;
	}

	private static string BuildInteractionLabel(InteractionKind interactionKind)
	{
		if (interactionKind == InteractionKind.None)
			return string.Empty;

		StringBuilder builder = new();
		AppendInteractionLabel(builder, interactionKind, InteractionKind.Pick, "PICK");
		AppendInteractionLabel(builder, interactionKind, InteractionKind.Put, "PUT");
		AppendInteractionLabel(builder, interactionKind, InteractionKind.Work, "WORK");
		AppendInteractionLabel(builder, interactionKind, InteractionKind.Charge, "CHARGE");
		return builder.ToString();
	}

	private static void AppendInteractionLabel(StringBuilder builder, InteractionKind source, InteractionKind target, string label)
	{
		if (source.HasFlag(target) == false)
			return;

		if (builder.Length > 0)
			builder.Append(" / ");

		builder.Append(label);
	}

	private static int3 RotateOffset(int3 offset, FacingDirection direction)
	{
		return direction switch
		{
			FacingDirection.North => offset,
			FacingDirection.East => new int3(offset.z, 0, -offset.x),
			FacingDirection.South => new int3(-offset.x, 0, -offset.z),
			FacingDirection.West => new int3(-offset.z, 0, offset.x),
			_ => offset
		};
	}

	private static Quaternion GetRotation(FacingDirection direction)
	{
		return direction switch
		{
			FacingDirection.North => Quaternion.identity,
			FacingDirection.East => Quaternion.Euler(0f, 90f, 0f),
			FacingDirection.South => Quaternion.Euler(0f, 180f, 0f),
			FacingDirection.West => Quaternion.Euler(0f, 270f, 0f),
			_ => Quaternion.identity
		};
	}
}
