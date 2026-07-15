public sealed class StagingBuilding : Building
{
	private readonly System.Collections.Generic.HashSet<IItemContainer> queuedLabelingContainers = new();

	public StagingBuilding(string displayName, System.Collections.Generic.List<GridCell> occupiedCells)
		: base(displayName, occupiedCells, BuildingType.Staging)
	{
		trackingItemStatus.Add(ItemStatus.None);
	}

	protected override bool IsBufferOutboundReady(CapsuleBuffer capsuleBuffer)
	{
		return capsuleBuffer != null &&
			capsuleBuffer.HasCapsule &&
			HasOnlyLabeledItems(capsuleBuffer) &&
			capsuleBuffer.CanDispatchToOutbound();
	}

	protected override void OnIBDockDocked(CapsuleDock dock, CargoCapsule capsule)
	{
		if (dock is CapsuleBuffer capsuleBuffer)
			TryRequestLabelingTask(capsuleBuffer);

		TryPromoteToOutboundState(dock as CapsuleBuffer);
	}

	internal bool HasLabelingWork(IItemContainer container)
	{
		return container != null && HasAvailableItemStatus(container, ItemStatus.None);
	}

	internal bool HasQueuedLabelingTask(IItemContainer container)
	{
		return container != null && queuedLabelingContainers.Contains(container);
	}

	internal bool CanRequestLabelingTask(CapsuleBuffer capsuleBuffer)
	{
		return TaskManager != null &&
			IsLabelingSourceBuffer(capsuleBuffer) &&
			HasQueuedLabelingTask(capsuleBuffer) == false &&
			HasLabelingWork(capsuleBuffer);
	}

	internal void OnLabelingTaskQueued(IItemContainer container)
	{
		if (container != null)
			queuedLabelingContainers.Add(container);
	}

	internal void OnLabelingTaskFinished(IItemContainer container)
	{
		if (container == null)
			return;

		queuedLabelingContainers.Remove(container);
		if (container is CapsuleBuffer capsuleBuffer && HasLabelingWork(capsuleBuffer))
			TryRequestLabelingTask(capsuleBuffer);
	}

	internal void OnLabelingTaskInvalidated(IItemContainer container)
	{
		if (container != null)
			queuedLabelingContainers.Remove(container);
	}

	internal bool TryLabelItems(IItemContainer container, out int labeledQuantity)
	{
		labeledQuantity = 0;
		if (container == null)
			return false;

		for (int i = 0; i < container.Stacks.Count; ++i)
		{
			ItemStack stack = container.Stacks[i];
			if (stack == null || stack.Quantity <= 0 || stack.Status != ItemStatus.None)
				continue;

			stack.SetStatus(ItemStatus.Labeled);
			labeledQuantity += stack.Quantity;
		}

		if (labeledQuantity <= 0)
			return false;

		ItemIndex.RefreshContainer(container);
		ClearItemContainerDirty(container);

		if (container is CapsuleBuffer capsuleBuffer)
			TryPromoteToOutboundState(capsuleBuffer);

		return true;
	}

	private bool TryRequestLabelingTask(CapsuleBuffer capsuleBuffer)
	{
		if (CanRequestLabelingTask(capsuleBuffer) == false)
			return false;

		return TaskManager.EnqueueTaskBuildRequest(new LabelingTaskBuildRequest(this, capsuleBuffer));
	}

	private static bool IsLabelingSourceBuffer(CapsuleBuffer capsuleBuffer)
	{
		return capsuleBuffer != null &&
			capsuleBuffer.DockState == CapsuleDockState.IB &&
			capsuleBuffer.HasCapsule;
	}

	private static void TryPromoteToOutboundState(CapsuleBuffer capsuleBuffer)
	{
		if (capsuleBuffer == null ||
			capsuleBuffer.HasCapsule == false ||
			capsuleBuffer.IsCapsuleEmpty() ||
			capsuleBuffer.DockedCapsule == null ||
			HasOnlyLabeledItems(capsuleBuffer) == false ||
			capsuleBuffer.DockedCapsule.LogisticsState == CapsuleLogisticsState.OB)
		{
			return;
		}

		capsuleBuffer.DockedCapsule.SetLogisticsState(CapsuleLogisticsState.OB);
	}

	private static bool HasOnlyLabeledItems(CapsuleBuffer capsuleBuffer)
	{
		if (capsuleBuffer == null || capsuleBuffer.Stacks.Count <= 0)
			return false;

		for (int i = 0; i < capsuleBuffer.Stacks.Count; ++i)
		{
			ItemStack stack = capsuleBuffer.Stacks[i];
			if (stack == null || stack.Quantity <= 0)
				continue;

			if (stack.Status != ItemStatus.Labeled)
				return false;
		}

		return true;
	}
}
