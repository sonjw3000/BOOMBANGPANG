using Unity.Mathematics;
using UnityEngine;
using static IBaseNode;
using static IBaseNode.NodeState;

public enum TransferObjectType
{
	Box,
	Item
}

public class TransferContext
{
	public readonly IInteractionPoint target;
	public readonly TransferObjectType transferType;

	public TransferContext(IInteractionPoint target, TransferObjectType transferType)
	{
		this.target = target;
		this.transferType = transferType;
	}
}

public class WaterTask : WorkerTask
{
	private readonly TransferContext from;
	private readonly TransferContext to;

	private bool workPhase = false;

	public WaterTask(TransferContext from, TransferContext to) : base(TaskType.Water)
	{
		this.from = from;
		this.to = to;
	}

	protected override void OnTaskAssigned()
	{
		// todo
		// check human like ability
		carryBox = OccupyWorker.GetComponent<CarryBoxAbility>();

		if (carryBox == null)
		{
			Debug.LogError("No carryBox ability but assigned to ccc!!");
		}
	}

	protected override IBaseNode BuildWorkNode()
	{
		SequenceNode root = new();

		SelectorNode ensureCarryState = new();

		// check need box
		SequenceNode checkRequirement = new();
		checkRequirement.Add(new ActionNode(CheckBoxState));
		checkRequirement.Add(AIWorker.GetBox(BoxType.Personal));
		checkRequirement.Add(new ActionNode(SetCarryStateReady));

		// check return box
		SequenceNode checkBoxReq = new();
		checkBoxReq.Add(new ActionNode(CheckBoxNotNeedState));
		checkBoxReq.Add(AIWorker.ReturnBox());
		checkBoxReq.Add(new ActionNode(SetCarryStateReady));

		ensureCarryState.Add(new ActionNode(IsCarryStateReady));
		ensureCarryState.Add(checkBoxReq);
		ensureCarryState.Add(checkRequirement);
		ensureCarryState.Add(new ActionNode(WaitForCarryRequirement));

		SequenceNode work = new();
		work.Add(AIWorker.MoveToTarget(WorkerStatusTarget.None, InteractionKind.Pick, PickSet));
		work.Add(AIWorker.BuildWorkTimeInteract(WorkActionType.PickBox, Pick));
		work.Add(AIWorker.MoveToTarget(WorkerStatusTarget.None, InteractionKind.Put, PutSet));
		work.Add(AIWorker.BuildWorkTimeInteract(WorkActionType.PutBox, Put));
		work.Add(new ActionNode(AIWorker.TaskCompleted));

		root.Add(ensureCarryState);
		root.Add(work);

		return root;
	}

	public override bool CheckTaskEnd()
	{
		return false;
	}

#if UNITY_EDITOR
	public override string ShowStatus()
	{

		return $"[WaterTask] Working: {0}";
	}
#endif

	static public NodeState IsCarryStateReady(in BTContext ctx)
	{
		// return true when box state is ready
		WaterTask task = ctx.Worker.CurrentTask as WaterTask;

		if (task.workPhase)
			return Success;

		// if fulfilled workPhase = true;
		if ((task.from.transferType == TransferObjectType.Item && task.carryBox.CarringBox != null) ||
			(task.from.transferType == TransferObjectType.Box && task.carryBox.CarringBox == null))
		{
			task.workPhase = true;
			return Success;
		}

		return Failure;
	}

	static public NodeState SetCarryStateReady(in BTContext ctx)
	{
		WaterTask task = ctx.Worker.CurrentTask as WaterTask;
		task.workPhase = true;

		return Success;
	}

	static public NodeState WaitForCarryRequirement(in BTContext ctx)
	{
		Debug.Log("Waiting for box requirement fulfill");
		return Running;
	}

	static public NodeState CheckBoxState(in BTContext ctx)
	{
		// if the box is required in picking, then have to pick box
		// 
		WaterTask task = ctx.Worker.CurrentTask as WaterTask;

		if (task.from.transferType == TransferObjectType.Item &&
			task.carryBox.CarringBox == null)
			return Success;

		return Failure;
	}

	static public NodeState CheckBoxNotNeedState(in BTContext ctx)
	{
		// if box is not required in picking, then don't need to pick box
		WaterTask task = ctx.Worker.CurrentTask as WaterTask;
		
		if (task.from.transferType == TransferObjectType.Box &&
			task.carryBox.CarringBox != null)
			return Success;

		return Failure;
	}

	static public NodeState PickSet(in BTContext ctx)
	{
		WaterTask task = ctx.Worker.CurrentTask as WaterTask;

		ctx.LocalBlackBoard.SetTargetBuilding(task.from.target as IGridPlaceable);
		return Success;
	}

	static public NodeState Pick(in BTContext ctx)
	{
		WaterTask task = ctx.Worker.CurrentTask as WaterTask;

		if (task.from.transferType == TransferObjectType.Item)
		{
			if (task.from.target is ItemInteraction target == false)
			{
				Debug.LogError("Target is not item interaction but transfer type is item??");
				return Failure;
			}

			if (target.MoveToBox(task.carryBox.CarringBox))
			{
				// 추가 뭐시기를 요청하던가 해야함
			}
		}
		else
		{
			BoxInteraction boxInteraction = task.from.target as BoxInteraction;
			if (boxInteraction.GetBox(out var box) == false) return Failure;
			if (task.carryBox.PutBox(box) == false) return Failure;
		}

		return Success;
	}

	static public NodeState PutSet(in BTContext ctx)
	{
		WaterTask task = ctx.Worker.CurrentTask as WaterTask;
		ctx.LocalBlackBoard.SetTargetBuilding(task.to.target as IGridPlaceable);

		return Success;
	}

	static public NodeState Put(in BTContext ctx)
	{
		WaterTask task = ctx.Worker.CurrentTask as WaterTask;

		if (task.to.transferType == TransferObjectType.Item)
		{
			if (task.to.target is ItemInteraction target == false)
			{
				Debug.LogError("Target is not item interaction but transfer type is item??");
				return Failure;
			}

			target.BringFromBox(task.carryBox.CarringBox);
		}
		else
		{
			BoxInteraction boxInteraction = task.from.target as BoxInteraction;

			if (task.carryBox.GetBox(out var box) == false)
			{
				Debug.LogError("No box to put??");
				return Failure;
			}
			if (boxInteraction.PutBox(box) == false)
			{
				Debug.LogError("Failed to put box??");
				return Failure;
			}
		}

		return Success;
	}
}
