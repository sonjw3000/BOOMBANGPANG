using System;
using System.Collections.Generic;

public partial class OutboundWorkflowService
{
	public OutboundWorkflowPolicySaveData CapturePolicyState()
	{
		return new OutboundWorkflowPolicySaveData
		{
			PickingPolicy = PickingPolicyType,
			PickingCollectingPolicy = PickingCollectingPolicyType,
			LoadingDestinationBuildingId = loadingDestinationBuildingId,
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
				BoxId = manifestEntry.Key,
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
			if (manifestData == null || manifestData.BoxId == 0 || manifestData.Lines == null)
				continue;

			PickingManifest manifest = GetPickingManifest(manifestData.BoxId);
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
				ClearPickingManifest(manifestData.BoxId);
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
