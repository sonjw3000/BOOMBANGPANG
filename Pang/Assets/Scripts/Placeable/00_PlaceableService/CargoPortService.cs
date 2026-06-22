using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

public class CargoPortService : FacilityService<CargoPort>, ICollectSupplySource
{
	public event Action<uint, CargoPort> OnCargoDocked;
	public event Action<uint, CargoPort> OnCargoUndocked;
	public event Action<uint, CargoPort> OnCargoQuantityZero;
	public event Action<uint, CargoPort> OnCargoQuantityOverPercent;

	public float CargoStandardPercent => 50.0f;
	private readonly Dictionary<CargoPort, uint> registeredBuildingIds = new();

	protected override void OnRegisterFacility(uint buildingId, CargoPort facility)
	{
		registeredBuildingIds[facility] = buildingId;
		facility.OnCargoDocked += HandleCargoDocked;
		facility.OnCargoUndocked += HandleCargoUndocked;
		if (facility is InboundCargoPort)
			facility.OnCargoQuantityZero += HandleCargoQuantityZero;
		else
			facility.OnCargoQuantityOverPercent += HandleCargoQuantityOverPercent;
	}

	protected override void OnUnregisterFacility(uint buildingId, CargoPort facility)
	{
		registeredBuildingIds.Remove(facility);
		facility.OnCargoDocked -= HandleCargoDocked;
		facility.OnCargoUndocked -= HandleCargoUndocked;
		if (facility is InboundCargoPort)
			facility.OnCargoQuantityZero -= HandleCargoQuantityZero;
		else
		facility.OnCargoQuantityOverPercent -= HandleCargoQuantityOverPercent;

		RemoveLinkedPortReferences(facility);
		facility.ClearLinkedPorts();
	}

	// cargoport event handlers
	private void HandleCargoDocked(CargoPort port)
	{
		if (TryGetRegisteredBuildingId(port, out uint buildingId))
			OnCargoDocked?.Invoke(buildingId, port);
	}

	private void HandleCargoUndocked(CargoPort port)
	{
		if (TryGetRegisteredBuildingId(port, out uint buildingId))
			OnCargoUndocked?.Invoke(buildingId, port);
	}

	private void HandleCargoQuantityZero(CargoPort port)
	{
		if (TryGetRegisteredBuildingId(port, out uint buildingId))
			OnCargoQuantityZero?.Invoke(buildingId, port);
	}

	private void HandleCargoQuantityOverPercent(CargoPort port)
	{
		if (TryGetRegisteredBuildingId(port, out uint buildingId))
			OnCargoQuantityOverPercent?.Invoke(buildingId, port);
	}

	// target finding
	public CargoPort FindClosestAvailablePort(
		in int3 pos,
		InteractionKind interactionKind,
		uint buildingId = 0,
		Predicate<CargoPort> predicate = null)
	{
		FacilityDistanceResolver distanceResolver = (CargoPort candidate, in int3 origin, out int score) =>
			InteractionPointSelector.TryGetClosestSameRegionInteractionPoint(
				candidate,
				interactionKind,
				origin,
				GameContext.Instance.GridService,
				out _,
				out score);

		Predicate<CargoPort> combinedPredicate = candidate =>
			candidate != null &&
			candidate.IsInteractionAvailable(interactionKind) &&
			(predicate == null || predicate(candidate));

		if (buildingId != 0)
		{
			return TryFindClosestFacility(buildingId, pos, distanceResolver, out CargoPort buildingTarget, combinedPredicate)
				? buildingTarget
				: null;
		}

		if (TryGetBuildingId(pos, out uint localBuildingId) &&
			TryFindClosestFacility(localBuildingId, pos, distanceResolver, out CargoPort localTarget, combinedPredicate))
		{
			return localTarget;
		}

		if (TryFindClosestFacility(pos, distanceResolver, out CargoPort globalTarget, combinedPredicate))
			return globalTarget;

		return null;
	}

	public CargoPort FindClosestAvailablePortForBox(
		in int3 pos,
		InteractionKind interactionKind,
		BoxBase box,
		uint buildingId = 0,
		Predicate<CargoPort> predicate = null)
	{
		return FindClosestAvailablePort(
			pos,
			interactionKind,
			buildingId,
			candidate =>
				CanAcceptBox(candidate, box, interactionKind) &&
				(predicate == null || predicate(candidate)));
	}

	public IEnumerable<ShelfBase> GetSources(uint itemId)
	{
		yield break;
	}

	public IEnumerable<ShelfBase> GetSources(uint buildingId, uint itemId)
	{
		yield break;
	}

	public IReadOnlyList<CargoPort> GetCargoPorts(uint buildingId)
	{
		return TryGetBuildingFacilities(buildingId, out var facilities)
			? facilities
			: Array.Empty<CargoPort>();
	}

	public bool TryQueryPorts(uint buildingId, List<CargoPort> results, Predicate<CargoPort> predicate = null)
	{
		return TryQueryFacilities(buildingId, results, predicate);
	}

	private static bool CanAcceptBox(CargoPort port, BoxBase box, InteractionKind interactionKind)
	{
		if (port == null || box is not CargoCapsule)
			return false;

		return interactionKind == InteractionKind.Put ? port.CanPutBox() : port.CanGetBox();
	}

	private void RemoveLinkedPortReferences(CargoPort targetPort)
	{
		if (targetPort == null)
			return;

		IReadOnlyList<uint> buildingIds = FacilityManager.GetBuildingIds();
		for (int i = 0; i < buildingIds.Count; ++i)
		{
			if (TryGetBuildingFacilities(buildingIds[i], out var facilities) == false)
				continue;

			for (int facilityIndex = 0; facilityIndex < facilities.Count; ++facilityIndex)
				facilities[facilityIndex]?.RemoveLinkedPort(targetPort);
		}
	}

	private bool TryGetRegisteredBuildingId(CargoPort port, out uint buildingId)
	{
		if (port != null && registeredBuildingIds.TryGetValue(port, out buildingId))
			return true;

		buildingId = 0;
		return false;
	}
}
