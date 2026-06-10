using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

[System.Serializable]
public sealed class BuildingFootprintRecord
{
	public uint RuntimeBuildingId;
	public int Floor;
	public RectInt Bounds;
}

public sealed class BuildingFootprintService : MonoBehaviour
{
	[SerializeField] private string wallPlaceableId = "wall_00";
	[SerializeField] private FacingDirection wallFacingDirection = FacingDirection.North;
	[SerializeField] private List<BuildingFootprintRecord> registeredFootprints = new();

	public IReadOnlyList<BuildingFootprintRecord> RegisteredFootprints => registeredFootprints;

	private BuildingManager BuildingManager => GameContext.Instance.BuildingMgr;
	private GridService GridService => GameContext.Instance.GridService;
	private PlaceableCatalog PlaceableCatalog => GameContext.Instance.PlaceableCatalog;

	public bool CanCreateFootprint(int floor, in RectInt bounds, out string reason)
	{
		reason = string.Empty;

		if (bounds.width <= 0 || bounds.height <= 0)
		{
			reason = "Building area must have positive size.";
			return false;
		}

		if (bounds.width < 3 || bounds.height < 3)
		{
			reason = "Building area must include at least one interior cell.";
			return false;
		}

		PlaceableDefinition wallDefinition = ResolveWallDefinition();
		if (wallDefinition == null)
		{
			reason = $"Missing wall placeable definition: {wallPlaceableId}";
			return false;
		}

		foreach (var footprint in registeredFootprints)
		{
			if (footprint == null || footprint.Floor != floor)
				continue;

			if (footprint.Bounds.Overlaps(bounds))
			{
				reason = "Building area overlaps another building footprint.";
				return false;
			}
		}

		for (int z = bounds.yMin; z < bounds.yMax; ++z)
		{
			for (int x = bounds.xMin; x < bounds.xMax; ++x)
			{
				int3 cellPos = new(x, floor, z);
				GridCell cell = GridService.GetCell(cellPos);
				if (cell == null)
				{
					reason = "Building area is out of bounds.";
					return false;
				}

				if (cell.OccupancyObjectOnGrid != null)
				{
					reason = "Building area is already occupied.";
					return false;
				}
			}
		}

		List<int3> possible = new();
		List<int3> blocked = new();
		foreach (var cellPos in BuildPerimeterCells(bounds, floor))
		{
			possible.Clear();
			blocked.Clear();

			PlacementContext context = new(cellPos, wallFacingDirection, wallDefinition);
			if (GridService.OnCheckInstallable(context, possible, blocked) == false || blocked.Count > 0)
			{
				reason = "Walls cannot be placed on the selected building border.";
				return false;
			}
		}

		return true;
	}

	public bool TryCreateFootprint(int floor, in RectInt bounds, out string reason)
	{
		if (CanCreateFootprint(floor, bounds, out reason) == false)
			return false;

		PlaceableDefinition wallDefinition = ResolveWallDefinition();
		if (wallDefinition == null)
		{
			reason = $"Missing wall placeable definition: {wallPlaceableId}";
			return false;
		}

		List<GameObject> createdWalls = new();
		foreach (var cellPos in BuildPerimeterCells(bounds, floor))
		{
			GameObject wallObject = Instantiate(wallDefinition.prefab);
			PlacementContext context = new(cellPos, wallFacingDirection, wallDefinition, PlacementEvent.Normal, wallObject);
			if (GridService.OnInstall(context) == false)
			{
				Destroy(wallObject);
				RollbackCreatedWalls(createdWalls);
				reason = "Failed to place one or more wall tiles for the building border.";
				return false;
			}

			createdWalls.Add(wallObject);
		}

		List<GridCell> ownedCells = BuildOwnedCells(bounds, floor);
		Building createdBuilding = BuildingManager.CreateBuilding(ownedCells);
		if (createdBuilding == null)
		{
			RollbackCreatedWalls(createdWalls);
			reason = "Failed to create runtime building data for the selected footprint.";
			return false;
		}

		registeredFootprints.Add(new BuildingFootprintRecord
		{
			RuntimeBuildingId = createdBuilding.RuntimeBuildingId,
			Floor = floor,
			Bounds = bounds,
		});

		reason = string.Empty;
		return true;
	}

	public void ResetRuntimeState()
	{
		registeredFootprints.Clear();
	}

	private PlaceableDefinition ResolveWallDefinition()
	{
		if (PlaceableCatalog == null)
			return null;

		PlaceableDefinition definition = PlaceableCatalog.FindById(wallPlaceableId);
		if (definition == null || definition.prefab == null || definition.gridFootprint == null)
			return null;

		return definition;
	}

	private static IEnumerable<int3> BuildPerimeterCells(in RectInt bounds, int floor)
	{
		HashSet<Vector2Int> emitted = new();
		List<int3> result = new();

		void AddCell(int x, int z)
		{
			Vector2Int key = new(x, z);
			if (emitted.Add(key))
				result.Add(new int3(x, floor, z));
		}

		for (int x = bounds.xMin; x < bounds.xMax; ++x)
		{
			AddCell(x, bounds.yMin);
			AddCell(x, bounds.yMax - 1);
		}

		for (int z = bounds.yMin; z < bounds.yMax; ++z)
		{
			AddCell(bounds.xMin, z);
			AddCell(bounds.xMax - 1, z);
		}

		return result;
	}

	private List<GridCell> BuildOwnedCells(in RectInt bounds, int floor)
	{
		List<GridCell> result = new(bounds.width * bounds.height);

		for (int z = bounds.yMin; z < bounds.yMax; ++z)
		{
			for (int x = bounds.xMin; x < bounds.xMax; ++x)
			{
				GridCell cell = GridService.GetCell(x, floor, z);
				if (cell != null)
					result.Add(cell);
			}
		}

		return result;
	}

	private void RollbackCreatedWalls(List<GameObject> createdWalls)
	{
		for (int i = createdWalls.Count - 1; i >= 0; --i)
		{
			GameObject wallObject = createdWalls[i];
			if (wallObject == null)
				continue;

			GridService.OnRemove(wallObject);
		}
	}
}
