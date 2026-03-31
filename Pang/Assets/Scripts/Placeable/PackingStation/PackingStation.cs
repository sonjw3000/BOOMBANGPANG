
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

public class PackingStation :
	MonoBehaviour,
	IGridPlaceable,
	IInteractionPoint
{
	[SerializeField] Transform waitStackSlot = null;
	[SerializeField] Transform packingSlot = null;
	[SerializeField] Transform endStackSlot = null;
	[SerializeField] Transform workerSlot = null;

	private AIWorker currentPackingWorker = null;

	private BoxBase waitStackBox = null;
	private BoxBase currentPackingBox = null;
	private BoxBase endPackingBox = null;

	private int3 gridPosition;
	private List<int3> interactionPoints = new List<int3>();
	
	private PackingStationService PackingStations => GameContext.Instance.OBWorkflowMgr.PackingStations;
	
	public int3 GridPosition => gridPosition;
	public IReadOnlyList<int3> InteractionPoints => interactionPoints;
	public int3 PackingPoint => InteractionPoints[0];
	public int3 ToteDropPoint => InteractionPoints[1];

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
		set
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
		set
		{
			if (value != null)
			{ 
				Debug.Log("Packing box set at station");

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
		set
		{
			if (value == null)
			{
				Debug.Log("End tote is removed! lets work");
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

	private void Start()
	{
		PackingStations.Register(this);
	}

	private void OnDestroy()
	{
		PackingStations.UnRegister(this);
	}

	public void OnPositionSet(in int3 pos)
	{
		gridPosition = pos;
		int3 workerPos = (int3)math.floor(workerSlot.position);
		int3 dropPos = gridPosition;
		dropPos.z += 1;
		dropPos.x += 1;
		interactionPoints.Add(workerPos);
		interactionPoints.Add(dropPos);
	}

	public void OnDestroyedBy(in DestroyContext context)
	{

	}

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

		EndStackBox= CurrentPackingBox;
		CurrentPackingBox = null;

		return true;
	}
}
