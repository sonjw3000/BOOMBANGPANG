public sealed class OBTaskBuildRequest : TaskBuildRequest<OBTask>
{
	private readonly CapsuleBuffer sourceBuffer;

	public OBTaskBuildRequest(CapsuleBuffer sourceBuffer, uint requestedBuildingID) : base(requestedBuildingID)
	{
		this.sourceBuffer = sourceBuffer;
	}

	public override WorkerTask.TaskType TaskType => WorkerTask.TaskType.OB;
	public override object RequestKey => GetRequestKey(sourceBuffer);
	public override bool IsStillValid => sourceBuffer != null && sourceBuffer.CanDispatchToOutbound();

	public static object GetRequestKey(CapsuleBuffer sourceBuffer)
	{
		return new TaskBuildRequestKey(WorkerTask.TaskType.OB, sourceBuffer);
	}

	protected override bool TryBuildTask(out OBTask task)
	{
		task = null;
		if (IsStillValid == false || BuildingManager == null || BuildingManager.TryGetBuilding(RequestedBuildingID, out Building building) == false)
			return false;

		if (building.CanBuildOutboundTaskRequest(sourceBuffer) == false)
			return false;

		ZoneFilter zoneFilter = ZoneFilter.ForContainer(sourceBuffer.DockedCapsule);
		OutboundCargoPort targetPort = building.ResolveOutboundPortTarget(sourceBuffer.GridPosition, zoneFilter);
		if (targetPort == null)
			return false;

		task = new OBTask(sourceBuffer, RequestedBuildingID, targetPort);
		return true;
	}

	public override void OnTaskQueued(WorkerTask task)
	{
		if (task is OBTask obTask && BuildingManager != null && BuildingManager.TryGetBuilding(RequestedBuildingID, out Building building))
			building.OnOutboundTaskBuilt(obTask);
	}
}
