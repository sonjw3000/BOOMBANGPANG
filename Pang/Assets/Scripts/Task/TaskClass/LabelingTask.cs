using Unity.Mathematics;
using UnityEngine;
using static IBaseNode;
using static IBaseNode.NodeState;

public sealed partial class LabelingTask : WorkerTask
{
	private readonly uint buildingId;
	private readonly CapsuleBuffer targetBuffer;
	private readonly IGridPlaceable targetPlaceable;
	private bool isTaskEnd;
	private int rejectedQuantity;

	private static WorkerManager WorkerManager => GameContext.Instance.WorkerMgr;

	public IItemContainer TargetContainer => targetBuffer;
	public CapsuleBuffer TargetBuffer => targetBuffer;
	public IGridPlaceable TargetPlaceable => targetPlaceable;
	public uint BuildingId => buildingId;
	internal bool IsTaskEnd => isTaskEnd;

	public LabelingTask(uint buildingId, CapsuleBuffer targetBuffer) : base(TaskType.Labeling)
	{
		this.buildingId = buildingId;
		this.targetBuffer = targetBuffer;
		targetPlaceable = targetBuffer;
	}

	public override bool TryGetPreferredWorker(out AIWorker worker)
	{
		worker = null;
		if (buildingId == 0 || WorkerManager == null)
			return false;

		int bestDistance = int.MaxValue;
		foreach (AIWorker candidate in WorkerManager.Workers)
		{
			if (candidate == null ||
				candidate.PrimaryBuildingId != buildingId ||
				candidate.CanAcceptPreferredTask(this) == false)
			{
				continue;
			}

			int distance = math.abs(candidate.GridPosition.x - GetReferencePosition().x) +
				math.abs(candidate.GridPosition.z - GetReferencePosition().z);
			if (distance >= bestDistance)
				continue;

			bestDistance = distance;
			worker = candidate;
		}

		return worker != null;
	}

	protected override IBaseNode BuildWorkNode()
	{
		SequenceNode root = new();
		root.Add(AIWorker.MoveToTarget(WorkerStatusTarget.CapsuleBuffer, InteractionKind.Pick, SetLabelingTarget));
		root.Add(AIWorker.BuildWorkTimeInteract(WorkActionType.LabelItem, ApplyLabel));
		return root;
	}

	public override bool CheckTaskEnd()
	{
		if (isTaskEnd)
			return true;

		InboundWorkflowService inbound = GameContext.HasInstance ? GameContext.Instance.IBWorkflowSvc : null;
		if (inbound == null || inbound.IsLabelingTargetReady(buildingId, targetBuffer) == false)
		{
			isTaskEnd = true;
			return true;
		}

		return isTaskEnd;
	}

	public override bool CanDispatchTo(AIWorker worker)
	{
		return worker != null &&
			buildingId != 0 &&
			worker.PrimaryBuildingId == buildingId &&
			CanDispatchToWorkerZones(worker, targetPlaceable);
	}

	public override bool DependsOnFacility(IFacility facility)
	{
		return ReferenceEquals(targetPlaceable, facility);
	}

#if UNITY_EDITOR
	public override string ShowStatus()
	{
		return $"[LabelingTask] {TargetName}";
	}
#endif

	public override string GetStatusSummary()
	{
		return isTaskEnd
			? $"Target: {TargetName}\nLabeling complete. Rejected: {rejectedQuantity}."
			: $"Target: {TargetName}\nApplying labels.";
	}

	private string TargetName => targetPlaceable is Component component ? component.name : "None";

	private int3 GetReferencePosition()
	{
		if (targetPlaceable != null)
			return targetPlaceable.GridPosition;

		return OccupyWorker != null ? OccupyWorker.GridPosition : default;
	}

	private static LabelingTask GetTask(in BTContext ctx) => ctx.Worker.CurrentTask as LabelingTask;

	public static NodeState SetLabelingTarget(in BTContext ctx)
	{
		LabelingTask task = GetTask(ctx);
		InboundWorkflowService inbound = GameContext.HasInstance ? GameContext.Instance.IBWorkflowSvc : null;
		if (task?.targetPlaceable == null || inbound == null)
			return Failure;

		if (inbound.IsLabelingTargetReady(task.buildingId, task.targetBuffer) == false)
			return Failure;

		ctx.LocalBlackBoard.SetTargetBuilding(task.targetPlaceable);
		return Success;
	}

	public static NodeState ApplyLabel(in BTContext ctx)
	{
		LabelingTask task = GetTask(ctx);
		if (task?.targetBuffer == null || GameContext.HasInstance == false)
			return Failure;

		if (task.TryApplyLabels(out int labeledQuantity, out int rejectedQuantity) == false)
			return Failure;

		task.isTaskEnd = true;
		task.rejectedQuantity = rejectedQuantity;
		BuildingManager buildingManager = GameContext.Instance.BuildingMgr;
		if (buildingManager != null)
			buildingManager.RefreshItemContainerState(task.targetBuffer);
		else
			GameContext.Instance.ExistingCapsuleRelocateCoordinator?.MarkDirty(task.targetBuffer);

		if (labeledQuantity <= 0 && rejectedQuantity <= 0)
			Debug.LogWarning($"[LabelingTask] Completed without labeled items. target={task.TargetName}");

		return Success;
	}

	private bool TryApplyLabels(out int labeledQuantity, out int rejectedQuantity)
	{
		labeledQuantity = 0;
		rejectedQuantity = 0;
		InboundWorkflowService inbound = GameContext.HasInstance ? GameContext.Instance.IBWorkflowSvc : null;
		if (inbound == null || inbound.IsLabelingTargetReady(buildingId, targetBuffer) == false)
			return false;

		bool qualityControlEnabled = inbound.InboundQualityControlEnabled;
		for (int i = 0; i < targetBuffer.Stacks.Count; ++i)
		{
			ItemStack stack = targetBuffer.Stacks[i];
			if (stack == null ||
				stack.Quantity <= 0 ||
				stack.Status != ItemStatus.None ||
				stack.HasQuality(ItemQuality.Waste))
			{
				continue;
			}

			if (qualityControlEnabled && inbound.InspectInboundQuality(stack).Accepted == false)
			{
				stack.AddQuality(ItemQuality.Waste);
				rejectedQuantity += stack.Quantity;
				continue;
			}

			stack.SetStatus(ItemStatus.Labeled);
			labeledQuantity += stack.Quantity;
		}

		return labeledQuantity > 0 || rejectedQuantity > 0;
	}

	public void RestoreState(bool isTaskEnd)
	{
		this.isTaskEnd = isTaskEnd;
	}
}
