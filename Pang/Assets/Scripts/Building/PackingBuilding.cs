public sealed class PackingBuilding : Building
{
	public PackingBuilding(string displayName, System.Collections.Generic.List<GridCell> occupiedCells)
		: base(displayName, occupiedCells, BuildingType.Packing)
	{
	}

	protected override bool IsBufferOutboundReady(CapsuleBuffer capsuleBuffer)
	{
		if (capsuleBuffer == null ||
			capsuleBuffer.BufferState != CapsuleBufferState.OBOnly ||
			capsuleBuffer.DockedCapsule == null ||
			(capsuleBuffer.DockedCapsule.LogisticsState != CapsuleLogisticsState.OBStandby &&
			 capsuleBuffer.DockedCapsule.LogisticsState != CapsuleLogisticsState.OB))
		{
			return false;
		}

        // todo
        // have to check items are fully packed first

		float workflowThreshold = GameContext.HasInstance && GameContext.Instance.OBWorkflowSvc != null
			? GameContext.Instance.OBWorkflowSvc.CargoPortThresholdPercent
			: CapsuleThresholdPercent;
		float threshold = OverrideCapsuleThreshold ? CapsuleThresholdPercent : workflowThreshold;
		return capsuleBuffer.FilledPercent >= threshold;
	}

	protected override void OnCapsuleBufferUndocked(CapsuleBuffer capsuleBuffer)
	{
		base.OnCapsuleBufferUndocked(capsuleBuffer);
		if (GameContext.HasInstance)
			GameContext.Instance.TaskMgr?.CancelTaskBuildRequest(WaterTaskBuildRequest.GetRequestKey(capsuleBuffer));
	}

	protected override void TryEvaluatePackingIngress(CapsuleBuffer capsuleBuffer)
	{
		TaskManager taskManager = GameContext.HasInstance ? GameContext.Instance.TaskMgr : null;
		if (taskManager == null || capsuleBuffer == null)
			return;

		if (CanBuildWaterTaskRequest(capsuleBuffer) == false)
		{
			taskManager.CancelTaskBuildRequest(WaterTaskBuildRequest.GetRequestKey(capsuleBuffer));
			return;
		}

		taskManager.EnqueueTaskBuildRequest(new WaterTaskBuildRequest(capsuleBuffer, RuntimeBuildingId));
	}

	internal override bool CanBuildWaterTaskRequest(CapsuleBuffer capsuleBuffer)
	{
		return capsuleBuffer != null &&
			capsuleBuffer.CanProvideInboundItems() &&
			GameContext.HasInstance &&
			GameContext.Instance.OBWorkflowSvc != null &&
			GameContext.Instance.OBWorkflowSvc.HasPackableManifest(capsuleBuffer.DockedCapsule);
	}

	internal override bool CanBuildWaterTaskRequest(PackingStation packingStation)
	{
		return packingStation != null && packingStation.EndPackingBox != null;
	}
}
