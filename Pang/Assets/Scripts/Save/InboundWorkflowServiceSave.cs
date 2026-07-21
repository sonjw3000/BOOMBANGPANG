public partial class InboundWorkflowService
{
	public InboundWorkflowPolicySaveData CapturePolicyState()
	{
		return new InboundWorkflowPolicySaveData
		{
			StoringCollectingPolicy = StoringCollectingPolicyType,
			StoringPlacingPolicy = StoringPlacingPolicyType,
			StoringBoxFillLimitPercent = storingBoxFillLimitPercent,
			UnloadingDestinationBuildingId = unloadingDestinationBuildingId,
		};
	}

	public void RestorePolicyState(InboundWorkflowPolicySaveData data)
	{
		CollectingPolicyType collectingPolicyType = data != null ? data.StoringCollectingPolicy : DefaultCollectingPolicyType;
		PlacingPolicyType placingPolicyType = data != null ? data.StoringPlacingPolicy : DefaultPlacingPolicyType;
		if (collectingPolicyType != DefaultCollectingPolicyType && CanUseStoringCollectingPolicy(collectingPolicyType) == false)
			collectingPolicyType = DefaultCollectingPolicyType;
		if (placingPolicyType != DefaultPlacingPolicyType && CanUseStoringPlacingPolicy(placingPolicyType) == false)
			placingPolicyType = DefaultPlacingPolicyType;
		unloadingDestinationBuildingId = data != null ? data.UnloadingDestinationBuildingId : 0;
		SetStoringCollectingPolicy(collectingPolicyType);
		SetStoringPlacingPolicy(placingPolicyType);
		float boxFillLimit = IsResearchCompleted(ResearchIds.WorkflowPolicyOptimization) &&
			data != null && data.StoringBoxFillLimitPercent > 0.0f
			? data.StoringBoxFillLimitPercent
			: 80.0f;
		SetStoringBoxFillLimitPercent(boxFillLimit);
	}

	public void ResetRuntimeState()
	{
		timeSinceLastInboundRocketSpawn = 0.0f;
		requestService?.ResetRuntimeState();
		RebuildPlanner();
	}
}
