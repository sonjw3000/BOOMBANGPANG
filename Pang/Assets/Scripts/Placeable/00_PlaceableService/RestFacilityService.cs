public sealed class RestFacilityService : RecoveryFacilityService<RestFacility>
{
	protected override bool AllowUnassignedWorkerGlobalSearch => true;
}
