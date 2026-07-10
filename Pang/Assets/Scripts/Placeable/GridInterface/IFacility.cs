public interface IFacility : IGridPlaceable
{
	public uint FacilityRulePresetId { get; }
	public int PowerConsumption { get; }

	public void SetFacilityRulePresetId(uint presetId);
}
