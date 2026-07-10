public static class FacilityPowerExtensions
{
	public static float GetPowerEfficiency(this IFacility facility)
	{
		return GameContext.HasInstance
			? GameContext.Instance.PowerSvc.GetPowerEfficiency(facility)
			: 0f;
	}
}
