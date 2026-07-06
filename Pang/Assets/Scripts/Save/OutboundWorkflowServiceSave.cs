using System;
using System.Collections.Generic;

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
