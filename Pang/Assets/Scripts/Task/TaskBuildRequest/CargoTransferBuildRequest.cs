using UnityEngine;
using Unity.Mathematics;


public sealed class CargoTransferBuildRequest : TaskBuildRequest<CargoTransferTask>
{
	private readonly OutboundCargoPort sourcePort;

	public CargoTransferBuildRequest(OutboundCargoPort sourcePort, uint requestedBuildingID) : base(requestedBuildingID)
	{
		this.sourcePort = sourcePort;
	}

	public override WorkerTask.TaskType TaskType => WorkerTask.TaskType.CargoTransfer;
	public override object RequestKey => GetRequestKey(sourcePort);
	public override bool IsStillValid => sourcePort != null && sourcePort.CanGetBox();

	public static object GetRequestKey(OutboundCargoPort sourcePort)
	{
		return new TaskBuildRequestKey(WorkerTask.TaskType.CargoTransfer, sourcePort);
	}

	protected override bool TryBuildTask(out CargoTransferTask task)
	{
		task = null;
		if (IsStillValid == false || ResolveSourceBuilding(sourcePort, out Building sourceBuilding) == false)
			return false;

		int3 sourcePoint = ResolveInteractionOrigin(sourcePort, InteractionKind.Pick);
		ZoneFilter zoneFilter = ZoneFilter.ForContainer(sourcePort.DockedCapsule);
		InboundCargoPort targetPort = sourceBuilding.ResolveLinkedInboundPortTarget(sourcePoint, zoneFilter);
		if (targetPort == null)
			return false;

		task = new CargoTransferTask(sourcePort, targetPort);
		return true;
	}

	public override void OnTaskQueued(WorkerTask task)
	{
		if (task is CargoTransferTask cargoTransferTask && GameContext.HasInstance)
			GameContext.Instance.OBWorkflowSvc?.OnCargoTransferTaskBuilt(cargoTransferTask);
	}

	private bool ResolveSourceBuilding(CargoPort sourcePort, out Building building)
	{
		building = null;
		if (sourcePort == null || GridService == null || BuildingManager == null)
			return false;

		GridCell cell = GridService.GetCell(sourcePort.GridPosition);
		return cell != null &&
			cell.BuildingId != 0 &&
			BuildingManager.TryGetBuilding(cell.BuildingId, out building) &&
			building != null;
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
