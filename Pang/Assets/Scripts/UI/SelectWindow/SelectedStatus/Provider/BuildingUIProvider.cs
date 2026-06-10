using UnityEngine;

public sealed class BuildingUIProvider : UIProvider<BuildingSelectionProxy>
{
	private Building Building => currentTarget != null ? currentTarget.Building : null;

	public override string Name => Building != null ? Building.DisplayName : "Unknown Building";
	public override string Subtitle => Building != null ? Building.Type.ToString() : "Unknown Building";
	public override Sprite Icon => null;

	public string StateDisplay => Building != null ? Building.State.ToString() : "Unknown";
	public int CellCount => Building != null ? Building.OccupiedCells.Count : 0;
	public int FacilityCount => Building != null ? Building.OccupiedFacilities.Count : 0;
	public int CargoPortCount => Building != null ? Building.OccupiedCargoPorts.Count : 0;
	public int ZoneCount => Building != null ? Building.OccupiedZones.Count : 0;

	public override void BuildInfoBlocks()
	{
		infoBlocks.Clear();
		infoBlocks.Add(new KeyValueBlock("State", StateDisplay));
		infoBlocks.Add(new KeyValueBlock("Facilities", FacilityCount.ToString()));
		infoBlocks.Add(new KeyValueBlock("Zones", ZoneCount.ToString()));
	}

	public override void OnUpdate()
	{
		if (infoBlocks.Count < 3)
			return;

		(infoBlocks[0] as KeyValueBlock)?.UpdateValue(StateDisplay);
		(infoBlocks[1] as KeyValueBlock)?.UpdateValue(FacilityCount.ToString());
		(infoBlocks[2] as KeyValueBlock)?.UpdateValue(ZoneCount.ToString());
	}
}
