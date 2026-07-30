using UnityEngine;

public static class FacilityEfficiency
{
	public static float GetHealthEfficiency(IHealth health)
	{
		return health == null || health.MaxHealth <= 0.0f
			? 0.0f
			: Mathf.Clamp01(health.Health / health.MaxHealth);
	}

	public static float GetOperatingEfficiency(IWearableFacility facility)
	{
		if (facility == null || GameContext.HasInstance == false)
			return 0.0f;

		float powerEfficiency = GameContext.Instance.PowerSvc.GetPowerEfficiency(facility);
		return Mathf.Clamp01(powerEfficiency) *
			GetHealthEfficiency(facility) *
			Mathf.Clamp01(facility.WearEfficiency);
	}

	public static float GetOperatingEfficiency(Building building, IHealth health, IWearable wearable)
	{
		if (building == null || health == null || wearable == null)
			return 0.0f;

		return Mathf.Clamp01(building.PowerEfficiency) *
			GetHealthEfficiency(health) *
			Mathf.Clamp01(wearable.WearEfficiency);
	}
}
