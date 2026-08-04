using System;
using System.Collections.Generic;
using UnityEngine;

public partial class OutboundWorkflowService
{
	public OutboundWorkflowPolicySaveData CapturePolicyState()
	{
		return new OutboundWorkflowPolicySaveData
		{
			PickingPolicy = PickingPolicyType,
			PickingCollectingPolicy = PickingCollectingPolicyType,
			PickingBoxFillLimitPercent = pickingBoxFillLimitPercent,
			LoadingDestinationBuildingId = loadingDestinationBuildingId,
			QualityControlEnabled = OutboundQualityControlEnabled,
			MinimumFreshnessPercent = minimumOutboundFreshnessPercent,
			MaximumDamagePercent = maximumOutboundDamagePercent,
		};
	}

	public void RestorePolicyState(OutboundWorkflowPolicySaveData data)
	{
		PickingPolicyType pickingPolicyType = data != null ? data.PickingPolicy : DefaultPickingPolicyType;
		CollectingPolicyType collectingPolicyType = data != null ? data.PickingCollectingPolicy : DefaultCollectingPolicyType;
		if (pickingPolicyType != DefaultPickingPolicyType && CanUsePickingPolicy(pickingPolicyType) == false)
			pickingPolicyType = DefaultPickingPolicyType;
		if (collectingPolicyType != DefaultCollectingPolicyType && CanUsePickingCollectingPolicy(collectingPolicyType) == false)
			collectingPolicyType = DefaultCollectingPolicyType;
		loadingDestinationBuildingId = data != null ? data.LoadingDestinationBuildingId : 0;
		SetPickingPolicy(pickingPolicyType);
		SetPickingCollectingPolicy(collectingPolicyType);
		float boxFillLimit = IsResearchCompleted(ResearchIds.WorkflowPolicyOptimization) &&
			data != null && data.PickingBoxFillLimitPercent > 0.0f
			? data.PickingBoxFillLimitPercent
			: 80.0f;
		SetPickingBoxFillLimitPercent(boxFillLimit);
		minimumOutboundFreshnessPercent = data != null
			? Mathf.Clamp(data.MinimumFreshnessPercent, 0.0f, 100.0f)
			: QualityControlPolicy.DefaultMinimumFreshnessPercent;
		maximumOutboundDamagePercent = data != null
			? Mathf.Clamp(data.MaximumDamagePercent, 0.0f, 100.0f)
			: QualityControlPolicy.DefaultMaximumDamagePercent;
		outboundQualityControlEnabled = IsResearchCompleted(ResearchIds.QualityControl) && data?.QualityControlEnabled == true;
		EvaluateLaunchSortWork();
	}

	public OutboundPickingManifestSaveData CapturePickingManifestState(Func<OrderLine, int> registerOrderLine)
	{
		OutboundPickingManifestSaveData data = new();
		foreach (var manifestEntry in pickingManifests)
		{
			PickingManifest manifest = manifestEntry.Value;
			if (manifest == null || manifest.IsEmpty)
				continue;

			PickingManifestSaveData manifestData = new()
			{
				BoxType = manifestEntry.Key.BoxType,
				BoxId = manifestEntry.Key.BoxId,
			};

			foreach (PickingManifestLine line in manifest.Lines)
			{
				if (line == null || line.PickedQuantity <= 0)
					continue;

				manifestData.Lines.Add(new PickingManifestLineSaveData
				{
					OrderLineId = registerOrderLine != null && line.OrderLine != null ? registerOrderLine(line.OrderLine) : -1,
					ItemId = line.ItemId,
					PickedQuantity = line.PickedQuantity,
					PackedQuantity = line.PackedQuantity,
					OutboundStage = line.OutboundStage,
				});
			}

			if (manifestData.Lines.Count > 0)
				data.Manifests.Add(manifestData);
		}

		return data;
	}

	public void RestorePickingManifestState(OutboundPickingManifestSaveData data, IReadOnlyDictionary<int, OrderLine> restoredOrderLines)
	{
		pickingManifests.Clear();
		if (data?.Manifests == null)
			return;

		foreach (PickingManifestSaveData manifestData in data.Manifests)
		{
			if (manifestData == null || manifestData.Lines == null)
				continue;

			PickingManifestKey key = new(manifestData.BoxType, manifestData.BoxId);
			if (key.IsValid == false)
			{
				Debug.LogWarning($"[Save] Skipped picking manifest with invalid owner key {key}.");
				continue;
			}

			PickingManifest manifest = GetPickingManifest(key);
			if (manifest == null)
				continue;

			foreach (PickingManifestLineSaveData lineData in manifestData.Lines)
			{
				if (lineData == null || lineData.PickedQuantity <= 0)
					continue;

				OrderLine orderLine = null;
				restoredOrderLines?.TryGetValue(lineData.OrderLineId, out orderLine);
				manifest.AddRestoredLine(orderLine, lineData.ItemId, lineData.PickedQuantity, lineData.PackedQuantity, lineData.OutboundStage);
			}

			if (manifest.IsEmpty)
				ClearPickingManifest(key);
		}
	}

	public void ResetRuntimeState()
	{
		timeSinceLastOrder = 0.0f;
		queuedCargoTransferPorts.Clear();
		queuedCargoTransferTargets.Clear();
		pickingManifests.Clear();
	}
}
