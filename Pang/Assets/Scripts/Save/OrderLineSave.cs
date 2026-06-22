public partial class OrderLine
{
	public void RestoreState(
		int saveId,
		OrderStatus status,
		int startWeek,
		int dueWeek,
		int baseReward,
		int delayPenalty,
		float reputationChange,
		int pickingAllocatedQuantity,
		int pickingCompletedQuantity,
		int packagingCompletedQuantity,
		int waitingForShippingQuantity,
		int shippingQuantity,
		int inDeliveryQuantity,
		int completedQuantity)
	{
		SaveId = saveId;
		StartWeek = startWeek;
		DueWeek = dueWeek;
		BaseReward = baseReward;
		DelayPenalty = delayPenalty;
		ReputationChange = reputationChange;

		isCancelled = status == OrderStatus.Cancelled;

		if (IsLegacyProgressEmpty(
			pickingAllocatedQuantity,
			pickingCompletedQuantity,
			packagingCompletedQuantity,
			waitingForShippingQuantity,
			shippingQuantity,
			inDeliveryQuantity,
			completedQuantity))
		{
			RestoreLegacyProgress(status);
		}
		else
		{
			PickingAllocatedQuantity = pickingAllocatedQuantity;
			PickingCompletedQuantity = pickingCompletedQuantity;
			PackagingCompletedQuantity = packagingCompletedQuantity;
			WaitingForShippingQuantity = waitingForShippingQuantity;
			ShippingQuantity = shippingQuantity;
			InDeliveryQuantity = inDeliveryQuantity;
			CompletedQuantity = completedQuantity;
			ClampProgress();
		}
	}
}
