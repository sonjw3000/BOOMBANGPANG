public partial class OutboundWorkflowService
{
	public OutboundWorkflowPolicySaveData CapturePolicyState()
	{
		return new OutboundWorkflowPolicySaveData
		{
			PickingCollectingPolicy = PickingCollectingPolicyType,
		};
	}

	public void RestorePolicyState(OutboundWorkflowPolicySaveData data)
	{
		CollectingPolicyType policyType = data != null ? data.PickingCollectingPolicy : DefaultCollectingPolicyType;
		SetPickingCollectingPolicy(policyType);
	}

	public void ResetRuntimeState()
	{
		timeSinceLastOrder = 0.0f;
		queuedCargoTransferPorts.Clear();
		pendingCargoTransferPorts.Clear();
		queuedCargoTransferTargets.Clear();
		RebuildPlanner();
	}
}
