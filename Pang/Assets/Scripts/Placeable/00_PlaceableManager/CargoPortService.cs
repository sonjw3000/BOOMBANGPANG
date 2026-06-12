using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

[MovedFrom(true, sourceClassName: "CargoPortManager")]
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
		if (TryGetBuildingId(pos, out uint buildingId))
		{
			CargoPort target = GetClosestAvailableTarget(buildingId, pos, interactionKind);
			if (target != null)
				return target;
		}

		return GetClosestAvailableTarget(GetAllFacilities(), pos, interactionKind);
	}

	public CargoPort GetClosestAvailableTarget(uint buildingId, in int3 pos, InteractionKind interactionKind)
	{
		if (TryGetBuildingFacilities(buildingId, out var facilities) == false)
			return null;

		return GetClosestAvailableTarget(facilities, pos, interactionKind);
	}

	public CargoPort GetClosestAvailableTargetForBox(in int3 pos, InteractionKind interactionKind, BoxBase box)
	{
		if (TryGetBuildingId(pos, out uint buildingId))
		{
			CargoPort target = GetClosestAvailableTargetForBox(buildingId, pos, interactionKind, box);
			if (target != null)
				return target;
		}

		return GetClosestAvailableTargetForBox(GetAllFacilities(), pos, interactionKind, box);
	}

	public CargoPort GetClosestAvailableTargetForBox(uint buildingId, in int3 pos, InteractionKind interactionKind, BoxBase box)
	{
		if (TryGetBuildingFacilities(buildingId, out var facilities) == false)
			return null;

		return GetClosestAvailableTargetForBox(facilities, pos, interactionKind, box);
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

	private IReadOnlyList<CargoPort> GetAllFacilities()
	{
		List<CargoPort> facilities = new();
		IReadOnlyList<uint> buildingIds = FacilityManager.GetBuildingIds();
		for (int i = 0; i < buildingIds.Count; ++i)
		{
			if (TryGetBuildingFacilities(buildingIds[i], out var buildingFacilities) == false)
				continue;

			for (int facilityIndex = 0; facilityIndex < buildingFacilities.Count; ++facilityIndex)
				facilities.Add(buildingFacilities[facilityIndex]);
		}

		return facilities;
	}

	private static CargoPort GetClosestAvailableTarget(IReadOnlyList<CargoPort> facilities, in int3 pos, InteractionKind interactionKind)
	{
		CargoPort target = null;
		int posPowMin = int.MaxValue;

		for (int i = 0; i < facilities.Count; ++i)
		{
			CargoPort candidate = facilities[i];
			if (candidate == null || candidate.IsInteractionAvailable(interactionKind) == false)
				continue;

			if (InteractionPointSelector.TryGetClosestSameRegionInteractionPoint(
				candidate,
				interactionKind,
				pos,
				GameContext.Instance.GridService,
				out _,
				out int sum) == false)
			{
				continue;
			}

			if (posPowMin > sum)
			{
				posPowMin = sum;
				target = candidate;
			}
		}

		return target;
	}

	private static CargoPort GetClosestAvailableTargetForBox(
		IReadOnlyList<CargoPort> facilities,
		in int3 pos,
		InteractionKind interactionKind,
		BoxBase box)
	{
		CargoPort target = null;
		int posPowMin = int.MaxValue;

		for (int i = 0; i < facilities.Count; ++i)
		{
			CargoPort candidate = facilities[i];
			if (candidate == null ||
				candidate.IsInteractionAvailable(interactionKind) == false ||
				CanAcceptAllStacks(candidate, box) == false)
			{
				continue;
			}

			if (InteractionPointSelector.TryGetClosestSameRegionInteractionPoint(
				candidate,
				interactionKind,
				pos,
				GameContext.Instance.GridService,
				out _,
				out int sum) == false)
			{
				continue;
			}

			if (posPowMin > sum)
			{
				posPowMin = sum;
				target = candidate;
			}
		}

		return target;
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
