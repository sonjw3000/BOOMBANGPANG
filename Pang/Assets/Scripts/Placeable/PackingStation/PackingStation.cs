
using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UIElements;

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
	private List<InteractionPoint> interactionPoints = new();
	private Dictionary<InteractionKind, List<int3>> interactionPointMap = new();

	private PackingStationService PackingStations => GameContext.Instance.OBWorkflowMgr.PackingStations;
	
	public int3 GridPosition => gridPosition;
	public IReadOnlyList<InteractionPoint> InteractionPoints => interactionPoints;
	public IReadOnlyDictionary<InteractionKind, List<int3>> InteractionPointMap => interactionPointMap;
	//public int3 PackingPoint => InteractionPointMap[InteractionKind.Work][0];
	//public int3 ToteDropPoint => InteractionPointMap[InteractionKind.Put][0];

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
		//int3 workerPos = (int3)math.floor(workerSlot.position);
		//int3 dropPos = gridPosition;
		//dropPos.z += 1;
		//dropPos.x += 1;
		//interactionPoints.Add(workerPos);
		//interactionPoints.Add(dropPos);
	}

	public void AddInteractionPoint(InteractionKind interactionKind, in int3 point)
	{
		interactionPoints.Add(new(interactionKind, point));

		foreach (InteractionKind value in Enum.GetValues(typeof(InteractionKind)))
		{
			if (value == InteractionKind.None) continue;

			if (interactionKind.HasFlag(value))
			{
				if (!interactionPointMap.ContainsKey(value))
					interactionPointMap[value] = new List<int3>();

				interactionPointMap[value].Add(point);
			}
		}
	}

	public int3 GetClosestInteractionPoint(InteractionKind interactionKind, in int3 from)
	{
		float distance = float.PositiveInfinity;
		int3 closestPoint = default;

		foreach (int3 point in interactionPointMap[interactionKind])
		{
			float d = math.distance(point, from);
			if (distance > d)
			{
				distance = d;
				closestPoint = point;
			}
		}

		if (distance == float.PositiveInfinity)
		{
			Debug.LogError($"No interaction point for {interactionKind} in PackingStation");
		}

		return closestPoint;
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
