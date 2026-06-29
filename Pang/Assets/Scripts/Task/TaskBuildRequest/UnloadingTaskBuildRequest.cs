public sealed class UnloadingTaskBuildRequest : TaskBuildRequest<UnloadingTask>
{
	private readonly Rocket rocket;

	public UnloadingTaskBuildRequest(Rocket rocket, uint requestedBuildingID) : base(requestedBuildingID)
	{
		this.rocket = rocket;
	}

	public override WorkerTask.TaskType TaskType => WorkerTask.TaskType.Unloading;
	public override object RequestKey => GetRequestKey(rocket);
	public override bool IsStillValid => rocket != null && rocket.CanGetBox();

	public static object GetRequestKey(Rocket rocket)
	{
		return new TaskBuildRequestKey(WorkerTask.TaskType.Unloading, rocket);
	}

	protected override bool TryBuildTask(out UnloadingTask task)
	{
		task = null;
		if (IsStillValid == false || Ctx.IBWorkflowSvc == null)
			return false;

		CargoPort targetPort = Ctx.IBWorkflowSvc.ResolveUnloadingDestinationPort(rocket, RequestedBuildingID);
		if (targetPort == null)
			return false;

		task = new UnloadingTask(rocket, targetPort);
		return true;
	}
}
