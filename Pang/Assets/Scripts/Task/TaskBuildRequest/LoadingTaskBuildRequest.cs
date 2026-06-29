public sealed class LoadingTaskBuildRequest : TaskBuildRequest<LoadingTask>
{
	private readonly CargoPort sourcePort;

	public LoadingTaskBuildRequest(CargoPort sourcePort, uint requestedBuildingID) : base(requestedBuildingID)
	{
		this.sourcePort = sourcePort;
	}

	public override WorkerTask.TaskType TaskType => WorkerTask.TaskType.Loading;
	public override object RequestKey => GetRequestKey(sourcePort);
	public override bool IsStillValid => sourcePort is OutboundCargoPort && sourcePort.CanGetBox();

	public static object GetRequestKey(CargoPort sourcePort)
	{
		return new TaskBuildRequestKey(WorkerTask.TaskType.Loading, sourcePort);
	}

	protected override bool TryBuildTask(out LoadingTask task)
	{
		task = null;
		if (IsStillValid == false || Ctx.OBWorkflowSvc == null)
			return false;

		LaunchStation targetStation = Ctx.OBWorkflowSvc.ResolveLoadingTargetStation(sourcePort);
		if (targetStation == null)
			return false;

		task = new LoadingTask(sourcePort, targetStation);
		return true;
	}
}
