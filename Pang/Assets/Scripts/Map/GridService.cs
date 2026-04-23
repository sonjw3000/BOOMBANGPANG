using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

public class GridService : MonoBehaviour
{
	[SerializeField] private GameObject placeableParent;
	[SerializeField] private GameObject gridParent;

	private readonly GridMap gridMap = new();

	public GridCell[,,] Map => gridMap.Map;
	public int3 MapSize => gridMap.MapSize;

	public bool IsPassable(in int3 pos)
	{
		if (gridMap.IsInBound(pos) == false)
			return false;
		return gridMap.Map[pos.x, pos.y, pos.z].IsPassable;
	}

	public bool IsBlocked(in int3 pos)
	{
		if (gridMap.IsInBound(pos) == false)
			return true;
		return gridMap.Map[pos.x, pos.y, pos.z].IsBlocked;
	}


	private readonly Dictionary<GameObject, PlacementContext> placedObjects = new();

	public event System.Action<PlacementContext> OnPlaceableInstalled;

	private EconomyService Economy => GameContext.Instance.EconomyService;

	public void OnGameStart()
	{
		GameObject tileFloor = GameObject.CreatePrimitive(PrimitiveType.Quad);

		tileFloor.transform.localScale = new Vector3Int(gridMap.MapSize.x, gridMap.MapSize.z, gridMap.MapSize.y);
		tileFloor.transform.Rotate(90, 0, 0);
		tileFloor.transform.position = new Vector3(gridMap.MapSize.x / 2 - 0.5f, 0, gridMap.MapSize.z / 2 - 0.5f);

		tileFloor.transform.parent = gridParent.transform;
	}

	public void BuildDefaultMap()
	{
		gridMap.SetMapSize(new int3(100, 1, 100));

		GridCell[,,] newMap = new GridCell[MapSize.x, MapSize.y, MapSize.z];
		for (int x = 0; x < MapSize.x; ++x)
		{
			for (int y = 0; y < MapSize.y; ++y)
			{
				for (int z = 0; z < MapSize.z; ++z)
				{
					newMap[x, y, z] = new GridCell(0);
				}
			}
		}

		gridMap.SetMap(newMap);
	}

	public void LoadByData(GameSaveLoader loadedData)
	{
		gridMap.LoadByData(loadedData.GetGrid());
		LoadByData(loadedData.GetPlaceable());
	}

	private void LoadByData(JsonData.PlaceableData data)
	{
		foreach (var obj in data.placeables)
		{
			int3 pos = new(obj.x, obj.y, obj.z);

			PlacementContext context = new(pos, obj.facingDirection, GameContext.Instance.PlaceableCatalog.FindById(obj.placeableID), PlacementEvent.Load);
			if (OnInstall(context) == false)
			{
				Debug.LogError("Cant be");
				return;
			}
		}
	}

	public bool OnCheckInstallable(in PlacementContext ctx, List<int3> possibleCell, List<int3> blocked)
	{
		if (ctx.placeableDefinition == null || ctx.placeableDefinition.gridFootprint == null)
		{
			Debug.LogWarning("No placeable or Footprint!!");
			return false;
		}

		bool installable = true;

		GridFootprint footprint = ctx.placeableDefinition.gridFootprint;
		Vector2Int pivot = footprint.Pivot;

		for (int z = 0; z < footprint.height; ++z)
		{
			for (int x = 0; x < footprint.width; ++x)
			{
				int3 offset = new(x - pivot.x, 0, z - pivot.y);
				int3 rotatedOffset = RotateOffset(offset, ctx.facingDirection);
				int3 target = ctx.center + rotatedOffset;

				if (gridMap.IsInBound(target) == false)
				{
					installable = false;
					continue;
				}

				if ((Map[target.x, target.y, target.z].Flags & GridFlags.BlockPlacement) == 0)
					possibleCell.Add(target);
				else
					blocked.Add(target);
			}

		}


		return installable || (blocked.Count > 0);
	}

	// gridPlaceable이 install이 되었을 때
	public bool OnInstall(in PlacementContext ctx)
	{
		if (ctx.placeableDefinition == null || ctx.placeableDefinition.gridFootprint == null)
		{
			Debug.LogWarning("No placeable or Footprint!!");
			return false;
		}

		if (ctx.placementEvent == PlacementEvent.Normal && Economy.CanAfford(ctx.placeableDefinition.Cost) == false)
		{
			Debug.Log("Can't afford that money!");
			return false;
		}


		GameObject obj = ctx.placedObj;

		if (obj == null)
			obj = Instantiate(ctx.placeableDefinition.prefab, placeableParent.transform);

		if (obj == null)
		{
			Debug.LogError("Failed to instantiate placeable prefab.");
			return false;
		}

		IInteractionPoint interactable = obj.GetComponent<IInteractionPoint>();


		GridFootprint footprint = ctx.placeableDefinition.gridFootprint;
		Vector2Int pivot = footprint.Pivot;

		for (int z = 0; z < footprint.height; ++z)
		{
			for (int x = 0; x < footprint.width; ++x)
			{
				int3 offset = new(x - pivot.x, 0, z - pivot.y);
				int3 rotatedOffset = RotateOffset(offset, ctx.facingDirection);
				int3 target = ctx.center + rotatedOffset;

				//Debug.Log($"target: {target} / offset: {offset}");

				if (gridMap.IsInBound(target) == false)
					return false;

				// set to cell
				var footprintCell = footprint.Get(x, z);

				Map[target.x, target.y, target.z].Set(footprintCell, obj);

				if (footprintCell.flags.HasFlag(GridFlags.Interaction))
				{
					// if interaction cell, add grid interaction to IGridPlaceable
					if (interactable == null)
					{
						Debug.LogError("The instantiated object does not have an IGridPlaceable component.");
						Destroy(obj);
						return false;
					}

					interactable.AddInteractionPoint(footprintCell.interactionKind, in target);
				}
			}
		}

		obj.transform.position += new Vector3(
			ctx.center.x,
			ctx.center.y,
			ctx.center.z
		);

		placedObjects[obj] = ctx;

		var gridPlaceable = obj.GetComponent<IGridPlaceable>();
		if( gridPlaceable != null)
		{
			gridPlaceable.OnPositionSet(ctx.center, ctx.facingDirection);
		}

		OnPlaceableInstalled.Invoke(ctx);

		//Debug.Log("PlacementSuccess");
		return true;
	}

	public bool OnRemove(GameObject targetObj)
	{
		if (placedObjects.TryGetValue(targetObj, out var context) == false)
		{
			Debug.Log("cant get");
			return false;
		}

		GridFootprint footprint = context.placeableDefinition.gridFootprint;
		Vector2Int pivot = footprint.Pivot;
		for (int z = 0; z < footprint.height; ++z)
		{
			for (int x = 0; x < footprint.width; ++x)
			{
				int3 offset = new(x - pivot.x, 0, z - pivot.y);
				int3 rotatedOffset = RotateOffset(offset, context.facingDirection);
				int3 target = context.center + rotatedOffset;
				if (gridMap.IsInBound(target) == false)
					return false;
				// clear to cell
				Map[target.x, target.y, target.z].Remove(footprint.Get(x, z), targetObj);
			}
		}

		placedObjects.Remove(targetObj);
		Destroy(targetObj);

		return true;
	}

	public bool TryMove(AIWorker worker, in int3 from, in int3 to)
	{
		var obj = gridMap.GetObjectOnGrid(from);
	
		if (obj != worker.gameObject)
			return false;

		if (IsBlocked(to))
			return false;

		PlacementContext context = placedObjects[worker.gameObject];
		GridFootprint footprint = context.placeableDefinition.gridFootprint;
		Vector2Int pivot = footprint.Pivot;
		
		for (int z = 0; z < footprint.height; ++z)
		{
			for (int x = 0; x < footprint.width; ++x)
			{
				int3 offset = new(x - pivot.x, 0, z - pivot.y);
				int3 rotatedOffset = RotateOffset(offset, context.facingDirection);
				int3 target = context.center + rotatedOffset;
				if (gridMap.IsInBound(target) == false)
					return false;
				// clear to cell
				Map[target.x, target.y, target.z].Remove(footprint.Get(x, z), worker.gameObject);
			}
		}

		context.center = to;
		for (int z = 0; z < footprint.height; ++z)
		{
			for (int x = 0; x < footprint.width; ++x)
			{
				int3 offset = new(x - pivot.x, 0, z - pivot.y);
				int3 rotatedOffset = RotateOffset(offset, context.facingDirection);
				int3 target = context.center + rotatedOffset;
				if (gridMap.IsInBound(target) == false)
					return false;
				Map[target.x, target.y, target.z].Set(footprint.Get(x, z), worker.gameObject);
			}
		}

		worker.SetPosition(to);
		return true;
	}

	// force moving
	private void OnForceMove(GameObject targetObj, PlacementContext ctx, in int3 newCenter)
	{
		// todo
		// 여기서 떨어진 애들은 파괴되어야함
		var footprint = ctx.placeableDefinition.gridFootprint;

		Vector2Int pivot = footprint.Pivot;

		for (int z = 0; z < footprint.height; ++z)
		{
			for (int x = 0; x < footprint.width; ++x)
			{
				int3 offset = new(x - pivot.x, 0, z - pivot.y);
				int3 target = newCenter + offset;

				// set to cell
				Map[target.x, target.y, target.z].Set(footprint.Get(x, z), targetObj);
			}
		}
		if (targetObj.TryGetComponent<IGridPlaceable>(out var gridPlaceable))
		{
			gridPlaceable.OnPositionSet(newCenter, ctx.facingDirection);
		}
		placedObjects[targetObj] = ctx;
	}

	public GameObject GetObjectOnGrid(in int3 pos)
	{
		return gridMap.GetObjectOnGrid(pos);
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


}

