using System.Collections.Generic;

public sealed class StoringPlanner
{
	private static int jobID = 1;

	private readonly CollectPlanner<InboundLine> collectPlanner;
	private readonly ICollectRequestSource<InboundLine> collectRequestSource;
	private CollectingPolicyType collectingPolicyType;
	private PlacingPolicyType placingPolicyType;
	private IPlacingPolicy placingPolicy;

	public static int GetNextJobId() => jobID;
	public static void SetNextJobId(int nextJobId) => jobID = nextJobId;
	public CollectingPolicyType CollectingPolicyType => collectingPolicyType;
	public PlacingPolicyType PlacingPolicyType => placingPolicyType;

	public StoringPlanner(
		ICollectSupplySource collectSupplySource,
		ICollectRequestSource<InboundLine> collectRequestSource,
		CollectingPolicyType collectingPolicyType = CollectingPolicyType.Nearest,
		PlacingPolicyType placingPolicyType = PlacingPolicyType.BelowAverageFilledNearest)
	{
		this.collectRequestSource = collectRequestSource;
		collectPlanner = new CollectPlanner<InboundLine>(
			collectSupplySource,
			collectRequestSource,
			CollectingPolicyFactory.Create<InboundLine>(collectingPolicyType));
		SetCollectingPolicy(collectingPolicyType);
		SetPlacingPolicy(placingPolicyType);
	}

	public void SetCollectingPolicy(CollectingPolicyType policyType)
	{
		collectingPolicyType = policyType;
		collectPlanner.SetCollectingPolicy(CollectingPolicyFactory.Create<InboundLine>(policyType));
	}

	public void SetPlacingPolicy(PlacingPolicyType policyType)
	{
		placingPolicyType = policyType;
		placingPolicy = PlacingPolicyFactory.Create(policyType);
	}

	public bool HasPendingCollectWork()
	{
		foreach (uint _ in collectRequestSource.GetRequestedItemIds())
			return true;

		return false;
	}

	public bool BuildStoreTask(out StoringTask task)
	{
		task = null;
		if (HasPendingCollectWork() == false)
			return false;

		WorkJob job = new(jobID++, new List<WorkLine>(), WorkOp.Storing);
		task = new StoringTask(job);
		return true;
	}

	public bool TryAllocateNextCollectLine(AIWorker worker, out WorkLine line)
	{
		return collectPlanner.TryAllocateNextCollectLine(worker, out line);
	}

	public bool TryDecideNextPlacingLine(AIWorker worker, out WorkLine line)
	{
		line = null;

		if (worker == null || placingPolicy == null)
			return false;

		BoxBase box = worker.CarryingAbility?.CarryingBox;
		if (box == null)
			return false;

		if (placingPolicy.TryDecide(worker.GridPosition, box, null, out var decision) == false)
			return false;

		if (decision.shelf == null || decision.Quantity <= 0)
			return false;

		line = new WorkLine(decision.shelf, decision.ItemID, decision.Quantity);
		return true;
	}
}
