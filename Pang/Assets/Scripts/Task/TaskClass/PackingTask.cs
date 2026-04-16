using Unity.Mathematics;
using UnityEngine;
using static IBaseNode;
using static IBaseNode.NodeState;
public class PackingTask : WorkerTask
{
	private static PackingStationService PackingService => GameContext.Instance.OBWorkflowMgr.PackingStations;

	public PackingTask() : base(TaskType.Packing)
	{

	}
	protected override void OnTaskAssigned()
	{
	}
	protected override IBaseNode BuildWorkNode()
	{
		// root selector

		// sequence 1: find packing station
		// sequence 2: work

		// pahse 1: find packing station
		// if packing station is null
		// find the nearest packing station and when arrival, enqueue to wating queue
		// 
		// phase 2 work
		// if no box, wait
		// if box, pack box
		// when finish packing, check packed stack point
		// if packed stack point is full, wait till there is space
		// move cur pack to stack point
		// add to waiting queue

		SelectorNode root = new SelectorNode();

		SequenceNode findPackingStation = new SequenceNode();
		SequenceNode work = new SequenceNode();

		root.Add(findPackingStation);
		root.Add(work);

		// find packing station
		findPackingStation.Add(new ActionNode(CheckPackingStation));
		findPackingStation.Add(new ActionNode(FindPackingStation));
		findPackingStation.Add(AIWorker.MoveToTarget((in BTContext ctx) =>
		{
			if (ctx.LocalBlackBoard.TryGet<IGridPlaceable>("TargetBuilding", out var placeable)
			&& placeable is PackingStation station)
			{
				ctx.Worker.SetWorkerAction(WorkerStatusAction.MovingTo);

				ctx.LocalBlackBoard.Set<int3>("goalPos", station.GetClosestInteractionPoint(InteractionKind.Work, ctx.Worker.GridPosition));
				return Success;
			}

			Debug.LogError("No Such Target!!!!");
			return Failure;
		}));

		ActionNode checkBox = new ActionNode(CheckBoxToPack);
		SequenceNode moveBox2Desk = AIWorker.BuildWorkTimeInteract(WorkActionType.MoveBox, PrepareToPack);
		SequenceNode packBox = AIWorker.BuildWorkTimeInteract(WorkActionType.PackItem, null);
		SequenceNode removeFromDesk = AIWorker.BuildWorkTimeInteract(WorkActionType.MoveBox, PackEnd);

		work.Add(checkBox);
		work.Add(moveBox2Desk);
		work.Add(packBox);
		work.Add(removeFromDesk);

		return root;
	}

	public override bool CheckTaskEnd()
	{
		return false;
	}

#if UNITY_EDITOR
	public override string ShowStatus()
	{
		return $"[PackingTask] : ";
	}

#endif

	public static NodeState CheckPackingStation(in BTContext ctx)
	{
		if (ctx.LocalBlackBoard.TryGet<IGridPlaceable>("TargetBuilding", out var _))
			return Failure;

		// have to find
		return Success;
	}

	public static NodeState FindPackingStation(in BTContext ctx)
	{
		var worker = ctx.Worker;
		var packingStation = PackingService.GetAvailableStationToWork(worker.GridPosition);

		ctx.Worker.SetWorkerTarget(WorkerStatusTarget.PackingStation);

		if (packingStation == null)
		{
			Debug.Log("No Available PackingStation");

			ctx.Worker.SetWorkerAction(WorkerStatusAction.WaitingForTargetBuilding);

			return Failure;
		}

		packingStation.CurrentPackingWorker = worker;

		ctx.LocalBlackBoard.Set<IGridPlaceable>("TargetBuilding", packingStation);
		return Success;
	}

	public static NodeState CheckBoxToPack(in BTContext ctx)
	{
		if (ctx.LocalBlackBoard.TryGet<IGridPlaceable>("TargetBuilding", out var placeable)
			&& placeable is PackingStation station)
		{
			if (station.IsBoxPackable())
				return Success;

			ctx.Worker.enabled = false;


			//Debug.Log("No box to pack, wait");
		}

		ctx.Worker.SetWorkerAction(WorkerStatusAction.WaitingForItems);
		ctx.Worker.SetWorkerTarget(WorkerStatusTarget.Box);

		return Failure;
	}

	public static NodeState PrepareToPack(in BTContext ctx)
	{
		if (ctx.LocalBlackBoard.TryGet<IGridPlaceable>("TargetBuilding", out var placeable)
			&& placeable is PackingStation station)
		{
			if (station.PrepareBox())
				return Success;

		}
		return Failure;
	}

	public static NodeState PackEnd(in BTContext ctx)
	{
		if (ctx.LocalBlackBoard.TryGet<IGridPlaceable>("TargetBuilding", out var placeable)
			&& placeable is PackingStation station)
		{
			if (station.EndWorkingBox() == false)
			{
				// packed tote is not removed!!!!
				// wait till tote removed
				ctx.Worker.enabled = false;
				return Running;
			}

			return Success;
		}

		return Failure;
	}

}
