public partial class PackingStationService
{
	public void ResetRuntimeState()
	{
		statesByBuildingId.Clear();
	}
}
