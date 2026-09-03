using System.Collections.Generic;
using Assets.Scripts.AI.BT;
using Unity.Mathematics;
using static IBaseNode;

public abstract partial class AIWorker
{
	private bool returningPlayerContainer;
	private BoxInteraction playerContainerReturnTarget;
	private bool returningPlayerContainerContents;
	private readonly List<CapsuleDock> playerContainerReturnDocks = new();

	public bool IsReturningPlayerContainer => returningPlayerContainer;

	private IBaseNode BuildPlayerContainerReturnNode()
	{
		SequenceNode node = new();
		node.Add(new ActionNode((in BTContext ctx) =>
		{
			if (ctx.Worker.IsReturningPlayerContainer == false || ctx.Worker.IsPlayerOverride ||
				ctx.Worker.CurrentTask != null)
				return NodeState.Failure;
			if (ctx.Worker.CarryingAbility?.CarryingBox != null)
				return NodeState.Success;

			ctx.Worker.CompletePlayerContainerReturn();
			return NodeState.Failure;
		}));
		node.Add(MoveToTarget(WorkerStatusTarget.Box, InteractionKind.Put, SelectPlayerContainerReturnTarget));
		SelectorNode put = new();
		SequenceNode contents = new();
		contents.Add(new ActionNode((in BTContext ctx) => ctx.Worker.returningPlayerContainerContents ? NodeState.Success : NodeState.Failure));
		contents.Add(BuildWorkTimeInteract(WorkActionType.PutItem, PutPlayerContainerContents));
		put.Add(contents);
		SequenceNode container = new();
		container.Add(new ActionNode((in BTContext ctx) => ctx.Worker.returningPlayerContainerContents || ctx.Worker.playerContainerReturnTarget == null ? NodeState.Failure : NodeState.Success));
		container.Add(BuildWorkTimeInteract(WorkActionType.PutBox, PutPlayerContainerReturn));
		put.Add(container);
		node.Add(put);
		node.Add(new ActionNode((in BTContext ctx) =>
		{
			if (ctx.Worker.CarryingAbility?.CarryingBox != null)
			{
				ctx.Worker.ReleasePlayerContainerReturnTarget();
				return NodeState.Failure;
			}
			ctx.Worker.CompletePlayerContainerReturn();
			return NodeState.Success;
		}));
		return node;
	}

	private static NodeState SelectPlayerContainerReturnTarget(in BTContext ctx)
	{
		AIWorker worker = ctx.Worker;
		BoxBase box = worker.CarryingAbility?.CarryingBox;
		if (box == null)
			return NodeState.Failure;

		worker.ReleasePlayerContainerReturnTarget();
		GameContext context = GameContext.Instance;
		uint buildingId = worker.PrimaryBuildingId;
		if (buildingId == 0)
			TryGetBuildingId(worker.GridPosition, out buildingId);

		if (box is CargoCapsule capsule)
		{
			if (context.CapsuleDockSvc != null &&
				context.CapsuleDockSvc.TryQueryDocks(buildingId, worker.playerContainerReturnDocks))
			{
				int bestDistance = int.MaxValue;
				foreach (CapsuleDock dock in worker.playerContainerReturnDocks)
				{
					if (worker.CanReturnPlayerContainerTo(dock, capsule) == false ||
						context.CapsuleRelocateCoordinator.IsReserved(dock))
						continue;

					int distance = math.abs(worker.GridPosition.x - dock.GridPosition.x) +
						math.abs(worker.GridPosition.z - dock.GridPosition.z);
					if (distance >= bestDistance)
						continue;
					bestDistance = distance;
					worker.playerContainerReturnTarget = dock;
				}
				worker.playerContainerReturnDocks.Clear();
				if (worker.playerContainerReturnTarget is CapsuleDock target &&
					context.CapsuleRelocateCoordinator.TryReserveActiveTarget(target) == false)
					worker.playerContainerReturnTarget = null;
			}
		}
		else if (box.Stacks.Count > 0 && context.CapsuleBufferSvc != null)
		{
			foreach (CapsuleBuffer buffer in context.CapsuleBufferSvc.GetBuffers(buildingId))
			{
				if (worker.CanReturnPlayerContentsTo(buffer, box) == false ||
					context.CapsuleRelocateCoordinator.IsReserved(buffer) ||
					context.TaskMgr?.HasManagedTaskFacilityDependency(buffer) == true ||
					context.CapsuleRelocateCoordinator.TryClaimForPlayer(buffer) == false)
					continue;
				worker.playerContainerReturnTarget = buffer;
				worker.returningPlayerContainerContents = true;
				break;
			}
		}
		else if (box.Stacks.Count == 0 && context.WMSys?.BoxPoolService != null &&
			context.WMSys.BoxPoolService.TryFindDestination(buildingId, worker.GridPosition,
				InteractionKind.Put, FacilityFilter.ForWorker(worker), out BoxPool pool) && pool.CanStoreBox(box))
		{
			worker.playerContainerReturnTarget = pool;
		}

		ctx.LocalBlackBoard.SetTargetBuilding(worker.playerContainerReturnTarget);
		if (worker.playerContainerReturnTarget != null)
			return NodeState.Success;

		worker.SetWorkerTarget(WorkerStatusTarget.Box);
		worker.SetWorkerAction(WorkerStatusAction.WaitingForTargetBuilding);
		return NodeState.Running;
	}

	private bool CanReturnPlayerContainerTo(BoxInteraction target, BoxBase box)
	{
		if (target == null || box == null || target.CanPutBox() == false ||
			GameContext.Instance.FacilityMgr?.IsInvalidating(target) == true)
			return false;
		if (target is BoxPool pool)
			return box.Stacks.Count == 0 && pool.CanStoreBox(box) && FacilityFilter.ForWorker(this).MatchesCurrentRules(pool);
		if (target is not CapsuleDock dock || box is not CargoCapsule capsule ||
			dock.CanAcceptCargoRoute(capsule.RouteKind) == false)
			return false;

		if (dock is CapsuleBuffer buffer)
		{
			return (capsule.Stacks.Count == 0 && buffer.RetainEmptyCapsule) ||
				GameContext.Instance.CapsuleBufferSvc?.IsRuleMatchedBuffer(buffer, capsule, false) == true;
		}
		return (dock is WasteBinDock || dock is WasteContainer) &&
			FacilityFilter.ForWorker(this).MatchesCurrentRules(dock);
	}

	private bool CanReturnPlayerContentsTo(CapsuleBuffer buffer, BoxBase box)
	{
		if (buffer == null || box == null || box is CargoCapsule || box.Stacks.Count == 0 ||
			buffer.DockedCapsule == null || buffer.Stacks.Count != 0 ||
			GameContext.Instance.FacilityMgr?.IsInvalidating(buffer) == true ||
			box.TotalSize > buffer.MaxSize)
			return false;
		foreach (ItemStack stack in box.Stacks)
			if (buffer.CanAcceptStack(stack) == false) return false;
		PickingManifest manifest = null;
		GameContext.Instance.OBWorkflowSvc?.TryGetPickingManifest(box, out manifest);
		return FacilityFilter.TryForContainer(box, manifest, false, out FacilityFilter filter, this) &&
			filter.MatchesCurrentRules(buffer);
	}

	private static NodeState PutPlayerContainerContents(in BTContext ctx)
	{
		AIWorker worker = ctx.Worker;
		BoxBase box = worker.CarryingAbility?.CarryingBox;
		CapsuleBuffer buffer = worker.playerContainerReturnTarget as CapsuleBuffer;
		if (worker.IsAtPlayerContainerReturnPoint() == false || worker.CanReturnPlayerContentsTo(buffer, box) == false)
		{
			worker.ReleasePlayerContainerReturnTarget();
			ctx.LocalBlackBoard.RemoveTargetBuilding();
			return NodeState.Failure;
		}

		List<(uint itemId, int quantity, bool packed)> movedStacks = new();
		TransferResultKind result = ItemTransferUtility.MoveAllStacks(new FullyTransferPayload(box, buffer,
			stack => movedStacks.Add((stack.ItemID, stack.Quantity, stack.HasStatus(ItemStatus.Packed)))));
		foreach (var moved in movedStacks)
		{
			GameContext.Instance.OBWorkflowSvc?.TransferPickingManifest(box, buffer.DockedCapsule,
				moved.itemId, moved.quantity, moved.packed);
			worker.ReportItemHandling(moved.itemId, moved.quantity, buffer);
		}
		worker.ReleasePlayerContainerReturnTarget();
		ctx.LocalBlackBoard.RemoveTargetBuilding();
		return result == TransferResultKind.Complete ? NodeState.Success : NodeState.Failure;
	}

	private bool IsAtPlayerContainerReturnPoint()
	{
		if (playerContainerReturnTarget == null)
			return false;
		foreach (InteractionPoint point in playerContainerReturnTarget.InteractionPoints)
			if ((point.InteractionKind & InteractionKind.Put) != 0 && point.Point.Equals(GridPosition))
				return true;
		return false;
	}

	private static NodeState PutPlayerContainerReturn(in BTContext ctx)
	{
		AIWorker worker = ctx.Worker;
		BoxInteraction target = worker.playerContainerReturnTarget;
		BoxBase box = worker.CarryingAbility?.CarryingBox;
		if (worker.IsAtPlayerContainerReturnPoint() == false || worker.CanReturnPlayerContainerTo(target, box) == false ||
			(target is CapsuleDock dock && GameContext.Instance.CapsuleRelocateCoordinator.IsPlayerClaimed(dock)))
		{
			worker.ReleasePlayerContainerReturnTarget();
			ctx.LocalBlackBoard.RemoveTargetBuilding();
			return NodeState.Failure;
		}

		if (worker.TryDetachBox(out box) == false)
			return NodeState.Failure;
		if (target.PutBox(box) == false)
		{
			worker.TryAttachBox(box);
			worker.ReleasePlayerContainerReturnTarget();
			ctx.LocalBlackBoard.RemoveTargetBuilding();
			return NodeState.Failure;
		}
		worker.ReportBoxHandling(box);
		return NodeState.Success;
	}

	private void CompletePlayerContainerReturn()
	{
		CancelPlayerContainerReturn();
		localBlackBoard.RemoveTargetBuilding();
		if (IsOperational == false)
			return;
		SetWorkerTarget(WorkerStatusTarget.None);
		SetWorkerAction(WorkerStatusAction.Idle);
		if (GameContext.HasInstance)
			WorkerMgr.AddIdleWorker(this);
	}

	private void CancelPlayerContainerReturn()
	{
		returningPlayerContainer = false;
		ReleasePlayerContainerReturnTarget();
	}

	private void ReleasePlayerContainerReturnTarget()
	{
		BoxInteraction target = playerContainerReturnTarget;
		bool contents = returningPlayerContainerContents;
		playerContainerReturnTarget = null;
		returningPlayerContainerContents = false;
		if (target is CapsuleDock dock && GameContext.HasInstance)
		{
			if (contents)
				GameContext.Instance.CapsuleRelocateCoordinator?.ReleasePlayerClaim(dock);
			else
				GameContext.Instance.CapsuleRelocateCoordinator?.NotifyRelocationTargetReleased(dock);
		}
	}
}
