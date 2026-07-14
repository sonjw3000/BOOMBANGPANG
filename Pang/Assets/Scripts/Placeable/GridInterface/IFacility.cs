public interface IFacility : IGridPlaceable, IHealth
{
	public uint FacilityRulePresetId { get; }
	public int PowerConsumption { get; }

	public void SetFacilityRulePresetId(uint presetId);
}
