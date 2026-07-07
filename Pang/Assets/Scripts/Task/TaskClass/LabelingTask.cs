using Unity.Mathematics;
using UnityEngine;
using static IBaseNode;
using static IBaseNode.NodeState;

public sealed partial class LabelingTask : WorkerTask
{
	private readonly StagingBuilding building;
	private readonly IItemContainer targetContainer;
	private readonly IGridPlaceable targetPlaceable;
	private bool isTaskEnd;

	private static WorkerManager WorkerManager => GameContext.Instance.WorkerMgr;

	public StagingBuilding Building => building;
	public IItemContainer TargetContainer => targetContainer;
	public IGridPlaceable TargetPlaceable => targetPlaceable;
	public uint BuildingId => building != null ? building.RuntimeBuildingId : 0;

	public LabelingTask(StagingBuilding building, IItemContainer targetContainer) : base(TaskType.Labeling)
	{
		this.building = building;
		this.targetContainer = targetContainer;
		targetPlaceable = targetContainer as IGridPlaceable;
		building?.OnLabelingTaskQueued(targetContainer);
	}

	public override bool TryGetPreferredWorker(out AIWorker worker)
	{
		worker = null;
		if (building == null || WorkerManager == null)
			return false;

		int bestDistance = int.MaxValue;
		foreach (AIWorker candidate in WorkerManager.Workers)
		{
			if (candidate == null ||
				candidate.PrimaryBuildingId != building.RuntimeBuildingId ||
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
		root.Add(AIWorker.MoveToTarget(WorkerStatusTarget.CapsuleBuffer, InteractionKind.Work, SetLabelingTarget));
		root.Add(AIWorker.BuildWorkTimeInteract(WorkActionType.LabelItem, ApplyLabel));
		return root;
	}

	public override bool CheckTaskEnd()
	{
		if (isTaskEnd)
			return true;

		if (building == null || building.HasLabelingWork(targetContainer) == false)
		{
			isTaskEnd = true;
			building?.OnLabelingTaskFinished(targetContainer);
			return true;
		}

		return isTaskEnd;
	}

	public override bool CanDispatchTo(AIWorker worker)
	{
		return worker != null &&
			building != null &&
			worker.PrimaryBuildingId == building.RuntimeBuildingId &&
			CanDispatchToWorkerZones(worker, targetPlaceable);
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
			? $"Target: {TargetName}\nLabeling complete."
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
		if (task?.targetPlaceable == null || task.building == null)
			return Failure;

		if (task.building.HasLabelingWork(task.targetContainer) == false)
			return Failure;

		ctx.LocalBlackBoard.SetTargetBuilding(task.targetPlaceable);
		return Success;
	}

	public static NodeState ApplyLabel(in BTContext ctx)
	{
		LabelingTask task = GetTask(ctx);
		if (task?.building == null)
			return Failure;

		if (task.building.TryLabelItems(task.targetContainer, out int labeledQuantity) == false)
			return Failure;

		task.isTaskEnd = true;
		task.building.OnLabelingTaskFinished(task.targetContainer);

		if (labeledQuantity <= 0)
			Debug.LogWarning($"[LabelingTask] Completed without labeled items. target={task.TargetName}");

		return Success;
	}

	public void RestoreState(bool isTaskEnd)
	{
		this.isTaskEnd = isTaskEnd;
	}
}
