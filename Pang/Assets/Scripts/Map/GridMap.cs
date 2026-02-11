using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

public class PlacementContext
{
	public readonly int3 center;
	public readonly FacingDirection facingDirection;
	public readonly PlaceableDefinition placeableDefinition;

	public PlacementContext(int3 center, FacingDirection dir, PlaceableDefinition def)
	{
		this.center = center;
		this.facingDirection = dir;
		this.placeableDefinition = def;
	}
}

public class GridCell
{
	private int tile = 0;
	private GridFlags flags = GridFlags.None;
	private InteractionKind kind = InteractionKind.None;

	//private IGridPlaceable objectRef = null;
	private GameObject objectRef = null;

	public GridFlags Flags => flags;
	public InteractionKind InteractionType => kind;

	public bool IsPassable => (Flags & GridFlags.BlockMovement) == 0;

	public GameObject ObjectOnGrid => objectRef;

	public GridCell(int tileType)
	{
		tile = tileType;
	}

	public void Set(in FootprintCell cellFootprint, GameObject obj)
	{
		flags = cellFootprint.flags;
		kind = cellFootprint.interactionKind;
		objectRef = obj;
	}

	public void Clear()
	{
		flags = GridFlags.None;
		kind = InteractionKind.None;
		objectRef = null;
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

	public bool IsInBound(int3 pos)
	{
		return
			0 <= pos.x && pos.x < mapSize.x &&
			0 <= pos.y && pos.y < mapSize.y &&
			0 <= pos.z && pos.z < mapSize.z;
	}

	public GameObject GetObjectOnGrid(int3 position)
	{
		if (IsInBound(position) == false)
			return null;

		GridCell cell = map[position.x, position.y, position.z];
		if (cell == null) return null;

		return cell.ObjectOnGrid;
	}
}
