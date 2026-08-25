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
		return base.IsBufferOutboundReady(capsuleBuffer);
	}

	protected override void OnIBDockDocked(CapsuleDock dock, CargoCapsule capsule)
	{
		base.OnIBDockDocked(dock, capsule);

		if (dock is CapsuleBuffer capsuleBuffer)
			TryRequestLabelingTask(capsuleBuffer);

	}

	internal bool HasLabelingWork(IItemContainer container)
	{
		if (container == null)
			return false;

		for (int i = 0; i < container.Stacks.Count; ++i)
		{
			ItemStack stack = container.Stacks[i];
			if (stack == null || stack.Quantity <= 0 || stack.Status != ItemStatus.None)
				continue;

			if (stack.HasQuality(ItemQuality.Waste))
				continue;

			return true;
		}

		return false;
	}

	internal void EvaluateLabelingWork()
	{
		for (int i = 0; i < OccupiedCapsuleBuffers.Count; ++i)
			TryRequestLabelingTask(OccupiedCapsuleBuffers[i]);
	}

	internal bool HasQueuedLabelingTask(IItemContainer container)
	{
		return container != null && queuedLabelingContainers.Contains(container);
	}

	internal bool CanRequestLabelingTask(CapsuleBuffer capsuleBuffer)
	{
		return TaskManager != null &&
			IsLabelingSourceBuffer(capsuleBuffer) &&
			IsCapsuleBufferTaskRuleSettled(capsuleBuffer) &&
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

	internal bool TryLabelItems(IItemContainer container, out int labeledQuantity, out int rejectedQuantity)
	{
		labeledQuantity = 0;
		rejectedQuantity = 0;
		if (container == null)
			return false;

		InboundWorkflowService inbound = GameContext.HasInstance ? GameContext.Instance.IBWorkflowSvc : null;
		bool qualityControlEnabled = inbound?.InboundQualityControlEnabled == true;

		for (int i = 0; i < container.Stacks.Count; ++i)
		{
			ItemStack stack = container.Stacks[i];
			if (stack == null || stack.Quantity <= 0 || stack.Status != ItemStatus.None)
				continue;

			// Waste is owned by the waste flow, even while inbound QC is disabled.
			if (stack.HasQuality(ItemQuality.Waste))
				continue;

			if (qualityControlEnabled)
			{
				QualityInspectionResult inspection = inbound.InspectInboundQuality(stack);
				if (inspection.Accepted == false)
				{
					stack.AddQuality(ItemQuality.Waste);
					rejectedQuantity += stack.Quantity;
					continue;
				}
			}

			stack.SetStatus(ItemStatus.Labeled);
			labeledQuantity += stack.Quantity;
		}

		if (labeledQuantity <= 0 && rejectedQuantity <= 0)
			return false;

		ItemIndex.RefreshContainer(container);
		ClearItemContainerDirty(container);
		if (container is CapsuleBuffer capsuleBuffer)
			MarkDockRoutingDirty(capsuleBuffer);

		return true;
	}

	protected override void OnCapsuleBufferContentChanged(CapsuleBuffer capsuleBuffer)
	{
		base.OnCapsuleBufferContentChanged(capsuleBuffer);
		if (capsuleBuffer == null)
			return;

		if (HasLabelingWork(capsuleBuffer))
			TryRequestLabelingTask(capsuleBuffer);

	}

	protected override void OnCapsuleRoutingSettled(CapsuleBuffer capsuleBuffer)
	{
		TryRequestLabelingTask(capsuleBuffer);
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
			capsuleBuffer.DockedCapsule?.LogisticsState == CapsuleLogisticsState.Inside &&
			capsuleBuffer.IsCapsuleEmpty() == false;
	}

}
