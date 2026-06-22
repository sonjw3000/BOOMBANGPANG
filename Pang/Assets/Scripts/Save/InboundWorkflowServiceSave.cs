public partial class InboundWorkflowService
{
	public InboundWorkflowPolicySaveData CapturePolicyState()
	{
		return new InboundWorkflowPolicySaveData
		{
			StoringCollectingPolicy = StoringCollectingPolicyType,
			StoringPlacingPolicy = StoringPlacingPolicyType,
			UnloadingDestinationBuildingId = unloadingDestinationBuildingId,
		};
	}

	public void RestorePolicyState(InboundWorkflowPolicySaveData data)
	{
		CollectingPolicyType collectingPolicyType = data != null ? data.StoringCollectingPolicy : DefaultCollectingPolicyType;
		PlacingPolicyType policyType = data != null ? data.StoringPlacingPolicy : DefaultPlacingPolicyType;
		unloadingDestinationBuildingId = data != null ? data.UnloadingDestinationBuildingId : 0;
		SetStoringCollectingPolicy(collectingPolicyType);
		SetStoringPlacingPolicy(policyType);
	}

	public void ResetRuntimeState()
	{
		timeSinceLastInboundRocketSpawn = 0.0f;
		requestService?.ResetRuntimeState();
		RebuildPlanner();
	}
}
