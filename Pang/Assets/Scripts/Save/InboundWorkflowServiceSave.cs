using UnityEngine;

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
			QualityControlEnabled = InboundQualityControlEnabled,
			MinimumFreshnessPercent = minimumInboundFreshnessPercent,
			MaximumDamagePercent = maximumInboundDamagePercent,
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
		minimumInboundFreshnessPercent = data != null
			? Mathf.Clamp(data.MinimumFreshnessPercent, 0.0f, 100.0f)
			: QualityControlPolicy.DefaultMinimumFreshnessPercent;
		maximumInboundDamagePercent = data != null
			? Mathf.Clamp(data.MaximumDamagePercent, 0.0f, 100.0f)
			: QualityControlPolicy.DefaultMaximumDamagePercent;
		inboundQualityControlEnabled = IsResearchCompleted(ResearchIds.QualityControl) && data?.QualityControlEnabled == true;
		ReevaluateLabelingWork();
	}

	public void ResetRuntimeState()
	{
		timeSinceLastInboundRocketSpawn = 0.0f;
		requestService?.ResetRuntimeState();
		labelingTasksByBuffer.Clear();
		storingScheduleBuildingIds.Clear();
		RebuildPlanner();
		SyncBuildingTaskProducers();
	}
}
