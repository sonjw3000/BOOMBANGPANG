using System.Collections.Generic;
using System.Threading;
using Unity.Mathematics;
using UnityEngine;
using static IBaseNode;
using static IBaseNode.NodeState;

public sealed class PickingTask : WorkerTask
{
	private WorkJob pickJob;
	private bool isTaskEnd = false;

	public WorkJob PickingData => pickJob;
	public WorkLine CurrentLine { get
		{
			if (PickingData.CurrentLineIndex >= pickJob.Lines.Count)
				return null;

			return PickingData.Lines[PickingData.CurrentLineIndex];
		}
	}

	static private CargoPortService CargoPorts => GameContext.Instance.OBWorkflowMgr.CargoPorts;
	static private PackingStationService PackingService => GameContext.Instance.OBWorkflowMgr.PackingStations;

	public PickingTask(WorkJob pickJob) : base(TaskType.Picking)
	{
		this.pickJob = pickJob;
	}

	protected override void OnTaskAssigned()
	{
		carryBox = OccupyWorker.GetComponent<CarryBoxAbility>();
	}

	protected override IBaseNode BuildWorkNode()
	{
		SelectorNode root = new SelectorNode();

		SelectorNode pickAfterPut = new SelectorNode();

		SequenceNode put = new SequenceNode();
		put.Add(new ActionNode(CheckPickingEnd));
		put.Add(AIWorker.MoveToTarget(WorkerStatusTarget.PackingStation, InteractionKind.Put ,GetAvailablePackingStation));
		put.Add(AIWorker.BuildWorkTimeInteract(WorkActionType.PutBox, PickingEndAction));

		SequenceNode pick = new SequenceNode();
		pick.Add(new ActionNode(CheckIsPickingState));

		pick.Add(AIWorker.CheckBoxAndGet(BoxType.Personal));
		pick.Add(AIWorker.MoveToTarget(WorkerStatusTarget.Shelf, InteractionKind.Pick, SetTarget));
		pick.Add(AIWorker.BuildWorkTimeInteract(WorkActionType.PickItem, PickItems));

		pickAfterPut.Add(put);
		pickAfterPut.Add(pick);

		root.Add(pickAfterPut);

		return root;
	}

	public override bool CheckTaskEnd()
	{
		return isTaskEnd;
	}

#if UNITY_EDITOR
	public override string ShowStatus()
	{
		return $"Picking Task: {PickingData.CurrentLineIndex} / {PickingData.Lines.Count}," + CurrentLine != null ? "" : "Goal: {CurrentLine.Source.GridPosition}";
	}
#endif

	public static NodeState CheckPickingEnd(in BTContext ctx)
	{
		PickingTask task = (PickingTask)ctx.Worker.CurrentTask;
		if (task.PickingData.IsJobEnd)
		{
			return Success;
		}
		return Failure;
	}

	public static NodeState CheckIsPickingState(in BTContext ctx)
	{
		PickingTask task = (PickingTask)ctx.Worker.CurrentTask;
		if (task.PickingData.IsJobEnd == false)
		{
			return Success;
		}
		return Failure;
	}

	public static NodeState GetAvailableOBCargoPort(in BTContext ctx)
	{
		PickingTask task = (PickingTask)ctx.Worker.CurrentTask;

		ShelfBase targetPos = null;

		targetPos = CargoPorts.GetClosestAvailableTarget(ctx.Worker.GridPosition, InteractionKind.Put);

		if (targetPos == null)
		{
			Debug.Log("No Available OB cargo port!");
			return Failure;
		}

		ctx.LocalBlackBoard.SetTargetBuilding(targetPos);

		return Success;
	}

	public static NodeState GetAvailablePackingStation(in BTContext ctx)
	{
		PackingService.TryGetWaitingStation(out var targetStation);

		ctx.LocalBlackBoard.SetTargetBuilding(targetStation);
		return Success;
	}

	public static NodeState PickingEndAction(in BTContext ctx)
	{
		PickingTask task = (PickingTask)ctx.Worker.CurrentTask;

		if (ctx.LocalBlackBoard.TryGetTargetBuilding(out var placeable)
			&& placeable is PackingStation station)
		{
			task.carryBox.GetBox(out var box);
			station.PutBox(box);

			task.isTaskEnd = true;

			return Success;
		}

		return Failure;
	}

	public static NodeState SetTarget(in BTContext ctx)
	{
		// test code
		PickingTask task = (PickingTask)ctx.Worker.CurrentTask;

		if (task.PickingData.IsJobEnd)
		{
			int cnt = task.PickingData.Lines.Count;
			Debug.Log($"task line idx: {task.PickingData.CurrentLineIndex}, task lines: {cnt}");
			// should not hit here
			Debug.Log("공이 웃으면?\n풋볼");
			Debug.Log("자가용의 반댓말은?\n커용");
			Debug.Log("푸가 넘어지면?\n쿵푸");
			Debug.Log("문신하면 무시할 수 없는 이유는?");
			Debug.Log("무시");
			Debug.Log("ㄴㄴ");

			return Failure;
		}

		// set goalPosition
		var line = task.CurrentLine;
		//ctx.LocalBlackBoard.Set<int3>("goalPos", line.Source.GetClosestInteractionPoint(InteractionKind.Pick, ctx.Worker.GridPosition));
		ctx.LocalBlackBoard.SetTargetBuilding(line.Source);

		return Success;
	}

	public static NodeState PickItems(in BTContext ctx)
	{
		PickingTask task = (PickingTask)ctx.Worker.CurrentTask;
		var curLine = task.CurrentLine;
		int removed = curLine.Source.RemoveItem(curLine.ItemID, curLine.Quantity);

		BoxBase box = ctx.Worker.GetComponent<CarryBoxAbility>().CarringBox;

		if (box == null)
		{
			Debug.Log("NO BOX??? WHY?");
			return Failure;
		}

		int realAdded = box.AddItem(task.CurrentLine.ItemID, removed);

		// 갯수를 체크해야한다
		// 중요함!
		if (task.CurrentLine.Quantity != realAdded)
		{
			// 갯수가 다르기 때문에 다른곳에서 동일 물품을 줏어야 한다. 새로운 위치로 이동해야하지 않을까?
			Debug.LogError("Reserve까지 해줬는데도 0이라고? 난 이거 인정 못해");
			return Failure;
		}

		task.PickingData.MoveToLextLine();

		return Success;
	}
}
