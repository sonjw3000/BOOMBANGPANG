using System.Collections.Generic;

public sealed class PackingBuilding : Building
{
	public PackingBuilding(string displayName, List<GridCell> occupiedCells)
		: base(displayName, occupiedCells, BuildingType.Packing)
	{
		trackingItemStatus.Add(ItemStatus.Labeled);
	}

	protected override bool IsBufferOutboundReady(CapsuleBuffer capsuleBuffer)
	{
		if (capsuleBuffer == null ||
			capsuleBuffer.DockState != CapsuleDockState.OBStandby ||
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

	protected override void OnIBDockDocked(CapsuleDock dock, CargoCapsule capsule)
	{
		base.OnIBDockDocked(dock, capsule);

		if (dock is CapsuleBuffer capsuleBuffer)
			EvaluatePackingIngress(capsuleBuffer);
	}

	private void EvaluatePackingIngress(CapsuleBuffer capsuleBuffer)
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

	internal bool CanBuildWaterTaskRequest(CapsuleBuffer capsuleBuffer)
	{
		return capsuleBuffer != null &&
			capsuleBuffer.CanProvideInboundItems() &&
			GameContext.HasInstance &&
			GameContext.Instance.OBWorkflowSvc != null &&
			GameContext.Instance.OBWorkflowSvc.HasPackableManifest(capsuleBuffer.DockedCapsule);
	}

	internal bool CanBuildWaterTaskRequest(PackingStation packingStation)
	{
		return packingStation != null && packingStation.EndPackingBox != null;
	}
}
