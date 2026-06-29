using Unity.Mathematics;

public sealed class IBTaskBuildRequest : TaskBuildRequest<IBTask>
{
	private readonly InboundCargoPort sourcePort;

	public IBTaskBuildRequest(InboundCargoPort sourcePort, uint requestedBuildingID) : base(requestedBuildingID)
	{
		this.sourcePort = sourcePort;
	}

	public override WorkerTask.TaskType TaskType => WorkerTask.TaskType.IB;
	public override object RequestKey => GetRequestKey(sourcePort);
	public override bool IsStillValid => sourcePort != null && sourcePort.CanGetBox() && sourcePort.IsCapsuleEmpty() == false;

	public static object GetRequestKey(InboundCargoPort sourcePort)
	{
		return new TaskBuildRequestKey(WorkerTask.TaskType.IB, sourcePort);
	}

	protected override bool TryBuildTask(out IBTask task)
	{
		task = null;
		if (IsStillValid == false || BuildingManager == null || BuildingManager.TryGetBuilding(RequestedBuildingID, out Building building) == false)
			return false;

		int3 sourcePoint = ResolveInteractionOrigin(sourcePort, InteractionKind.Pick);
		ZoneFilter zoneFilter = ZoneFilter.ForContainer(sourcePort.DockedCapsule);
		CapsuleBuffer targetBuffer = building.ResolveInboundBufferTarget(sourcePoint, zoneFilter);
		if (targetBuffer == null)
			return false;

		task = new IBTask(sourcePort, RequestedBuildingID, targetBuffer);
		return true;
	}

	public override void OnTaskQueued(WorkerTask task)
	{
		if (task is IBTask ibTask && BuildingManager != null && BuildingManager.TryGetBuilding(RequestedBuildingID, out Building building))
			building.OnInboundTaskBuilt(ibTask);
	}

	private static int3 ResolveInteractionOrigin(BoxInteraction interactionTarget, InteractionKind interactionKind)
	{
		if (interactionTarget == null)
			return default;

		if (interactionTarget.InteractionPointMap != null &&
			interactionTarget.InteractionPointMap.ContainsKey(interactionKind) &&
			interactionTarget.InteractionPointMap[interactionKind] != null &&
			interactionTarget.InteractionPointMap[interactionKind].Count > 0)
		{
			return interactionTarget.GetClosestInteractionPoint(interactionKind, interactionTarget.GridPosition);
		}

		return interactionTarget.GridPosition;
	}
}
