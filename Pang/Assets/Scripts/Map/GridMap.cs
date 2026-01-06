using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.SocialPlatforms.GameCenter;

public struct PlacementContext
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

	private GameObject objectRef = null;

	public GridFlags Flags => flags;
	public InteractionKind InteractionType => kind;

	public bool IsPassable => (Flags & GridFlags.BlockMovement) != 0;

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

}

public class GridMap : MonoBehaviour
{
	//[SerializeField] private GameObject placeableParent;
	//[SerializeField] private GameObject gridParent;
	//[SerializeField] private GameObject nonPlaceableParent;
	

	// 임시로 serialize field
	[SerializeField] private int3 mapSize;
	private GridCell[,,] map;
	private List<IGridPlaceable> placeableObjects;

	public GridCell[,,] Map => map;
	public int3 MapSize => mapSize;

	//// UI상 가능/불가능 타일을 보여주기 위한 타일
	//private List<Cell> possibleTiles = new();
	//private List<Cell> blockedTiles = new();

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
					int idx = 
						x * mapSize.x + 
						y * mapSize.y + 
						z * mapSize.z;

					map[x, y, z] = new GridCell(data.Tiles[idx]);
				}
			}
		}


	}

	public void LoadByData(JsonData.PlaceableData data)
	{
		foreach (var obj in data.placeables)
		{
			int3 pos = new int3(obj.x, obj.y, obj.z);

			PlacementContext context = new PlacementContext(pos, obj.facingDirection, GameContext.Instance.PlaceableCatalog.FindById(obj.placeableID));
			if (OnInstall(context) == false)
			{
				Debug.LogError("Cant be");
				return;
			}
		}
	}

	// return true when can install
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
				int3 offset = new int3(x - pivot.x, 0, z - pivot.y);
				int3 rotatedOffset = RotateOffset(offset, ctx.facingDirection);
				int3 target = ctx.center + rotatedOffset;

				if (IsInBound(target) == false)
				{
					installable = false;
					continue;
				}

				if ((map[target.x, target.y, target.z].Flags & GridFlags.BlockPlacement) != 0)
					possibleCell.Add(target);
				else
					blocked.Add(target);
			}

		}


		return installable | (blocked.Count > 0);
	}


	// gridPlaceable이 install이 되었을 때
	public bool OnInstall(in PlacementContext ctx)
	{
		if (ctx.placeableDefinition == null || ctx.placeableDefinition.gridFootprint == null)
		{
			Debug.LogWarning("No placeable or Footprint!!");
			return false;
		}

		GridFootprint footprint = ctx.placeableDefinition.gridFootprint;
		Vector2Int pivot = footprint.Pivot;

		for(int z = 0; z < footprint.height; ++z)
		{
			for(int x = 0; x < footprint.width; ++x)
			{
				int3 offset = new int3(x - pivot.x, 0, z - pivot.y);
				int3 rotatedOffset = RotateOffset(offset, ctx.facingDirection);
				int3 target = ctx.center + rotatedOffset;

				if (IsInBound(target) == false) 
					return false;

				// set to cell
				map[target.x, target.y, target.z].Set(footprint.Get(offset.x, offset.z), ctx.placeableDefinition.prefab);
			}
		}

		return true;
	}

	private bool IsInBound(int3 pos)
	{
		return 
			pos.x >= 0 && pos.y >= 0 && pos.z >= 0 &&
			pos.x < mapSize.x && pos.y < mapSize.y && pos.z < mapSize.z;
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
