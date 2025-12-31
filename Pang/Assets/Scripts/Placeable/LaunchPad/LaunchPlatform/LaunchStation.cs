using UnityEngine;
using Unity.Mathematics;
using System.Collections.Generic;

// 런치 패드 베이스


public class LaunchStation
	: MonoBehaviour
	, IGridPlaceable
	, IGridPlacementEffect
	, IInteractionPoint
{
	[SerializeField] private List<PlatformAddon> addons = new();

	private int3 gridPosition;
	private List<int3> interactionPoints = new();

	public int3 GridPosition => gridPosition;

	public int3 CargoInteractionPoint => interactionPoints[0];
	public IReadOnlyList<int3> InteractionPoints => interactionPoints;

	private LaunchStationService LaunchStations => GameContext.Instance.OBWorkflowMgr.LaunchStations;

	private void Awake()
	{
		foreach (var addon in addons)
		{
			addon.SetPadBase(this);
		}
	}

	private void Start()
	{
		LaunchStations.RegisterLaunchPad(this);
	}

	private void OnDestroy()
	{
		LaunchStations.UnregisterLaunchPad(this);
	}

	public bool TryGetStoreablePad(out CargoStorageAddon addon)
	{
		foreach (var a in addons)
		{
			if (a.GetComponent<CargoStorageAddon>() == null)
				continue;
			addon = (CargoStorageAddon)a;
			return true;
		}

		addon = null;
		return false;
	}

	public bool TryGetLaunchablePad(in BoxBase cargo, out LaunchPadAddon addon)
	{
		foreach (var a in addons)
		{
			if (a.GetComponent<LaunchPadAddon>() != null)
			{
				addon = (LaunchPadAddon)a;
				return true;
			}
		}

		addon = null;
		return false;
	}


	public void OnPositionSet(in int3 position)
	{
		enabled = true;
		// 
		gridPosition = position;

		// zero
		interactionPoints.Add(new int3(
			Mathf.RoundToInt(GridPosition.x + transform.forward.x * 2),
			Mathf.RoundToInt(GridPosition.y),
			Mathf.RoundToInt(GridPosition.z + transform.forward.z * 2)
			));
	}

	public void OnRemoved()
	{
	}

	public void OnDestroyedBy(in DestroyContext ctx)
	{
	}



}