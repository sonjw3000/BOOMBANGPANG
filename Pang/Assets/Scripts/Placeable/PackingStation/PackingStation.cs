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

	private BoxWithOrder waitStackBox = null;
	private BoxWithOrder currentPackingBox = null;
	private BoxWithOrder endPackingBox = null;

	// item container for packing items
	private List<ItemPackage> packedItems = new();
	protected Dictionary<uint, int> itemTotals = new();
	private float totalSize = 0.0f;


	// IItemContainer's property
	public IReadOnlyList<ItemStack> Stacks => packedItems;
	public IReadOnlyDictionary<uint, int> ItemTotals => itemTotals;
	public float TotalSize => totalSize;
	public float MaxSize => sizePerStack * maxStacks;
	public bool CanRegister() => maxStacks > Stacks.Count;

	// properties
	private PackingStationService PackingStations => GameContext.Instance.OBWorkflowMgr.PackingStations;

	public override WorkerStatusTarget BuildingTarget => WorkerStatusTarget.PackingStation;


	public AIWorker CurrentPackingWorker { 
		get { return currentPackingWorker; }
		set
		{
			if (currentPackingWorker != null)
				currentPackingWorker.OnWorkingPointSet(null);

			currentPackingWorker = value;
			if (value != null)
			{
				currentPackingWorker.OnWorkingPointSet(this);
				PackingStations.Enqueue(this);
			}
		}
	}
	public BoxWithOrder CurrentPackingBox
	{
		get { return currentPackingBox; }
		private set
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
	}
	public BoxWithOrder WaitStackBox
	{
		get { return waitStackBox; }
		private set
		{
			if (value != null)
			{ 
				//Debug.Log("Packing box set at station");

				value.Box.transform.SetParent(waitStackSlot);
				value.Box.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);

				currentPackingWorker.enabled = true;
			}
			else
			{
				PackingStations.Enqueue(this);
			}

			if (waitStackBox != null)
				waitStackBox.Box.transform.SetParent(null);
			waitStackBox = value;
		}
	}
	public BoxWithOrder EndStackBox
	{
		get { return endPackingBox; }
		private set
		{
			if (value == null)
			{
				//Debug.Log("End tote is removed! lets work");
				CurrentPackingWorker.enabled = true;
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

	public bool IsPackageCanBeHandled =>
		CurrentPackingWorker != null &&
		CurrentPackingBox != null;

	public bool IsNoWorkerAssigned =>
		CurrentPackingWorker == null;

	public override bool CanGetBox() => EndStackBox != null;
	public override bool CanPutBox() => WaitStackBox == null;

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

	public bool IsBoxMoveableToPack => WaitStackBox != null && CurrentPackingBox == null;
	public bool IsBoxMoveableToEnd => CurrentPackingBox != null && EndStackBox == null;

	public bool PrepareBox()
	{
		if (WaitStackBox == null)
			return false;

		if (CurrentPackingBox != null)
		{
			Debug.LogError("Why current packing box is not null???? have to check");
			return false;
		}
		CurrentPackingBox = WaitStackBox;
		WaitStackBox = null;

		return true;
	}

	public bool EndWorkingBox()
	{
		if (EndStackBox != null)
			return false;

		EndStackBox = CurrentPackingBox;
		CurrentPackingBox = null;

		// notify to station
		PackingStations.OnPackingComplete(this);

		return true;
	}

	public override bool GetBox(out BoxBase box)
	{
		box = null;
		if (EndStackBox == null)
			return false;

		box = EndStackBox.Box;
		EndStackBox = null;

		return true;
	}

	public override bool PutBox(BoxBase box)
	{
		if (WaitStackBox != null)
			return false;

		WaitStackBox.Box = box;

		return true;
	}

	public bool PutBoxToPack(BoxWithOrder boxToPack)
	{
		if (WaitStackBox != null)
			return false;

		waitStackBox = boxToPack;
		boxToPack.Job.ResetForPacking();

		return true;
	}

}
