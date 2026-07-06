public interface IFacility : IGridPlaceable
{
	public uint FacilityRulePresetId { get; }

	public void SetFacilityRulePresetId(uint presetId);
}
