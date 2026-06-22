public sealed partial class AirlockService
{
	public void ResetRuntimeState()
	{
		foreach (uint buildingId in FacilityManager.GetBuildingIds())
		{
			if (TryGetBuildingFacilities(buildingId, out var airlocks) == false)
				continue;

			for (int i = 0; i < airlocks.Count; ++i)
				airlocks[i]?.Release(null);
		}
	}
}
