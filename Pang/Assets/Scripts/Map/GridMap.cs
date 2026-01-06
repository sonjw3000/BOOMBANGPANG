using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

public struct PlacementContext
{
	public readonly int3 center;
	public readonly FacingDirection facingDirection;
	public readonly PlaceableDefinition placeableDefinition;
}

public class GridCell
{
	private GridFlags flags;
	private InteractionKind kind;

	private GameObject objectRef;

	public GridFlags Flags => flags;
	public InteractionKind InteractionType => kind;

	public void Set(in FootprintCell cellFootprint, GameObject obj)
	{
		flags = cellFootprint.flags;
		kind = cellFootprint.interactionKind;
		objectRef = obj;
	}
}

public class GridMap : MonoBehaviour
{
	private GridCell[,,] map;
	private int3 mapSize;

	private List<IGridPlaceable> placeableObjects;

	[Header("Placeable Objects")]
	[SerializeField] private PlaceableCatalog catalog;

	// 아직은 쓰지 마라!
	//[Header("Tiles")]
	//[SerializeField] private PlaceableCatalog baseTiles;


	[SerializeField] private string mapJsonFile;

	//// UI상 가능/불가능 타일을 보여주기 위한 타일
	//private List<Cell> possibleTiles = new();
	//private List<Cell> blockedTiles = new();
	

	private void Awake()
	{
		// mapJsonFile을 통해 json을 불러와 map을 초기화한다

	}


	// return true when installable
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
