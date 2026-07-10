using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

[System.Serializable]
public sealed class BuildingFootprintRecord
{
	public uint RuntimeBuildingId;
	public string PresetId;
	public int Floor;
	public Vector2Int Center;
	public RectInt Bounds;
}

public sealed partial class BuildingFootprintService : MonoBehaviour
{
	[SerializeField] private List<BuildingFootprintPreset> footprintPresets = new();
	[SerializeField] private BuildingFootprintPreset footprintPreset;
	[SerializeField] private string wallPlaceableId = "wall_00";
	[SerializeField] private FacingDirection wallFacingDirection = FacingDirection.North;
	[SerializeField] private List<BuildingFootprintRecord> registeredFootprints = new();

	public IReadOnlyList<BuildingFootprintPreset> AvailablePresets => footprintPresets;
	public BuildingFootprintPreset ActivePreset => ResolveActivePreset();
	public IReadOnlyList<BuildingFootprintRecord> RegisteredFootprints => registeredFootprints;

	private BuildingManager BuildingManager => GameContext.Instance.BuildingMgr;
	private GridService GridService => GameContext.Instance.GridService;
	private PlaceableCatalog PlaceableCatalog => GameContext.Instance.PlaceableCatalog;

	public bool SetActivePreset(BuildingFootprintPreset preset)
	{
		if (preset == null || preset.IsValid == false || footprintPresets.Contains(preset) == false)
			return false;

		footprintPreset = preset;
		return true;
	}

	public bool TryGetPreset(string presetId, out BuildingFootprintPreset preset)
	{
		preset = null;
		if (string.IsNullOrWhiteSpace(presetId))
			return false;

		for (int i = 0; i < footprintPresets.Count; ++i)
		{
			BuildingFootprintPreset candidate = footprintPresets[i];
			if (candidate == null || candidate.IsValid == false || candidate.PresetId != presetId)
				continue;

			preset = candidate;
			return true;
		}

		return false;
	}

	public bool CanCreateFootprint(int floor, in int3 center, out string reason)
	{
		reason = string.Empty;

		BuildingFootprintPreset preset = ActivePreset;
		if (preset == null)
		{
			reason = "No valid building footprint preset is selected.";
			return false;
		}

		PlaceableDefinition wallDefinition = ResolveWallDefinition();
		if (wallDefinition == null)
		{
			reason = $"Missing wall placeable definition: {wallPlaceableId}";
			return false;
		}

		List<int3> wallPositions = new();
		for (int z = 0; z < preset.Height; ++z)
		{
			for (int x = 0; x < preset.Width; ++x)
			{
				BuildingFootprintCell footprintCell = preset.Get(x, z);
				if (footprintCell.IsOwned == false)
					continue;

				int3 cellPos = ToWorldPosition(center, preset, x, z, floor);
				GridCell cell = GridService.GetCell(cellPos);
				if (cell == null)
				{
					reason = "Building footprint is out of bounds.";
					return false;
				}

				if (cell.BuildingId != 0 || cell.OccupancyObjectOnGrid != null || cell.CanPlaceObject == false)
				{
					reason = "Building footprint contains an occupied cell.";
					return false;
				}

				if (footprintCell.IsWall)
					wallPositions.Add(cellPos);
			}
		}

		List<int3> possible = new();
		List<int3> blocked = new();
		for (int i = 0; i < wallPositions.Count; ++i)
		{
			possible.Clear();
			blocked.Clear();

			PlacementContext context = new(wallPositions[i], wallFacingDirection, wallDefinition);
			if (GridService.OnCheckInstallable(context, possible, blocked) == false || blocked.Count > 0)
			{
				reason = "Walls cannot be placed on the building footprint boundary.";
				return false;
			}
		}

		return true;
	}

	public bool TryCreateFootprint(int floor, in int3 center, out string reason)
	{
		return TryCreateFootprint(floor, center, BuildingType.Generic, out reason);
	}

	public bool TryCreateFootprint(int floor, in int3 center, BuildingType buildingType, out string reason)
	{
		BuildingFootprintPreset preset = ActivePreset;
		if (CanCreateFootprint(floor, center, out reason) == false)
			return false;

		PlaceableDefinition wallDefinition = ResolveWallDefinition();
		if (wallDefinition == null)
		{
			reason = $"Missing wall placeable definition: {wallPlaceableId}";
			return false;
		}

		List<GameObject> createdWalls = new();
		for (int z = 0; z < preset.Height; ++z)
		{
			for (int x = 0; x < preset.Width; ++x)
			{
				BuildingFootprintCell footprintCell = preset.Get(x, z);
				if (footprintCell.IsWall == false)
					continue;

				int3 cellPos = ToWorldPosition(center, preset, x, z, floor);
				GameObject wallObject = Instantiate(wallDefinition.prefab);
				PlacementContext context = new(cellPos, wallFacingDirection, wallDefinition, PlacementEvent.Normal, wallObject);
				if (GridService.OnInstall(context) == false)
				{
					Destroy(wallObject);
					RollbackCreatedWalls(createdWalls);
					reason = "Failed to place one or more wall tiles for the building boundary.";
					return false;
				}

				createdWalls.Add(wallObject);
			}
		}

		List<GridCell> ownedCells = BuildOwnedCells(center, preset, floor);
		Building createdBuilding = BuildingManager.CreateBuilding(ownedCells, buildingType);
		if (createdBuilding == null)
		{
			RollbackCreatedWalls(createdWalls);
			reason = "Failed to create runtime building data for the selected footprint.";
			return false;
		}

		Vector2Int center2D = new(center.x, center.z);
		registeredFootprints.Add(new BuildingFootprintRecord
		{
			RuntimeBuildingId = createdBuilding.RuntimeBuildingId,
			PresetId = preset.PresetId,
			Floor = floor,
			Center = center2D,
			Bounds = preset.GetBounds(center2D),
		});

		reason = string.Empty;
		return true;
	}

	public bool TryGetFootprint(uint runtimeBuildingId, out BuildingFootprintRecord record)
	{
		record = null;
		if (runtimeBuildingId == 0)
			return false;

		for (int i = 0; i < registeredFootprints.Count; ++i)
		{
			BuildingFootprintRecord footprint = registeredFootprints[i];
			if (footprint == null || footprint.RuntimeBuildingId != runtimeBuildingId)
				continue;

			record = footprint;
			return true;
		}

		return false;
	}

	public bool TryGetInteriorBounds(uint runtimeBuildingId, out RectInt interiorBounds, out int floor)
	{
		interiorBounds = default;
		floor = 0;

		if (TryGetFootprint(runtimeBuildingId, out BuildingFootprintRecord footprint) == false || footprint == null)
			return false;

		floor = footprint.Floor;
		RectInt bounds = footprint.Bounds;
		if (bounds.width < 3 || bounds.height < 3)
			return false;

		interiorBounds = new RectInt(bounds.xMin + 1, bounds.yMin + 1, bounds.width - 2, bounds.height - 2);
		return interiorBounds.width > 0 && interiorBounds.height > 0;
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

	private BuildingFootprintPreset ResolveActivePreset()
	{
		if (footprintPreset != null && footprintPreset.IsValid && footprintPresets.Contains(footprintPreset))
			return footprintPreset;

		for (int i = 0; i < footprintPresets.Count; ++i)
		{
			BuildingFootprintPreset candidate = footprintPresets[i];
			if (candidate != null && candidate.IsValid)
				return candidate;
		}

		return null;
	}

	private static int3 ToWorldPosition(in int3 center, BuildingFootprintPreset preset, int x, int z, int floor)
	{
		return new int3(
			center.x + x - preset.Pivot.x,
			floor,
			center.z + z - preset.Pivot.y);
	}

	private List<GridCell> BuildOwnedCells(in int3 center, BuildingFootprintPreset preset, int floor)
	{
		List<GridCell> result = new();
		for (int z = 0; z < preset.Height; ++z)
		{
			for (int x = 0; x < preset.Width; ++x)
			{
				if (preset.Get(x, z).IsOwned == false)
					continue;

				GridCell cell = GridService.GetCell(ToWorldPosition(center, preset, x, z, floor));
				if (cell != null)
					result.Add(cell);
			}
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
