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

public partial class PackingStation :
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

	private BoxWithOrder waitStackBox = null;
	private BoxWithOrder currentPackingBox = null;
	private BoxWithOrder endPackingBox = null;

	private readonly List<ItemStack> packedItems = new();
	protected Dictionary<uint, int> itemTotals = new();
	private float totalSize = 0.0f;
	private ItemTag itemTags = ItemTag.None;

	public event Action<PackingStation> OnItemContentChanged;

	public IReadOnlyList<ItemStack> Stacks => packedItems;
	public IReadOnlyDictionary<uint, int> ItemTotals => itemTotals;
	public float TotalSize => totalSize;
	public float MaxSize => sizePerStack * maxStacks;
	public ItemTag ItemTags => itemTags;
	protected ItemDatabase itemDB => GameContext.Instance.ItemDB;


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
		if (stack == null || stack.HasStatus(ItemStatus.Packed) == false)
			return false;

		if (stack.Quantity <= 0 || totalSize + stack.Size > MaxSize)
			return false;

		return FindMergeTarget(stack) != null || packedItems.Count < maxStacks;
	}

	private PackingStationService PackingStationService => GameContext.Instance.OBWorkflowSvc.PackingStationService;

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

		PackingStationService.RefreshWaitingStation(this);
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

		PackingStationService.OnPackingComplete(this);
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
		if (box == null)
			return false;

		if (GameContext.HasInstance == false)
			return false;

		if (GameContext.Instance.OBWorkflowSvc.TryBuildPackingJob(box, this, this, out WorkJob job) == false)
		{
			Debug.LogError("PackingStation requires a box with packing manifest.");
			return false;
		}

		return PutBoxToPack(new BoxWithOrder(box, job));
	}

	public bool PutBoxToPack(BoxWithOrder boxToPack)
	{
		if (waitStackBox != null || boxToPack == null)
			return false;

		incomingRequestSuspended = false;
		boxToPack.Job.ResetForPacking();
		ClearIncomingBoxReservation();
		SetWaitStackBox(boxToPack);
		PackingStationService.RequestPackingTaskIfNeeded(this);
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
		if (CanAcceptStack(stack) == false)
			return false;

		uint itemId = stack.ItemID;
		int quantity = stack.Quantity;
		ItemStack mergeTarget = FindMergeTarget(stack);
		if (mergeTarget != null)
		{
			if (mergeTarget.TryMergeFrom(stack) == false)
				return false;
		}
		else
		{
			packedItems.Add(stack);
		}

		itemTotals[itemId] = itemTotals.GetValueOrDefault(itemId) + quantity;
		UpdateSize();
		OnItemContentChanged?.Invoke(this);
		return true;
	}

	public bool RemoveStack(ItemStack stack)
	{
		if (stack == null || packedItems.Remove(stack) == false)
			return false;

		itemTotals[stack.ItemID] = itemTotals.GetValueOrDefault(stack.ItemID) - stack.Quantity;
		if (itemTotals[stack.ItemID] <= 0)
			itemTotals.Remove(stack.ItemID);

		UpdateSize();
		OnItemContentChanged?.Invoke(this);
		return true;
	}

	public bool TryRemoveFromStack(ItemStack stack, int quantity, out ItemStack removedStack)
	{
		removedStack = null;
		if (stack == null || quantity <= 0 || packedItems.Contains(stack) == false)
			return false;

		uint itemId = stack.ItemID;
		removedStack = stack.Split(quantity);
		if (removedStack == null)
			return false;

		int removedQuantity = removedStack.Quantity;
		itemTotals[itemId] = itemTotals.GetValueOrDefault(itemId) - removedQuantity;
		if (itemTotals[itemId] <= 0)
			itemTotals.Remove(itemId);

		if (stack.Quantity <= 0)
		{
			packedItems.Remove(stack);
			stack.Recycle();
		}

		UpdateSize();
		OnItemContentChanged?.Invoke(this);
		return true;
	}

	private void UpdateSize()
	{
		totalSize = 0.0f;
		for (int i = 0; i < packedItems.Count; ++i)
			totalSize += packedItems[i].Size;

		itemTags = ItemTag.None;

		if (itemDB == null || packedItems == null)
			return;

		for (int i = 0; i < packedItems.Count; ++i)
		{
			ItemStack stack = packedItems[i];
			if (stack == null || stack.Quantity <= 0)
				continue;

			if (itemDB.GetItemData(stack.ItemID, out ItemDefinition itemData) == false || itemData == null)
				continue;

			itemTags |= itemData.Tag;
		}
	}

	private ItemStack FindMergeTarget(ItemStack incoming)
	{
		if (incoming == null)
			return null;

		for (int i = 0; i < packedItems.Count; ++i)
		{
			ItemStack existing = packedItems[i];
			if (ReferenceEquals(existing, incoming))
				continue;

			if (existing.CanMergeWith(incoming))
				return existing;
		}

		return null;
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

}
