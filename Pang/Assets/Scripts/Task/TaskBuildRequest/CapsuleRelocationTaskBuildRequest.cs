using Unity.Mathematics;

public sealed class CapsuleRelocationTaskBuildRequest : TaskBuildRequest<CapsuleRelocationTask>
{
	private readonly CapsuleDock sourceDock;
	private readonly WorkerTask.TaskType taskType;
	private readonly CapsuleRelocationReason reason;

	public CapsuleRelocationTaskBuildRequest(
		CapsuleDock sourceDock,
		uint requestedBuildingID,
		WorkerTask.TaskType taskType,
		CapsuleRelocationReason reason) : base(requestedBuildingID)
	{
		this.sourceDock = sourceDock;
		this.taskType = taskType;
		this.reason = reason;
	}

	public override WorkerTask.TaskType TaskType => taskType;
	public override object RequestKey => GetRequestKey(taskType, sourceDock);
	public override bool IsStillValid => IsSourceStillValid(sourceDock, taskType);

	public static object GetRequestKey(WorkerTask.TaskType taskType, CapsuleDock sourceDock)
	{
		return new TaskBuildRequestKey(taskType, sourceDock);
	}

	public static object GetInboundRequestKey(InboundCargoPort sourcePort)
	{
		return GetRequestKey(WorkerTask.TaskType.IB, sourcePort);
	}

	public static object GetOutboundRequestKey(CapsuleBuffer sourceBuffer)
	{
		return GetRequestKey(WorkerTask.TaskType.OB, sourceBuffer);
	}

	protected override bool TryBuildTask(out CapsuleRelocationTask task)
	{
		task = null;
		if (IsStillValid == false || BuildingManager == null || BuildingManager.TryGetBuilding(RequestedBuildingID, out Building building) == false)
			return false;

		CapsuleDock targetDock = ResolveTarget(building);
		if (targetDock == null)
			return false;

		task = new CapsuleRelocationTask(taskType, sourceDock, targetDock, RequestedBuildingID, reason);
		return true;
	}

	public override void OnTaskQueued(WorkerTask task)
	{
		if (task is CapsuleRelocationTask relocationTask &&
			BuildingManager != null &&
			BuildingManager.TryGetBuilding(RequestedBuildingID, out Building building))
		{
			building.OnCapsuleRelocationTaskBuilt(relocationTask);
		}
	}

	private CapsuleDock ResolveTarget(Building building)
	{
		if (building == null || sourceDock == null)
			return null;

		int3 sourcePoint = ResolveInteractionOrigin(sourceDock, InteractionKind.Pick);
		ZoneFilter zoneFilter = ZoneFilter.ForContainer(sourceDock.DockedCapsule);
		return taskType switch
		{
			WorkerTask.TaskType.IB when sourceDock is InboundCargoPort =>
				building.ResolveInboundBufferTarget(sourcePoint, zoneFilter),
			WorkerTask.TaskType.OB when sourceDock is CapsuleBuffer sourceBuffer && building.CanBuildOutboundTaskRequest(sourceBuffer) =>
				building.ResolveOutboundPortTarget(sourceBuffer.GridPosition, zoneFilter),
			_ => null,
		};
	}

	private static bool IsSourceStillValid(CapsuleDock dock, WorkerTask.TaskType taskType)
	{
		if (dock == null || dock.CanGetBox() == false)
			return false;

		return taskType switch
		{
			WorkerTask.TaskType.IB when dock is InboundCargoPort => dock.IsCapsuleEmpty() == false,
			WorkerTask.TaskType.OB when dock is CapsuleBuffer sourceBuffer => sourceBuffer.CanDispatchToOutbound(),
			_ => false,
		};
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
