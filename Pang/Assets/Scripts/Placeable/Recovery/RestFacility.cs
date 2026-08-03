public sealed class RestFacility : RecoveryFacilityBase
{
	public override InteractionKind RecoveryInteractionKind => InteractionKind.Rest;
	public override WorkerStatusTarget BuildingTarget => WorkerStatusTarget.RestFacility;
	public override int PowerConsumption => 0;

	public override bool CanServe(AIWorker worker) => worker is HumanWorker;
}
