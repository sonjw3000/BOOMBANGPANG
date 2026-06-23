using System;
using System.Collections.Generic;
using UnityEngine;

public partial class PackingStation
{
	public void InitializeForSaveLoad()
	{
		// Facility registration is now owned by GridService -> FacilityManager.
		RefreshWaitingState();
	}

	public PackingStationSaveData CaptureState(Func<OrderLine, int> registerOrderLine, Func<GameObject, int> getPlaceableId)
	{
		return new PackingStationSaveData
		{
			PackedItems = packedItems.ConvertAll(stack => new ItemStackSaveData
			{
				ItemId = stack.ItemID,
				Quantity = stack.Quantity,
				Freshness = stack.Freshness,
				Damage = stack.Damage,
				Status = stack.Status,
				RelatedOrderLineId = registerOrderLine != null && stack.RelatedOrderLine != null ? registerOrderLine(stack.RelatedOrderLine) : -1,
				OutboundStage = stack.OutboundStage,
			}),
			WaitingBox = CaptureBoxWithOrder(waitStackBox, registerOrderLine, getPlaceableId),
			CurrentBox = CaptureBoxWithOrder(currentPackingBox, registerOrderLine, getPlaceableId),
			EndBox = CaptureBoxWithOrder(endPackingBox, registerOrderLine, getPlaceableId),
			CurrentWorkerId = currentPackingWorker != null ? (int)currentPackingWorker.WorkerID : -1,
			IncomingWorkerId = incomingPickingWorker != null ? (int)incomingPickingWorker.WorkerID : -1,
			IncomingRequestSuspended = incomingRequestSuspended,
		};
	}

	public void RestoreState(
		PackingStationSaveData data,
		Dictionary<int, OrderLine> restoredOrderLines,
		Dictionary<int, GameObject> restoredPlaceables)
	{
		for (int i = 0; i < packedItems.Count; ++i)
			packedItems[i]?.Recycle();

		packedItems.Clear();
		itemTotals.Clear();
		totalSize = 0.0f;
		waitStackBox = null;
		currentPackingBox = null;
		endPackingBox = null;
		currentPackingWorker = null;
		incomingPickingWorker = null;
		incomingRequestSuspended = data != null && data.IncomingRequestSuspended;

		if (data == null)
			return;

		foreach (var stackData in data.PackedItems)
		{
			OrderLine line = null;
			if (restoredOrderLines != null && stackData.RelatedOrderLineId >= 0)
				restoredOrderLines.TryGetValue(stackData.RelatedOrderLineId, out line);

			ItemStack stack = ItemStack.Rent(stackData.ItemId, stackData.Freshness, stackData.Damage, stackData.Status, line, stackData.OutboundStage);
			stack.AddItem(stackData.Quantity);
			AddStack(stack);
			if (stack.Quantity <= 0)
				stack.Recycle();
		}

		SetWaitStackBox(RestoreBoxWithOrder(data.WaitingBox, restoredOrderLines, restoredPlaceables));
		SetCurrentPackingBox(RestoreBoxWithOrder(data.CurrentBox, restoredOrderLines, restoredPlaceables));
		SetEndStackBox(RestoreBoxWithOrder(data.EndBox, restoredOrderLines, restoredPlaceables));
	}

	public void RestoreWorkerBindings(Dictionary<uint, AIWorker> workersById, PackingStationSaveData data)
	{
		if (data == null)
			return;

		if (data.CurrentWorkerId >= 0 && workersById.TryGetValue((uint)data.CurrentWorkerId, out var currentWorker))
			CurrentPackingWorker = currentWorker;

		if (data.IncomingWorkerId >= 0 && workersById.TryGetValue((uint)data.IncomingWorkerId, out var incomingWorker))
			incomingPickingWorker = incomingWorker;

		SetIncomingRequestSuspended(data.IncomingRequestSuspended);
	}

	private static BoxWithOrderSaveData CaptureBoxWithOrder(BoxWithOrder value, Func<OrderLine, int> registerOrderLine, Func<GameObject, int> getPlaceableId)
	{
		if (value == null)
			return null;

		return new BoxWithOrderSaveData
		{
			Box = value.Box == null
				? null
				: new BoxReferenceSaveData
				{
					BoxType = value.Box.Type,
					BoxId = value.Box.BoxId,
				},
			Job = value.Job != null ? value.Job.CaptureState(getPlaceableId, registerOrderLine) : null,
		};
	}

	private static BoxWithOrder RestoreBoxWithOrder(
		BoxWithOrderSaveData data,
		Dictionary<int, OrderLine> restoredOrderLines,
		Dictionary<int, GameObject> restoredPlaceables)
	{
		if (data == null || data.Job == null)
			return null;

		if (data.Box == null || GameContext.Instance.BoxMgr.TryGetBox(data.Box.BoxType, data.Box.BoxId, out var box) == false)
			return null;

		WorkJob job = data.Job.Restore(restoredPlaceables, restoredOrderLines);
		return box != null && job != null ? new BoxWithOrder(box, job) : null;
	}
}
