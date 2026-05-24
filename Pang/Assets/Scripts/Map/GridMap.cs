using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using Assets.Scripts.Save.JsonData;


public enum PlacementEvent
{
	Normal,
	Load,
	WorkerSpawn,
	RocketLanding,
	RocketCrashLanding,
}

public class PlacementContext
{
	public int3 center;
	public readonly FacingDirection facingDirection;
	public readonly PlaceableDefinition placeableDefinition;
	public readonly PlacementEvent placementEvent;
	public readonly GameObject placedObj = null;

	public PlacementContext(int3 center, FacingDirection dir, PlaceableDefinition def, PlacementEvent placementEvent = PlacementEvent.Normal, GameObject placedObj = null)
	{
		this.center = center;
		this.facingDirection = dir;
		this.placeableDefinition = def;
		this.placementEvent = placementEvent;
		this.placedObj = placedObj;
	}
}

public class GridCell
{
	private int tile = 0;
	private GridFlags flags = GridFlags.None;
	private int regionId = 0;
	private readonly Dictionary<GameObject, GridFlags> flagsByObject = new();

	private GameObject objectRef = null;
	private GameObject occupancyObjectRef = null;
	private GridOccupancyCategory occupancyCategory = GridOccupancyCategory.None;

	private FindRoute reservedBy = null;
	private readonly HashSet<FindRoute> plannedRoutes = new();

	public int Tile => tile;
	public GridFlags Flags => flags;
	public int RegionId => regionId;
	public GridOccupancyCategory OccupancyCategory => occupancyCategory;

	public bool IsPassable => Flags.HasFlag(GridFlags.BlockMovement | GridFlags.DynamicObstacle);
	public bool IsBlocked => Flags.HasFlag(GridFlags.BlockMovement);
	public bool IsIndoor => regionId == 2;
	public bool SealsSpace => (flags & GridFlags.SealsSpace) != 0;
	public bool CanPlaceObject => IsBlocked == false && reservedBy == null;
	public GameObject ObjectOnGrid => objectRef;
	public GameObject OccupancyObjectOnGrid => occupancyObjectRef;

	public FindRoute ReservedRoute => reservedBy;
	public int PlannedPathCount => plannedRoutes.Count;
	public IReadOnlyCollection<FindRoute> PlannedRoutes => plannedRoutes;

	public event System.Action<GridCell> OnGridUnReserved;

	public GridCell(int tileType)
	{
		tile = tileType;
	}

	public void Set(in FootprintCell cellFootprint, GameObject obj)
	{
		GridFlags flagsToSet = GetGridFlagsForPlacement(cellFootprint.flags);
		flags |= flagsToSet;
		occupancyCategory = cellFootprint.occupancyCategory;
		occupancyObjectRef = obj;
		if (obj != null)
		{
			flagsByObject.TryGetValue(obj, out GridFlags objectFlags);
			flagsByObject[obj] = objectFlags | flagsToSet;
		}

		if (cellFootprint.flags.HasFlag(GridFlags.Interaction) == false)
			objectRef = obj;
	}

	private static GridFlags GetGridFlagsForPlacement(GridFlags source)
	{
		if ((source & GridFlags.Interaction) == 0)
			return source;

		return source & ~(GridFlags.BlockMovement | GridFlags.DynamicObstacle | GridFlags.SealsSpace);
	}

	public void Clear()
	{
		flags = GridFlags.None;
		regionId = 0;
		flagsByObject.Clear();
		objectRef = null;
		occupancyObjectRef = null;
		occupancyCategory = GridOccupancyCategory.None;
	}

	public void Remove(in FootprintCell cellFootprint, GameObject obj)
	{
		if (objectRef == obj)
			objectRef = null;

		if (occupancyObjectRef == obj)
		{
			occupancyObjectRef = null;
			occupancyCategory = GridOccupancyCategory.None;
		}


		if (obj != null && flagsByObject.TryGetValue(obj, out GridFlags objectFlags))
		{
			objectFlags &= ~cellFootprint.flags;
			if (objectFlags == GridFlags.None)
				flagsByObject.Remove(obj);
			else
				flagsByObject[obj] = objectFlags;

			RebuildFlags();
			return;
		}

		flags &= ~cellFootprint.flags;
	}

	public void SetRegionId(int value)
	{
		regionId = value < 0 ? 0 : value;
	}

	private void RebuildFlags()
	{
		flags = GridFlags.None;
		foreach (GridFlags objectFlags in flagsByObject.Values)
			flags |= objectFlags;
	}

	public bool TryReserve(FindRoute routeWorker)
	{
		if (routeWorker != reservedBy && reservedBy != null)
			return false;

		reservedBy = routeWorker;
		return true;
	}

	public bool TryUnreserve(FindRoute routeWorker)
	{
		if (reservedBy != routeWorker)
		{
			// Debug.LogWarning($"[GridCell] Unreserve failed. Reserved by: {(reservedBy != null ? reservedBy.name : "null")}, but requested by: {routeWorker.name}");
			return false;
		}

		reservedBy = null;

		//Debug.Log($"[GridCell] Unreserved. Invoking events for listeners.");
		OnGridUnReserved?.Invoke(this);
		return true;
	}

	public bool RegisterPlannedRoute(FindRoute routeWorker)
	{
		if (routeWorker == null)
			return false;

		return plannedRoutes.Add(routeWorker);
	}

	public bool UnregisterPlannedRoute(FindRoute routeWorker)
	{
		if (routeWorker == null)
			return false;

		return plannedRoutes.Remove(routeWorker);
	}

}

public class GridMap
{
	// 임시로 serialize field
	[SerializeField] private int3 mapSize;
	private GridCell[,,] map;
	private List<IGridPlaceable> placeableObjects;

	public GridCell[,,] Map => map;
	public int3 MapSize => mapSize;

	//// UI상 가능/불가능 타일을 보여주기 위한 타일
	//private List<Cell> possibleTiles = new();
	//private List<Cell> blockedTiles = new();

	public void SetMapSize(int3 size) => mapSize = size;
	public void SetMap(GridCell[,,] map) => this.map = map;


	public void LoadByData(GridMapData data)
	{
		mapSize = new int3(data.X, data.Y, data.Z);
		map = new GridCell[mapSize.x, mapSize.y, mapSize.z];

		// set grid tiles
		for (int x = 0; x < mapSize.x; ++x)
		{
			for (int y = 0; y < mapSize.y; ++y)
			{
				for (int z = 0; z < mapSize.z; ++z)
				{
					int idx = x + mapSize.x * (y + mapSize.y * z);

					map[x, y, z] = new GridCell(data.Tiles[idx]);
				}
			}
		}
	}

	public bool IsInBound(in int3 pos)
	{
		return
			0 <= pos.x && pos.x < mapSize.x &&
			0 <= pos.y && pos.y < mapSize.y &&
			0 <= pos.z && pos.z < mapSize.z;
	}

	public GameObject GetObjectOnGrid(in int3 position)
	{
		if (IsInBound(position) == false)
			return null;

		GridCell cell = map[position.x, position.y, position.z];
		if (cell == null) return null;

		return cell.ObjectOnGrid;
	}

	public GridFlags GetGridFlags(in int3 pos)
	{
		if (IsInBound(pos) == false)
			return GridFlags.Error;

		GridCell cell = map[pos.x, pos.y, pos.z];
		if (cell == null) return GridFlags.Error;

		return cell.Flags;
	}
}
