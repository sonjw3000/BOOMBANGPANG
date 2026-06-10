using UnityEngine;
using System.Collections.Generic;

public enum BuildingType
{
	Generic,
	Storage,
	Packing,
	Launch,
}

[DisallowMultipleComponent]
public sealed class Building
{
	private string displayName = string.Empty;
	private BuildingType buildingType = BuildingType.Generic;
	private uint runtimeBuildingId;

	private List<GridCell> occupiedCells = new();
	private List<ZoneArea> occupiedZones = new();

	private List<IGridPlaceable> occupiedPlaceables = new();
	private List<CargoPort> occupiedCargoPorts = new();
	// todo
	// airlock 추가시에 적용
	// private List<Airlock> airlocks = new List<Airlock>();
	public string DisplayName => displayName;
	public BuildingType Type => buildingType;
	public uint RuntimeBuildingId => runtimeBuildingId;
	public IEnumerable<GridCell> OccupiedCells => occupiedCells;
	public IEnumerable<ZoneArea> OccupiedZones => occupiedZones;


	private BuildingManager BuildingMgr => GameContext.Instance.BuildingMgr;
	private GridService GridService => GameContext.Instance.GridService;

	public Building(string displayName, RectInt bounds, int floor, BuildingType buildingType = BuildingType.Generic)
	{
		this.displayName = displayName;
		this.buildingType = buildingType;

		if (GameContext.HasInstance)
			BuildingMgr.Register(this);

		for (int x = bounds.xMin; x < bounds.xMax; ++x)
		{
			for (int y = bounds.yMin; y < bounds.yMax; ++y)
			{
				GridCell cell = GridService.GetCell(x, floor, y);
				if (cell == null)
					continue;

				occupiedCells.Add(cell);
				// cell.SetBuilding(this);
			}
		}
	}

	~Building()
	{
		if (GameContext.HasInstance)
			BuildingMgr.Unregister(this);
	}

	internal void AssignRuntimeBuildingId(uint id)
	{
		runtimeBuildingId = id;
	}
}
