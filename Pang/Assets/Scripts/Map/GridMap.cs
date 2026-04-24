using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;


public enum PlacementEvent
{
	Normal,
	Load,
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

	private GameObject objectRef = null;

	private FindRoute reservedBy = null;

	public GridFlags Flags => flags;

	public bool IsPassable => Flags.HasFlag(GridFlags.BlockMovement | GridFlags.DynamicObstacle);
	public bool IsBlocked => Flags.HasFlag(GridFlags.BlockMovement);
	public bool CanPlaceObject => IsBlocked == false && reservedBy == null;
	public GameObject ObjectOnGrid => objectRef;

	public FindRoute ReservedRoute => reservedBy;

	public event System.Action<GridCell> OnGridUnReserved;

	public GridCell(int tileType)
	{
		tile = tileType;
	}

	public void Set(in FootprintCell cellFootprint, GameObject obj)
	{
		flags |= cellFootprint.flags;

		if (cellFootprint.flags.HasFlag(GridFlags.Interaction) == false)
			objectRef = obj;
	}

	public void Clear()
	{
		flags = GridFlags.None;
		objectRef = null;
	}

	public void Remove(in FootprintCell cellFootprint, GameObject obj)
	{
		if (objectRef == obj)
			objectRef = null;
		flags &= ~cellFootprint.flags;
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
			return false;

		reservedBy = null;

		OnGridUnReserved?.Invoke(this);
		return true;
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


	public void LoadByData(JsonData.GridMapData data)
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
