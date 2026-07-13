using System;
using System.Collections.Generic;

public readonly struct CompanyBuildingStateSnapshot
{
	public uint BuildingId { get; }
	public BuildingType BuildingType { get; }
	public BuildingState State { get; }
	public float AverageTemperatureCelsius { get; }
	public float PowerSupplyRatio { get; }

	public CompanyBuildingStateSnapshot(
		uint buildingId,
		BuildingType buildingType,
		BuildingState state,
		float averageTemperatureCelsius,
		float powerSupplyRatio)
	{
		BuildingId = buildingId;
		BuildingType = buildingType;
		State = state;
		AverageTemperatureCelsius = averageTemperatureCelsius;
		PowerSupplyRatio = powerSupplyRatio;
	}
}

public sealed class CompanyStateSnapshot
{
	private static readonly CompanyStateSnapshot empty = new(Array.Empty<CompanyBuildingStateSnapshot>());

	private readonly IReadOnlyList<CompanyBuildingStateSnapshot> buildings;

	public static CompanyStateSnapshot Empty => empty;
	public IReadOnlyList<CompanyBuildingStateSnapshot> Buildings => buildings;

	public CompanyStateSnapshot(IEnumerable<CompanyBuildingStateSnapshot> buildings)
	{
		List<CompanyBuildingStateSnapshot> copy = buildings != null
			? new List<CompanyBuildingStateSnapshot>(buildings)
			: new List<CompanyBuildingStateSnapshot>();
		copy.Sort(CompareByBuildingId);
		this.buildings = copy.AsReadOnly();
	}

	public static CompanyStateSnapshot Capture()
	{
		if (GameContext.HasInstance == false)
			return Empty;

		BuildingManager buildingManager = GameContext.Instance.BuildingMgr;
		if (buildingManager == null)
			return Empty;

		IReadOnlyList<Building> registeredBuildings = buildingManager.RegisteredBuildings;
		List<CompanyBuildingStateSnapshot> capturedBuildings = new(registeredBuildings.Count);
		for (int i = 0; i < registeredBuildings.Count; ++i)
		{
			Building building = registeredBuildings[i];
			if (building == null)
				continue;

			capturedBuildings.Add(new CompanyBuildingStateSnapshot(
				building.RuntimeBuildingId,
				building.Type,
				building.State,
				building.AverageTemperatureCelsius,
				building.PowerEfficiency));
		}

		return capturedBuildings.Count > 0
			? new CompanyStateSnapshot(capturedBuildings)
			: Empty;
	}

	private static int CompareByBuildingId(CompanyBuildingStateSnapshot left, CompanyBuildingStateSnapshot right)
	{
		return left.BuildingId.CompareTo(right.BuildingId);
	}
}
