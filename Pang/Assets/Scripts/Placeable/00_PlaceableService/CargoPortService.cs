using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

public class CargoPortService : FacilityService<CargoPort>, ICollectSupplySource
{
	public event System.Action<ShelfBase, uint, bool> OnItemPresentChanged;
	public event System.Action<ShelfBase, uint, int> OnItemQuantityChanged;
	public event System.Action<ShelfBase, uint, int> OnReserveQuantityChanged;

	protected override void OnRegisterFacility(uint buildingId, CargoPort facility)
	{
		if (facility == null)
			return;

		facility.OnItemPresentChanged += HandlePresentChange;
		facility.OnItemQuantityChanged += HandleItemQuantityChanged;
		facility.OnItemReservedPickChanged += HandleReserveQuantityChanged;
	}

	protected override void OnUnregisterFacility(uint buildingId, CargoPort facility)
	{
		if (facility == null)
			return;

		facility.OnItemPresentChanged -= HandlePresentChange;
		facility.OnItemQuantityChanged -= HandleItemQuantityChanged;
		facility.OnItemReservedPickChanged -= HandleReserveQuantityChanged;
	}

	private void HandlePresentChange(ShelfBase port, uint itemId, bool present)
	{
		OnItemPresentChanged?.Invoke(port, itemId, present);
	}

	private void HandleItemQuantityChanged(ShelfBase port, uint itemId, int quantityDelta)
	{
		OnItemQuantityChanged?.Invoke(port, itemId, quantityDelta);
	}

	private void HandleReserveQuantityChanged(ShelfBase port, uint itemId, int reservedQuantityDelta)
	{
		OnReserveQuantityChanged?.Invoke(port, itemId, reservedQuantityDelta);
	}

	public CargoPort GetClosestAvailableTarget(in int3 pos, InteractionKind interactionKind)
	{
		FacilityDistanceResolver distanceResolver = (CargoPort candidate, in int3 origin, out int score) =>
			InteractionPointSelector.TryGetClosestSameRegionInteractionPoint(
				candidate,
				interactionKind,
				origin,
				GameContext.Instance.GridService,
				out _,
				out score);

		Predicate<CargoPort> predicate = candidate => candidate.IsInteractionAvailable(interactionKind);

		if (TryGetBuildingId(pos, out uint buildingId) &&
			TryFindClosestFacility(buildingId, pos, distanceResolver, out CargoPort target, predicate))
		{
			return target;
		}

		if (TryFindClosestFacility(pos, distanceResolver, out CargoPort globalTarget, predicate))
			return globalTarget;

		return null;
	}

	public CargoPort GetClosestAvailableTarget(uint buildingId, in int3 pos, InteractionKind interactionKind)
	{
		FacilityDistanceResolver distanceResolver = (CargoPort candidate, in int3 origin, out int score) =>
			InteractionPointSelector.TryGetClosestSameRegionInteractionPoint(
				candidate,
				interactionKind,
				origin,
				GameContext.Instance.GridService,
				out _,
				out score);

		Predicate<CargoPort> predicate = candidate => candidate.IsInteractionAvailable(interactionKind);
		return TryFindClosestFacility(buildingId, pos, distanceResolver, out CargoPort target, predicate)
			? target
			: null;
	}

	public CargoPort GetClosestAvailableTargetForBox(in int3 pos, InteractionKind interactionKind, BoxBase box)
	{
		FacilityDistanceResolver distanceResolver = (CargoPort candidate, in int3 origin, out int score) =>
			InteractionPointSelector.TryGetClosestSameRegionInteractionPoint(
				candidate,
				interactionKind,
				origin,
				GameContext.Instance.GridService,
				out _,
				out score);

		Predicate<CargoPort> predicate = candidate =>
			candidate.IsInteractionAvailable(interactionKind) &&
			CanAcceptAllStacks(candidate, box);

		if (TryGetBuildingId(pos, out uint buildingId) &&
			TryFindClosestFacility(buildingId, pos, distanceResolver, out CargoPort target, predicate))
		{
			return target;
		}

		if (TryFindClosestFacility(pos, distanceResolver, out CargoPort globalTarget, predicate))
			return globalTarget;

		return null;
	}

	public CargoPort GetClosestAvailableTargetForBox(uint buildingId, in int3 pos, InteractionKind interactionKind, BoxBase box)
	{
		FacilityDistanceResolver distanceResolver = (CargoPort candidate, in int3 origin, out int score) =>
			InteractionPointSelector.TryGetClosestSameRegionInteractionPoint(
				candidate,
				interactionKind,
				origin,
				GameContext.Instance.GridService,
				out _,
				out score);

		Predicate<CargoPort> predicate = candidate =>
			candidate.IsInteractionAvailable(interactionKind) &&
			CanAcceptAllStacks(candidate, box);

		return TryFindClosestFacility(buildingId, pos, distanceResolver, out CargoPort target, predicate)
			? target
			: null;
	}

	public IEnumerable<ShelfBase> GetSources(uint itemId)
	{
		IReadOnlyList<uint> buildingIds = FacilityManager.GetBuildingIds();
		for (int i = 0; i < buildingIds.Count; ++i)
		{
			foreach (ShelfBase source in GetSources(buildingIds[i], itemId))
				yield return source;
		}
	}

	public IEnumerable<ShelfBase> GetSources(uint buildingId, uint itemId)
	{
		if (TryGetBuildingFacilities(buildingId, out var facilities) == false)
			yield break;

		for (int i = 0; i < facilities.Count; ++i)
		{
			CargoPort port = facilities[i];
			if (port != null && port.GetPickableQuantity(itemId) > 0)
				yield return port;
		}
	}

	private static bool CanAcceptAllStacks(CargoPort port, BoxBase box)
	{
		if (port == null || box == null)
			return false;

		if (box.Stacks.Count > port.MaxStack - port.Stacks.Count)
			return false;

		for (int i = 0; i < box.Stacks.Count; ++i)
		{
			if (port.CanAcceptStack(box.Stacks[i]) == false)
				return false;
		}

		return true;
	}
}
