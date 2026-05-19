using System.Collections.Generic;
using UnityEngine;

public sealed class PickingPlanner
{
	private static int jobID = 1;

	private readonly CollectPlanner<OrderLine> collectPlanner;
	private readonly ICollectRequestSource<OrderLine> collectRequestSource;
	private CollectingPolicyType collectingPolicyType;
	private float boxFillLimitPercent;

	public static int GetNextJobId() => jobID;
	public static void SetNextJobId(int nextJobId) => jobID = nextJobId;
	public CollectingPolicyType CollectingPolicyType => collectingPolicyType;

	public PickingPlanner(
		ICollectSupplySource collectSupplySource,
		ICollectRequestSource<OrderLine> collectRequestSource,
		float boxFillLimitPercent,
		CollectingPolicyType collectingPolicyType = CollectingPolicyType.Nearest)
	{
		this.collectRequestSource = collectRequestSource;
		this.boxFillLimitPercent = boxFillLimitPercent;
		collectPlanner = new CollectPlanner<OrderLine>(
			collectSupplySource,
			collectRequestSource,
			CollectingPolicyFactory.Create<OrderLine>(collectingPolicyType));
		SetCollectingPolicy(collectingPolicyType);
	}

	public void SetCollectingPolicy(CollectingPolicyType policyType)
	{
		collectingPolicyType = policyType;
		collectPlanner.SetCollectingPolicy(CollectingPolicyFactory.Create<OrderLine>(policyType));
	}

	public void SetBoxFillLimitPercent(float value)
	{
		boxFillLimitPercent = value;
	}

	public bool HasPendingCollectWork()
	{
		foreach (uint _ in collectRequestSource.GetRequestedItemIds())
			return true;

		return false;
	}

	public bool BuildPickingTask(out PickingTask task)
	{
		task = null;
		if (HasPendingCollectWork() == false)
			return false;

		WorkJob job = new(jobID++, new List<WorkLine>(), WorkOp.Picking);
		task = new PickingTask(job);
		return true;
	}

	public bool TryAllocateNextCollectLine(AIWorker worker, out WorkLine line)
	{
		line = null;
		if (worker == null)
			return false;

		BoxBase box = worker.CarryingAbility?.CarryingBox;
		if (box == null)
			return false;

		if (HasReachedBoxFillLimit(box))
			return false;

		return collectPlanner.TryAllocateNextCollectLine(worker, out line);
	}

	private bool HasReachedBoxFillLimit(BoxBase box)
	{
		if (box == null || box.MaxSize <= 0.0f)
			return false;

		float filledPercent = (box.TotalSize / box.MaxSize) * 100.0f;
		return filledPercent >= boxFillLimitPercent;
	}
}
