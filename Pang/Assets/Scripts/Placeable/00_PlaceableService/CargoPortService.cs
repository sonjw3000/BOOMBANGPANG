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
	protected override bool IsDestinationCandidate(
		CargoPort facility,
		uint buildingId,
		InteractionKind interactionKind,
		ZoneFilter zoneFilter)
	{
		return base.IsDestinationCandidate(facility, buildingId, interactionKind, zoneFilter)
			&& facility.IsInteractionAvailable(interactionKind);
	}

	public CargoPort FindClosestAvailablePort(
		in int3 pos,
		InteractionKind interactionKind,
		uint buildingId = 0,
		Predicate<CargoPort> predicate = null)
	{
		Predicate<CargoPort> combinedPredicate = candidate =>
			candidate != null &&
			(predicate == null || predicate(candidate));

		if (buildingId != 0)
		{
			return TryFindDestination(buildingId, pos, interactionKind, ZoneFilter.None, out CargoPort buildingTarget, combinedPredicate)
				? buildingTarget
				: null;
		}

		if (TryGetBuildingId(pos, out uint localBuildingId) &&
			TryFindDestination(localBuildingId, pos, interactionKind, ZoneFilter.None, out CargoPort localTarget, combinedPredicate))
		{
			return localTarget;
		}

		return TryFindDestination(0, pos, interactionKind, ZoneFilter.None, out CargoPort globalTarget, combinedPredicate)
			? globalTarget
			: null;
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

	private bool TryGetRegisteredBuildingId(CargoPort port, out uint buildingId)
	{
		if (port != null && registeredBuildingIds.TryGetValue(port, out buildingId))
			return true;

		buildingId = 0;
		return false;
	}
}
