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
	private FacingDirection facingDirection;

	private List<InteractionPoint> interactionPoints = new();
	private Dictionary<InteractionKind, List<int3>> interactionPointMap = new();
	private bool isRegistered = false;

	public int3 GridPosition => gridPosition;
	public FacingDirection Direction => facingDirection;
	public WorkerStatusTarget BuildingTarget => WorkerStatusTarget.LaunchStation;
	public IReadOnlyList<InteractionPoint> InteractionPoints => interactionPoints;
	
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
		InitializeForSaveLoad();
	}

	private void OnDestroy()
	{
		if (isRegistered)
		{
			LaunchStations.Unregister(this);
			isRegistered = false;
		}
	}

	public bool TryGetAddon<T>(out T addon) where T : PlatformAddon
	{
		foreach (var a in addons)
		{
			if (a.GetComponent<T>() == null)
				continue;
			addon = a as T;
			return true;
		}

		addon = null;
		return false;
	}

	public void OnPositionSet(in int3 position, FacingDirection direction)
	{
		enabled = true;
		gridPosition = position;
		facingDirection = direction;
	}

	public void ClearInteractionPoints()
	{
		interactionPoints.Clear();
		interactionPointMap.Clear();
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

	public void InitializeForSaveLoad()
	{
		if (isRegistered)
			return;

		LaunchStations.Register(this);
		isRegistered = true;
	}

	public LaunchStationSaveData CaptureState()
	{
		LaunchStationSaveData data = new();
		if (TryGetAddon<CargoStorageAddon>(out var cargoStorage))
		{
			foreach (var cargo in cargoStorage.CargosToLaunch)
			{
				if (cargo != null)
					data.CargoQueueBoxIds.Add(cargo.BoxId);
			}
		}

		if (TryGetAddon<LaunchPadAddon>(out var launchPad))
		{
			data.ReadyToLaunch = launchPad.IsReady;
			if (launchPad.CargoToLaunch != null)
				data.LoadedCargoBoxId = launchPad.CargoToLaunch.BoxId;
		}

		return data;
	}

	public void RestoreState(LaunchStationSaveData data, IReadOnlyDictionary<uint, BoxBase> restoredBoxes)
	{
		if (data == null || restoredBoxes == null)
			return;

		if (TryGetAddon<CargoStorageAddon>(out var cargoStorage))
		{
			List<BoxBase> cargos = new();
			foreach (var cargoId in data.CargoQueueBoxIds)
			{
				if (restoredBoxes.TryGetValue(cargoId, out var cargo))
					cargos.Add(cargo);
			}

			cargoStorage.RestoreState(cargos);
		}

		if (TryGetAddon<LaunchPadAddon>(out var launchPad))
		{
			restoredBoxes.TryGetValue(data.LoadedCargoBoxId, out var loadedCargo);
			launchPad.RestoreState(loadedCargo, data.ReadyToLaunch);
		}
	}
}
