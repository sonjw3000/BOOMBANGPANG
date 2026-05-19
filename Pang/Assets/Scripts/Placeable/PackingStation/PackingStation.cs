using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

public class BoxWithOrder
{
	public BoxBase Box;
	public WorkJob Job;

	public bool IsFullyPacked => Job.IsJobEnd;

	public BoxWithOrder(BoxBase box, WorkJob job)
	{
		Box = box;
		Job = job;
	}
}

public class PackingStation :
	BoxInteraction,
	IItemContainer
{
	[SerializeField] Transform waitStackSlot = null;
	[SerializeField] Transform packingSlot = null;
	[SerializeField] Transform endStackSlot = null;
	[SerializeField] Transform workerSlot = null;

	[SerializeField] protected int maxStacks = 16;
	[SerializeField] protected float sizePerStack = 100;

	private AIWorker currentPackingWorker = null;
	private AIWorker incomingPickingWorker = null;
	private bool incomingRequestSuspended = false;
	private bool isRegistered = false;

	private BoxWithOrder waitStackBox = null;
	private BoxWithOrder currentPackingBox = null;
	private BoxWithOrder endPackingBox = null;

	private readonly List<ItemPackage> packedItems = new();
	protected Dictionary<uint, int> itemTotals = new();
	private float totalSize = 0.0f;

	public IReadOnlyList<ItemStack> Stacks => packedItems;
	public IReadOnlyDictionary<uint, int> ItemTotals => itemTotals;
	public float TotalSize => totalSize;
	public float MaxSize => sizePerStack * maxStacks;
	public bool CanRegister() => maxStacks > Stacks.Count;

	public int GetQuantity(uint itemId)
	{
		return itemTotals.GetValueOrDefault(itemId);
	}

	public int GetAcceptableQuantity(uint itemId, int requested)
	{
		return 0;
	}

	public bool CanAcceptStack(ItemStack stack)
	{
		return stack is ItemPackage && packedItems.Count < maxStacks;
	}

	private PackingStationService PackingStations => GameContext.Instance.OBWorkflowMgr.PackingStations;

	public override WorkerStatusTarget BuildingTarget => WorkerStatusTarget.PackingStation;

	public AIWorker CurrentPackingWorker
	{
		get => currentPackingWorker;
		set
		{
			if (currentPackingWorker == value)
				return;

			if (currentPackingWorker != null)
				currentPackingWorker.OnWorkingPointSet(null);

			currentPackingWorker = value;
			if (currentPackingWorker == null)
			{
				incomingRequestSuspended = false;
			}
			else
			{
				currentPackingWorker.OnWorkingPointSet(this);
			}

			RefreshWaitingState();
		}
	}

	public AIWorker IncomingPickingWorker => incomingPickingWorker;
	public BoxWithOrder CurrentPackingBox => currentPackingBox;
	public BoxWithOrder WaitingBox => waitStackBox;
	public BoxWithOrder EndPackingBox => endPackingBox;
	public bool IncomingRequestSuspended => incomingRequestSuspended;
	public bool HasWaitingBox => waitStackBox != null;
	public bool IsNoWorkerAssigned => currentPackingWorker == null;
	public bool IsBoxMoveableToPack => waitStackBox != null && currentPackingBox == null;
	public bool IsBoxMoveableToEnd => currentPackingBox != null && endPackingBox == null;

	public override bool CanGetBox() => endPackingBox != null;
	public override bool CanPutBox() => waitStackBox == null;

	private void Start()
	{
		InitializeForSaveLoad();
	}

	private void OnDestroy()
	{
		if (isRegistered)
		{
			PackingStations.UnRegister(this);
			isRegistered = false;
		}
	}

	public override void OnPositionSet(in int3 pos, FacingDirection direction)
	{
		enabled = true;
		position = pos;
		facingDirection = direction;
	}

	public override void OnDestroyedBy(in DestroyContext context)
	{
	}

	public override void OnRemoved()
	{
	}

	public bool CanRequestIncomingBox()
	{
		return waitStackBox == null &&
			incomingPickingWorker == null &&
			incomingRequestSuspended == false;
	}

	public void SetIncomingRequestSuspended(bool suspended)
	{
		if (incomingRequestSuspended == suspended)
			return;

		incomingRequestSuspended = suspended;
		RefreshWaitingState();
	}

	public bool CanAssignedWorkerLeaveForRecovery()
	{
		return waitStackBox == null && incomingPickingWorker == null;
	}

	public bool TryReserveIncomingBox(AIWorker picker)
	{
		if (CanRequestIncomingBox() == false)
			return false;

		incomingPickingWorker = picker;
		RefreshWaitingState();
		return true;
	}

	public void ClearIncomingBoxReservation(AIWorker picker = null)
	{
		if (picker != null && incomingPickingWorker != picker)
			return;

		incomingPickingWorker = null;
		RefreshWaitingState();
	}

	public void RefreshWaitingState()
	{
		if (GameContext.HasInstance == false)
			return;

		PackingStations.RefreshWaitingStation(this);
	}

	public bool PrepareBox()
	{
		if (waitStackBox == null)
			return false;

		if (currentPackingBox != null)
		{
			Debug.LogError("Why current packing box is not null???? have to check");
			return false;
		}

		SetCurrentPackingBox(waitStackBox);
		SetWaitStackBox(null);
		RefreshWaitingState();
		return true;
	}

	public bool EndWorkingBox()
	{
		if (endPackingBox != null || currentPackingBox == null)
			return false;

		TransferResultKind result = ItemTransferUtility.MoveAllStacks(new(this, currentPackingBox.Box));
		if (result != TransferResultKind.Complete)
			return false;

		SetEndStackBox(currentPackingBox);
		SetCurrentPackingBox(null);

		PackingStations.OnPackingComplete(this);
		return true;
	}

	public override bool GetBox(out BoxBase box)
	{
		box = null;
		if (endPackingBox == null)
			return false;

		box = endPackingBox.Box;
		SetEndStackBox(null);
		RefreshWaitingState();
		return true;
	}

	public override bool PutBox(BoxBase box)
	{
		Debug.LogError("PackingStation requires PutBoxToPack with order data.");
		return false;
	}

	public bool PutBoxToPack(BoxWithOrder boxToPack)
	{
		if (waitStackBox != null || boxToPack == null)
			return false;

		boxToPack.Job.ResetForPacking();
		ClearIncomingBoxReservation();
		SetWaitStackBox(boxToPack);
		PackingStations.RequestPackingTaskIfNeeded(this);
		return true;
	}

	public int AddItem(uint itemId, int quantity)
	{
		Debug.LogError("Should not add item to packing station");
		return 0;
	}

	public int RemoveItem(uint itemId, int quantity)
	{
		Debug.LogError("Should not remove item to packing station");
		return 0;
	}

	public bool AddStack(ItemStack stack)
	{
		if (CanAcceptStack(stack) == false || stack is not ItemPackage pkg)
			return false;

		packedItems.Add(pkg);
		itemTotals[pkg.ItemID] = itemTotals.GetValueOrDefault(pkg.ItemID) + pkg.Quantity;
		UpdateSize();
		return true;
	}

	public bool RemoveStack(ItemStack stack)
	{
		if (stack is not ItemPackage pkg || packedItems.Remove(pkg) == false)
			return false;

		itemTotals[pkg.ItemID] = itemTotals.GetValueOrDefault(pkg.ItemID) - pkg.Quantity;
		if (itemTotals[pkg.ItemID] <= 0)
			itemTotals.Remove(pkg.ItemID);

		UpdateSize();
		return true;
	}

	private void UpdateSize()
	{
		totalSize = 0.0f;
		for (int i = 0; i < packedItems.Count; ++i)
			totalSize += packedItems[i].Size;
	}

	private void SetCurrentPackingBox(BoxWithOrder value)
	{
		if (value != null)
		{
			value.Box.transform.SetParent(packingSlot);
			value.Box.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
		}

		if (currentPackingBox != null)
			currentPackingBox.Box.transform.SetParent(null);

		currentPackingBox = value;
	}

	private void SetWaitStackBox(BoxWithOrder value)
	{
		if (value != null)
		{
			value.Box.transform.SetParent(waitStackSlot);
			value.Box.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);

			if (currentPackingWorker != null)
				currentPackingWorker.enabled = true;
		}

		if (waitStackBox != null)
			waitStackBox.Box.transform.SetParent(null);

		waitStackBox = value;
	}

	private void SetEndStackBox(BoxWithOrder value)
	{
		if (value == null)
		{
			if (currentPackingWorker != null)
				currentPackingWorker.enabled = true;
		}
		else
		{
			value.Box.transform.SetParent(endStackSlot);
			value.Box.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
		}

		if (endPackingBox != null)
			endPackingBox.Box.transform.SetParent(null);

		endPackingBox = value;
	}

	public void InitializeForSaveLoad()
	{
		if (isRegistered)
			return;

		PackingStations.Register(this);
		isRegistered = true;
	}

	public PackingStationSaveData CaptureState(Func<OrderLine, int> registerOrderLine, Func<GameObject, int> getPlaceableId)
	{
		return new PackingStationSaveData
		{
			PackedItems = packedItems.ConvertAll(pkg => new ItemStackSaveData
			{
				ItemId = pkg.ItemID,
				Quantity = pkg.Quantity,
				IsPackage = true,
				RelatedOrderLineId = registerOrderLine != null ? registerOrderLine(pkg.RelatedOrderLine) : -1,
				OutboundStage = pkg.OutboundStage,
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
		Dictionary<uint, BoxBase> restoredBoxes,
		Dictionary<int, OrderLine> restoredOrderLines,
		Dictionary<int, GameObject> restoredPlaceables)
	{
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

		foreach (var pkgData in data.PackedItems)
		{
			if (restoredOrderLines.TryGetValue(pkgData.RelatedOrderLineId, out var line) == false)
				continue;

			ItemPackage package = new(PackingType.Box, line, pkgData.ItemId, pkgData.Quantity, pkgData.OutboundStage);
			packedItems.Add(package);
			itemTotals[pkgData.ItemId] = itemTotals.GetValueOrDefault(pkgData.ItemId) + pkgData.Quantity;
		}

		SetWaitStackBox(RestoreBoxWithOrder(data.WaitingBox, restoredBoxes, restoredOrderLines, restoredPlaceables));
		SetCurrentPackingBox(RestoreBoxWithOrder(data.CurrentBox, restoredBoxes, restoredOrderLines, restoredPlaceables));
		SetEndStackBox(RestoreBoxWithOrder(data.EndBox, restoredBoxes, restoredOrderLines, restoredPlaceables));
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
			BoxId = value.Box != null ? value.Box.BoxId : 0,
			Job = value.Job != null ? value.Job.CaptureState(getPlaceableId, registerOrderLine) : null,
		};
	}

	private static BoxWithOrder RestoreBoxWithOrder(
		BoxWithOrderSaveData data,
		Dictionary<uint, BoxBase> restoredBoxes,
		Dictionary<int, OrderLine> restoredOrderLines,
		Dictionary<int, GameObject> restoredPlaceables)
	{
		if (data == null || data.Job == null)
			return null;

		if (restoredBoxes.TryGetValue(data.BoxId, out var box) == false)
			return null;

		WorkJob job = data.Job.Restore(restoredPlaceables, restoredOrderLines);
		return box != null && job != null ? new BoxWithOrder(box, job) : null;
	}
}
