using Unity.Mathematics;
using UnityEngine;

public abstract partial class WorkerTask
{
	private BoxBase payloadBox;
	private int3 payloadRecoveryPosition;
	private bool hasPendingPayloadRecovery;

	public BoxBase PayloadBox => payloadBox;
	public bool HasPendingPayloadRecovery => hasPendingPayloadRecovery;
	public bool IsValidForDispatch => payloadBox == null || payloadBox.IsValid;

	internal void TrackPayloadBox(BoxBase box)
	{
		if (box == null || payloadBox == box)
			return;

		ClearPayloadBox();
		payloadBox = box;
		payloadBox.OnInvalidated += HandlePayloadBoxInvalidated;
	}

	internal void ReleasePayloadBox(BoxBase box)
	{
		if (box == null || payloadBox != box || hasPendingPayloadRecovery)
			return;

		ClearPayloadBox();
	}

	internal bool TryGetPayloadRecovery(out BoxBase box, out int3 position)
	{
		box = payloadBox;
		position = payloadRecoveryPosition;
		return hasPendingPayloadRecovery && box != null && box.IsValid;
	}

	internal bool RestorePayloadRecovery(BoxBase box, in int3 position)
	{
		if (box == null || box.IsValid == false)
			return false;

		PreparePayloadRecovery(box, position);
		box.transform.SetParent(null);
		box.transform.position = new Vector3(position.x, position.y, position.z);
		return true;
	}

	protected static IBaseNode.NodeState CheckWorkerCarriesPayload(in BTContext ctx)
	{
		WorkerTask task = ctx.Worker?.CurrentTask;
		BoxBase carriedBox = ctx.Worker?.CarryingAbility?.CarryingBox;
		return task != null && task.payloadBox != null && carriedBox == task.payloadBox
			? IBaseNode.NodeState.Success
			: IBaseNode.NodeState.Failure;
	}

	private void PreparePayloadRecovery(BoxBase box, in int3 position)
	{
		TrackPayloadBox(box);
		payloadRecoveryPosition = position;
		hasPendingPayloadRecovery = true;
	}

	private IBaseNode BuildPayloadRecoveryNode()
	{
		SelectorNode root = new();
		root.Add(new ActionNode((in BTContext ctx) =>
			hasPendingPayloadRecovery == false
				? IBaseNode.NodeState.Success
				: IBaseNode.NodeState.Failure));

		SequenceNode recover = new();
		recover.Add(new ActionNode(MoveToPayloadRecovery));
		recover.Add(new ActionNode(PickPayloadRecovery));
		root.Add(recover);
		return root;
	}

	private IBaseNode.NodeState MoveToPayloadRecovery(in BTContext ctx)
	{
		if (ValidatePayloadRecovery() == false)
			return InvalidateFromPayloadLoss();

		AIWorker worker = ctx.Worker;
		if (worker?.CarryingAbility?.CarryingBox == payloadBox)
			return IBaseNode.NodeState.Success;
		if (worker != null && IsAdjacent(worker.GridPosition, payloadRecoveryPosition))
			return IBaseNode.NodeState.Success;

		FindRoute route = worker?.RouteFinder;
		if (route == null)
			return IBaseNode.NodeState.Failure;

		if (route.HasActiveGoal)
		{
			if (route.IsGoal)
			{
				route.enabled = false;
				route.ConsumeArrivedGoal();
				return IBaseNode.NodeState.Success;
			}

			worker.enabled = false;
			worker.SetWorkerTarget(WorkerStatusTarget.Box);
			worker.SetWorkerAction(WorkerStatusAction.MovingTo);
			return IBaseNode.NodeState.Running;
		}

		if (TryGetPayloadPickupPosition(worker, out int3 pickupPosition) == false)
		{
			worker.SetWorkerTarget(WorkerStatusTarget.Box);
			worker.SetWorkerAction(WorkerStatusAction.WaitingForTargetBuilding);
			return AIWorker.KeepTaskWaiting(ctx);
		}

		if (worker.GridPosition.Equals(pickupPosition))
			return IBaseNode.NodeState.Success;

		worker.SetWorkerTarget(WorkerStatusTarget.Box);
		route.enabled = true;
		route.SetGoalPosition(pickupPosition);
		return IBaseNode.NodeState.Running;
	}

	private IBaseNode.NodeState PickPayloadRecovery(in BTContext ctx)
	{
		if (ValidatePayloadRecovery() == false)
			return InvalidateFromPayloadLoss();

		CarryBoxAbility carryAbility = ctx.Worker?.CarryingAbility;
		if (carryAbility == null)
			return IBaseNode.NodeState.Failure;

		if (carryAbility.CarryingBox == payloadBox)
		{
			hasPendingPayloadRecovery = false;
			return IBaseNode.NodeState.Success;
		}

		if (carryAbility.CarryingBox != null || IsAdjacent(ctx.Worker.GridPosition, payloadRecoveryPosition) == false)
			return IBaseNode.NodeState.Failure;

		if (carryAbility.PutBox(payloadBox) == false)
			return IBaseNode.NodeState.Failure;

		hasPendingPayloadRecovery = false;
		return IBaseNode.NodeState.Success;
	}

	private bool ValidatePayloadRecovery()
	{
		if (hasPendingPayloadRecovery == false || payloadBox == null || payloadBox.IsValid == false)
			return false;

		return GameContext.HasInstance &&
			GameContext.Instance.BoxMgr.TryGetBox(payloadBox.Type, payloadBox.BoxId, out BoxBase registeredBox) &&
			registeredBox == payloadBox;
	}

	private IBaseNode.NodeState InvalidateFromPayloadLoss()
	{
		if (GameContext.HasInstance)
			GameContext.Instance.TaskMgr.InvalidateTask(this);

		return IBaseNode.NodeState.Abort;
	}

	private bool TryGetPayloadPickupPosition(AIWorker worker, out int3 pickupPosition)
	{
		pickupPosition = default;
		GridService gridService = GameContext.HasInstance ? GameContext.Instance.GridService : null;
		if (worker == null || gridService == null || gridService.GetCell(payloadRecoveryPosition) == null)
			return false;

		int3[] candidates =
		{
			payloadRecoveryPosition + new int3(1, 0, 0),
			payloadRecoveryPosition + new int3(-1, 0, 0),
			payloadRecoveryPosition + new int3(0, 0, 1),
			payloadRecoveryPosition + new int3(0, 0, -1),
		};

		int bestDistance = int.MaxValue;
		for (int i = 0; i < candidates.Length; ++i)
		{
			GridCell cell = gridService.GetCell(candidates[i]);
			if (cell == null || cell.IsBlocked || gridService.IsSameRegion(payloadRecoveryPosition, candidates[i]) == false)
				continue;

			int distance = math.abs(worker.GridPosition.x - candidates[i].x) +
				math.abs(worker.GridPosition.y - candidates[i].y) +
				math.abs(worker.GridPosition.z - candidates[i].z);
			if (distance >= bestDistance)
				continue;

			bestDistance = distance;
			pickupPosition = candidates[i];
		}

		return bestDistance != int.MaxValue;
	}

	private static bool IsAdjacent(in int3 first, in int3 second)
	{
		return math.abs(first.x - second.x) +
			math.abs(first.y - second.y) +
			math.abs(first.z - second.z) <= 1;
	}

	private void HandlePayloadBoxInvalidated(BoxBase box)
	{
		if (box != payloadBox)
			return;

		if (GameContext.HasInstance)
			GameContext.Instance.TaskMgr.InvalidateTask(this);
	}

	private void ClearPayloadBox()
	{
		if (payloadBox != null)
			payloadBox.OnInvalidated -= HandlePayloadBoxInvalidated;

		payloadBox = null;
		hasPendingPayloadRecovery = false;
		payloadRecoveryPosition = default;
	}
}
