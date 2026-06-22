using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UIElements;

// 런치 패드 베이스


public class LaunchStation
	: MonoBehaviour
	, IFacility
	, IGridPlacementEffect
	, IInteractionPoint
{
	[SerializeField] private List<PlatformAddon> addons = new();

	private int3 gridPosition;
	private FacingDirection facingDirection;

	private List<InteractionPoint> interactionPoints = new();
	private Dictionary<InteractionKind, List<int3>> interactionPointMap = new();

	public int3 GridPosition => gridPosition;
	public FacingDirection Direction => facingDirection;
	public WorkerStatusTarget BuildingTarget => WorkerStatusTarget.LaunchStation;
	public IReadOnlyList<InteractionPoint> InteractionPoints => interactionPoints;
	
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
		// Facility registration is now owned by GridService -> FacilityManager.
	}

	public LaunchStationSaveData CaptureState()
	{
		LaunchStationSaveData data = new();
		if (TryGetAddon<CargoStorageAddon>(out var cargoStorage))
		{
			foreach (var cargo in cargoStorage.CargosToLaunch)
			{
				if (cargo != null)
				{
					data.CargoQueueBoxes.Add(new BoxReferenceSaveData
					{
						BoxType = cargo.Type,
						BoxId = cargo.BoxId,
					});
				}
			}
		}

		if (TryGetAddon<LaunchPadAddon>(out var launchPad))
		{
			data.ReadyToLaunch = launchPad.IsReady;
			if (launchPad.CargoToLaunch != null)
			{
				data.LoadedCargoBox = new BoxReferenceSaveData
				{
					BoxType = launchPad.CargoToLaunch.Type,
					BoxId = launchPad.CargoToLaunch.BoxId,
				};
			}
		}

		return data;
	}

	public void RestoreState(LaunchStationSaveData data)
	{
		if (data == null)
			return;

		if (TryGetAddon<CargoStorageAddon>(out var cargoStorage))
		{
			List<BoxBase> cargos = new();
			foreach (var cargoRef in data.CargoQueueBoxes)
			{
				if (cargoRef != null && GameContext.Instance.BoxMgr.TryGetBox(cargoRef.BoxType, cargoRef.BoxId, out var cargo))
					cargos.Add(cargo);
			}

			cargoStorage.RestoreState(cargos);
		}

		if (TryGetAddon<LaunchPadAddon>(out var launchPad))
		{
			BoxBase loadedCargo = null;
			if (data.LoadedCargoBox != null)
				GameContext.Instance.BoxMgr.TryGetBox(data.LoadedCargoBox.BoxType, data.LoadedCargoBox.BoxId, out loadedCargo);
			launchPad.RestoreState(loadedCargo, data.ReadyToLaunch);
		}
	}
}
