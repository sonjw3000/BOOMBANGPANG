using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

public class GridService : MonoBehaviour
{
	[SerializeField] private GameObject placeableParent;
	[SerializeField] private GameObject gridParent;

	private GridMap gridMap = new();

	public GridCell[,,] Map => gridMap.Map;
	public int3 MapSize => gridMap.MapSize;



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
			int3 pos = new int3(obj.x, obj.y, obj.z);

			PlacementContext context = new PlacementContext(pos, obj.facingDirection, GameContext.Instance.PlaceableCatalog.FindById(obj.placeableID));
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
				int3 offset = new int3(x - pivot.x, 0, z - pivot.y);
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

		GameObject obj = Instantiate(ctx.placeableDefinition.prefab, placeableParent.transform);

		if (obj == null)
		{
			Debug.LogError("Failed to instantiate placeable prefab.");
			return false;
		}

		GridFootprint footprint = ctx.placeableDefinition.gridFootprint;
		Vector2Int pivot = footprint.Pivot;

		for (int z = 0; z < footprint.height; ++z)
		{
			for (int x = 0; x < footprint.width; ++x)
			{
				int3 offset = new int3(x - pivot.x, 0, z - pivot.y);
				int3 rotatedOffset = RotateOffset(offset, ctx.facingDirection);
				int3 target = ctx.center + rotatedOffset;

				//Debug.Log($"target: {target} / offset: {offset}");

				if (gridMap.IsInBound(target) == false)
					return false;

				// set to cell
				Map[target.x, target.y, target.z].Set(footprint.Get(x, z), obj);
			}
		}

		obj.transform.position += new Vector3(
			ctx.center.x,
			ctx.center.y,
			ctx.center.z
		);


		return true;
	}

	public bool OnRemove(int3 gridPosition)
	{

		return true;
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

