using UnityEngine;
using System;
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
	private bool hasPicked = false;

	public WaterTask(TransferContext from, TransferContext to) : base(TaskType.Water)
	{
		this.from = from;
		this.to = to;
	}

	public WaterTaskSaveData CaptureState(Func<GameObject, int> getPlaceableId)
	{
		return new WaterTaskSaveData
		{
			From = CaptureTransferContext(from, getPlaceableId),
			To = CaptureTransferContext(to, getPlaceableId),
			WorkPhase = workPhase,
			HasPicked = hasPicked,
		};
	}

	public void RestoreState(bool workPhase, bool hasPicked)
	{
		this.workPhase = workPhase;
		this.hasPicked = hasPicked;
	}

	protected override void OnTaskAssigned()
	{
		// todo
		// check human like ability
		if (WorkerCarryBox == null)
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

		SelectorNode pickIfNeeded = new();
		pickIfNeeded.Add(new ActionNode(HasPicked));

		SequenceNode pick = new();
		pick.Add(AIWorker.MoveToTarget(WorkerStatusTarget.None, InteractionKind.Pick, PickSet));
		pick.Add(AIWorker.BuildWorkTimeInteract(WorkActionType.PickBox, Pick));
		pickIfNeeded.Add(pick);

		SequenceNode work = new();
		work.Add(pickIfNeeded);
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
		if ((task.from.transferType == TransferObjectType.Item && task.WorkerCarryBox.CarryingBox != null) ||
			(task.from.transferType == TransferObjectType.Box && task.WorkerCarryBox.CarryingBox == null))
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

	static public NodeState HasPicked(in BTContext ctx)
	{
		WaterTask task = ctx.Worker.CurrentTask as WaterTask;
		return task.hasPicked ? Success : Failure;
	}

	static public NodeState CheckBoxState(in BTContext ctx)
	{
		// if the box is required in picking, then have to pick box
		// 
		WaterTask task = ctx.Worker.CurrentTask as WaterTask;

		if (task.from.transferType == TransferObjectType.Item &&
			task.WorkerCarryBox.CarryingBox == null)
			return Success;

		return Failure;
	}

	static public NodeState CheckBoxNotNeedState(in BTContext ctx)
	{
		// if box is not required in picking, then don't need to pick box
		WaterTask task = ctx.Worker.CurrentTask as WaterTask;
		
		if (task.from.transferType == TransferObjectType.Box &&
			task.WorkerCarryBox.CarryingBox != null)
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
			if (task.from.target is not IItemContainer fromContainer)
			{
				Debug.LogError("Target is not item interaction but transfer type is item??");
				return Failure;
			}

			TransferResultKind result = ItemTransferUtility.MoveAllStacks(new(fromContainer, task.WorkerCarryBox.CarryingBox));
			if (result == TransferResultKind.None)
				return Failure;

			if (result == TransferResultKind.Partial)
				GameContext.Instance.TaskMgr.EnqueueTask(new WaterTask(task.from, task.to));
		}
		else
		{
			BoxInteraction boxInteraction = task.from.target as BoxInteraction;
			if (boxInteraction.GetBox(out var box) == false) return Failure;
			if (task.WorkerCarryBox.PutBox(box) == false) return Failure;
		}

		task.hasPicked = true;
		return Success;
	}

	static public NodeState PutSet(in BTContext ctx)
	{
		WaterTask task = ctx.Worker.CurrentTask as WaterTask;

		if (task.to.transferType == TransferObjectType.Item)
		{
			if (task.to.target is not IItemContainer toContainer)
			{
				Debug.LogError("Target is not item interaction but transfer type is item??");
				return Failure;
			}

			BoxBase box = task.WorkerCarryBox.CarryingBox;
			if (box == null)
			{
				Debug.LogError("No box to put??");
				return Failure;
			}

			if (CanAcceptAllStacks(toContainer, box) == false)
			{
				// 현재는 standby에서 task를 계속 재평가한다.
				// 이후 목적지/자원 매니저가 worker를 disable 후 가능해질 때 enable하는 패턴으로 교체할 수 있다.
				ctx.Worker.SetWorkerTarget(WorkerStatusTarget.WorkTarget);
				ctx.Worker.SetWorkerAction(WorkerStatusAction.WaitingForTargetBuilding);
				return AIWorker.MoveToStandbyWhileWaiting(ctx);
			}
		}
		else
		{
			if (task.to.target is not BoxInteraction boxInteraction)
			{
				Debug.LogError("Target is not box interaction but transfer type is box??");
				return Failure;
			}

			if (boxInteraction.CanPutBox() == false)
			{
				// 현재는 standby에서 task를 계속 재평가한다.
				// 이후 목적지/자원 매니저가 worker를 disable 후 가능해질 때 enable하는 패턴으로 교체할 수 있다.
				ctx.Worker.SetWorkerTarget(WorkerStatusTarget.WorkTarget);
				ctx.Worker.SetWorkerAction(WorkerStatusAction.WaitingForTargetBuilding);
				return AIWorker.MoveToStandbyWhileWaiting(ctx);
			}
		}

		ctx.LocalBlackBoard.SetTargetBuilding(task.to.target as IGridPlaceable);

		return Success;
	}

	static public NodeState Put(in BTContext ctx)
	{
		WaterTask task = ctx.Worker.CurrentTask as WaterTask;

		if (task.to.transferType == TransferObjectType.Item)
		{
			if (task.to.target is not IItemContainer toContainer)
			{
				Debug.LogError("Target is not item interaction but transfer type is item??");
				return Failure;
			}

			TransferResultKind result = ItemTransferUtility.MoveAllStacks(new(task.WorkerCarryBox.CarryingBox, toContainer));
			if (result == TransferResultKind.None)
				return Failure;
			if (result == TransferResultKind.Partial)
			{
				// PutSet에서 사전 검사했는데도 partial이면 도착 후 경쟁 상태가 생긴 것이다.
				// hasPicked 상태를 유지하고 다음 평가에서 목적지 가능 여부를 다시 확인한다.
				return Failure;
			}
		}
		else
		{
			BoxInteraction boxInteraction = task.to.target as BoxInteraction;

			if (task.WorkerCarryBox.GetBox(out var box) == false)
			{
				Debug.LogError("No box to put??");
				return Failure;
			}
			if (boxInteraction.PutBox(box) == false)
			{
				task.WorkerCarryBox.PutBox(box);
				Debug.LogError("Failed to put box??");
				return Failure;
			}
		}

		return Success;
	}

	private static bool CanAcceptAllStacks(IItemContainer target, BoxBase source)
	{
		if (target == null || source == null)
			return false;

		for (int i = 0; i < source.Stacks.Count; ++i)
		{
			if (target.CanAcceptStack(source.Stacks[i]) == false)
				return false;
		}

		return true;
	}

	private static TransferContextSaveData CaptureTransferContext(TransferContext context, Func<GameObject, int> getPlaceableId)
	{
		if (context?.target is not Component targetComponent)
			return null;

		return new TransferContextSaveData
		{
			TargetPlaceableId = getPlaceableId != null ? getPlaceableId(targetComponent.gameObject) : -1,
			TransferType = context.transferType,
		};
	}
}
