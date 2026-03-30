using Assets.Scripts.AI.BT;
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

		// do work

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
		if (ctx.LocalBlackBoard.TryGet(BlackBoardKey<IGridPlaceable>.TargetPlaceable, out var _))
			return Failure;

		// have to find
		return Success;
	}

	public static NodeState FindPackingStation(in BTContext ctx)
	{
		var worker = ctx.Worker;
		var packingStation = PackingService.GetAvailableStation(worker.GridPosition);

		if (packingStation == null)
			return Failure;

		packingStation.CurrentPackingWorker = worker;

		ctx.LocalBlackBoard.Set(BlackBoardKey<IGridPlaceable>.TargetPlaceable, packingStation);
		return Success;
	}

	public static NodeState CheckBoxToPack(in BTContext ctx)
	{
		if (ctx.LocalBlackBoard.TryGet(BlackBoardKey<IGridPlaceable>.TargetPlaceable, out var placeable)
			&& placeable is PackingStation station)
		{
			if (station.CurrentPackingBox)
				return Success;
		}

		// no box to pack, wait till box comes
		// todo
		// set this worker disabled and add event listener to packing station, when box comes, enable worker again

		ctx.Worker.enabled = false;

		return Failure;
	}

	public static NodeState PackEnd(in BTContext ctx)
	{

		// todo
		return Failure;
	}

}
