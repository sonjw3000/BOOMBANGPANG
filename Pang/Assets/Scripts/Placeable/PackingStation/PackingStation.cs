
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

public class PackingStation :
	MonoBehaviour,
	IGridPlaceable,
	IInteractionPoint
{
	private AIWorker currentPackingWorker = null;
	private BoxBase currentPackingBox = null;
	
	private int3 gridPosition;
	private List<int3> interationPoints = new List<int3>();
	
	private PackingStationService PackingStations => GameContext.Instance.OBWorkflowMgr.PackingStations;
	
	public int3 GridPosition => gridPosition;
	public IReadOnlyList<int3> InteractionPoints => interationPoints;

	public AIWorker CurrentPackingWorker { get; set; }
	public BoxBase CurrentPackingBox
	{
		get { return currentPackingBox; }
		set
		{
			currentPackingBox = value;
			if (currentPackingBox != null)
			{ 
				Debug.Log("Packing box set at station");
				currentPackingWorker.enabled = true;
			}
		}
	}

	public bool IsPackageCanBeHandled =>
		currentPackingWorker != null &&
		currentPackingBox == null;

	public bool IsNoWorkerAssigned =>
		currentPackingWorker == null;

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
	}

	public void OnDestroyedBy(in DestroyContext context)
	{

	}

}
