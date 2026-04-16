using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UIElements;

// 런치 패드 베이스


public class LaunchStation
	: MonoBehaviour
	, IGridPlaceable
	, IGridPlacementEffect
	, IInteractionPoint
{
	[SerializeField] private List<PlatformAddon> addons = new();

	private int3 gridPosition;
	private List<InteractionPoint> interactionPoints = new();
	private Dictionary<InteractionKind, List<int3>> interactionPointMap = new();

	public int3 GridPosition => gridPosition;
	public WorkerStatusTarget BuildingTarget => WorkerStatusTarget.LaunchStation;
	
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
		LaunchStations.Register(this);
	}

	private void OnDestroy()
	{
		LaunchStations.Unregister(this);
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
		gridPosition = position;
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
			Debug.LogError($"No interaction point for {interactionKind} in LaunchStation");
		}

		return closestPoint;
	}


	public void OnRemoved()
	{
	}

	public void OnDestroyedBy(in DestroyContext ctx)
	{
	}

	public bool IsInteractionAvailable(InteractionKind interactionKind)
	{
		return true;
	}
}
