public partial class FacilityManager
{
	public void ResetRuntimeState()
	{
		buildingFacilities.Clear();
		facilityBuildingIds.Clear();
		invalidatingFacilities.Clear();
		destroyedFacilities.Clear();
	}
}
