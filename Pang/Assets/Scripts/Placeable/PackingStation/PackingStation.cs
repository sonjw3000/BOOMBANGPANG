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
	public bool HasWaitingBox => waitStackBox != null;
	public bool IsNoWorkerAssigned => currentPackingWorker == null;
	public bool IsBoxMoveableToPack => waitStackBox != null && currentPackingBox == null;
	public bool IsBoxMoveableToEnd => currentPackingBox != null && endPackingBox == null;

	public override bool CanGetBox() => endPackingBox != null;
	public override bool CanPutBox() => waitStackBox == null;

	private void Start()
	{
		PackingStations.Register(this);
	}

	private void OnDestroy()
	{
		PackingStations.UnRegister(this);
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

		for (int i = packedItems.Count - 1; i >= 0; --i)
		{
			if (currentPackingBox.Box.AddStack(packedItems[i]) == false)
			{
				Debug.LogWarning("Box's Stack is full");
				break;
			}

			packedItems.RemoveAt(i);
		}

		SetEndStackBox(currentPackingBox);
		SetCurrentPackingBox(null);

		PackingStations.OnPackingComplete(this);
		PackingStations.OnPackingTaskCompleted(this);
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
		if (maxStacks <= packedItems.Count || stack is not ItemPackage pkg)
			return false;

		packedItems.Add(pkg);
		return true;
	}

	public bool RemoveStack(ItemStack stack)
	{
		if (stack is not ItemPackage pkg || packedItems.Remove(pkg) == false)
			return false;

		return true;
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
