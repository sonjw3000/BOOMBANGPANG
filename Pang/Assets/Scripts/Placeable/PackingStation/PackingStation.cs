using System;
using Unity.Mathematics;
using UnityEngine;

public class PackingStation :
	BoxInteraction
{
	[SerializeField] Transform waitStackSlot = null;
	[SerializeField] Transform packingSlot = null;
	[SerializeField] Transform endStackSlot = null;
	[SerializeField] Transform workerSlot = null;

	private AIWorker currentPackingWorker = null;

	private BoxBase waitStackBox = null;
	private BoxBase currentPackingBox = null;
	private BoxBase endPackingBox = null;

	private PackingStationService PackingStations => GameContext.Instance.OBWorkflowMgr.PackingStations;

	public override WorkerStatusTarget BuildingTarget => WorkerStatusTarget.PackingStation;

	public AIWorker CurrentPackingWorker { 
		get { return currentPackingWorker; }
		set
		{
			//value.transform.SetParent(workerSlot);
			//value.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
			currentPackingWorker = value;
			if (value != null)
			{
				PackingStations.Enqueue(this);
			}
		}
	}
	public BoxBase CurrentPackingBox
	{
		get { return currentPackingBox; }
		private set
		{
			if (value != null)
			{
				value.transform.SetParent(packingSlot);
				value.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
			}
			if (currentPackingBox != null)
				currentPackingBox.transform.SetParent(null);
			currentPackingBox = value;
		}
	}
	public BoxBase WaitStackBox
	{
		get { return waitStackBox; }
		private set
		{
			if (value != null)
			{ 
				//Debug.Log("Packing box set at station");

				value.transform.SetParent(waitStackSlot);
				value.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);

				currentPackingWorker.enabled = true;
			}
			else
			{
				PackingStations.Enqueue(this);
			}

			if (waitStackBox != null)
				waitStackBox.transform.SetParent(null);
			waitStackBox = value;
		}
	}
	public BoxBase EndStackBox
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
				value.transform.SetParent(endStackSlot);
				value.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
			}

			if (endPackingBox != null)
				endPackingBox.transform.SetParent(null);
			endPackingBox = value;

		}
	}

	public bool IsPackageCanBeHandled =>
		CurrentPackingWorker != null &&
		CurrentPackingBox != null;

	public bool IsNoWorkerAssigned =>
		CurrentPackingWorker == null;

	public override bool CanGetBox() => EndStackBox != null;
	public override bool CanPutBox() => WaitStackBox != null;

	private void Start()
	{
		PackingStations.Register(this);
	}

	private void OnDestroy()
	{
		PackingStations.UnRegister(this);
	}

	public override void OnPositionSet(in int3 pos)
	{
		enabled = true;
		position = pos;
	}

	public override void OnDestroyedBy(in DestroyContext context)
	{

	}

	public override void OnRemoved()
	{
	}

	public bool IsBoxPackable() => WaitStackBox != null && CurrentPackingBox == null;
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

		box = EndStackBox;
		EndStackBox = null;

		return true;
	}

	public override bool PutBox(BoxBase box)
	{
		if (WaitStackBox != null)
			return false;

		WaitStackBox = box;

		return true;
	}

}
