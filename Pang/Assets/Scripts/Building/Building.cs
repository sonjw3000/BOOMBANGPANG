using System.Collections.Generic;

public enum BuildingType
{
	Generic,
	Staging,
	Storage,
	Packing,
	Launch,
}

public static class BuildingTypeUtility
{
	public static string ToDisplayString(BuildingType type)
	{
		return type switch
		{
			BuildingType.Generic => "Generic",
			BuildingType.Staging => "Staging",
			BuildingType.Storage => "Storage",
			BuildingType.Packing => "Packing",
			BuildingType.Launch => "Launch",
			_ => type.ToString(),
		};
	}
}

public enum BuildingState
{
	Active,
	PendingDemolition,
	Destroyed,
}

public enum BuildingWorkScope
{
	HomeOnly,
	HomeAndOutdoor,
	CrossBuilding,
	Global,
}

public static class BuildingWorkScopeUtility
{
	public static string ToDisplayString(BuildingWorkScope scope)
	{
		return scope switch
		{
			BuildingWorkScope.HomeOnly => "Home Only",
			BuildingWorkScope.HomeAndOutdoor => "Home + Outdoor",
			BuildingWorkScope.CrossBuilding => "Cross Building",
			BuildingWorkScope.Global => "Global",
			_ => scope.ToString(),
		};
	}
}

public sealed class Building
{
	private string displayName = string.Empty;
	private BuildingType buildingType = BuildingType.Generic;
	private uint runtimeBuildingId;
	private BuildingState state = BuildingState.Active;
	private BuildingWorkScope workScope = BuildingWorkScope.HomeOnly;
	private bool isRegistered;

	private readonly List<GridCell> occupiedCells;

	private readonly List<IFacility> occupiedFacilities = new();
	private readonly List<CargoPort> occupiedCargoPorts = new();
	// todo
	// airlock 추가시에 적용
	// private List<Airlock> airlocks = new List<Airlock>();
	public string DisplayName => displayName;
	public BuildingType Type => buildingType;
	public uint RuntimeBuildingId => runtimeBuildingId;
	public BuildingState State => state;
	public BuildingWorkScope WorkScope => workScope;
	public IReadOnlyList<GridCell> OccupiedCells => occupiedCells;
	public IReadOnlyList<IFacility> OccupiedFacilities => occupiedFacilities;
	public IReadOnlyList<CargoPort> OccupiedCargoPorts => occupiedCargoPorts;

	public Building(string displayName, List<GridCell> occupiedCells, BuildingType buildingType = BuildingType.Generic)
	{
		this.displayName = displayName;
		this.buildingType = buildingType;
		this.occupiedCells = occupiedCells ?? new List<GridCell>();
	}

	internal void AssignRuntimeBuildingId(uint id)
	{
		runtimeBuildingId = id;
	}

	internal void SetRegistered(bool registered)
	{
		isRegistered = registered;
	}

	public void Rename(string newDisplayName)
	{
		displayName = newDisplayName;
	}

	public void SetState(BuildingState newState)
	{
		state = newState;
	}

	public void SetWorkScope(BuildingWorkScope newWorkScope)
	{
		workScope = newWorkScope;
	}

	internal bool RegisterFacility(IFacility facility)
	{
		if (facility == null || occupiedFacilities.Contains(facility))
			return false;

		occupiedFacilities.Add(facility);
		if (facility is CargoPort cargoPort && occupiedCargoPorts.Contains(cargoPort) == false)
			occupiedCargoPorts.Add(cargoPort);

		return true;
	}

	internal bool UnregisterFacility(IFacility facility)
	{
		if (facility == null)
			return false;

		bool removed = occupiedFacilities.Remove(facility);
		if (facility is CargoPort cargoPort)
			occupiedCargoPorts.Remove(cargoPort);

		return removed;
	}
}
