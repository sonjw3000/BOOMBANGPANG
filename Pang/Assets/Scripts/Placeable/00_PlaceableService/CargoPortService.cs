using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

public class CargoPortService : FacilityService<CargoPort>, ICollectSupplySource
{
	public event Action<uint, CargoPort> OnCapsuleDocked;
	public event Action<uint, CargoPort> OnCapsuleUndocked;
	public event Action<uint, CargoPort> OnCargoContentChanged;
	public event Action<uint, CargoPort> OnCargoQuantityZero;
	public event Action<uint, CargoPort> OnCargoQuantityOverPercent;

	public float CargoStandardPercent => 50.0f;
	private readonly Dictionary<CargoPort, uint> registeredBuildingIds = new();

	protected override void OnRegisterFacility(uint buildingId, CargoPort facility)
	{
		registeredBuildingIds[facility] = buildingId;
		facility.OnCapsuleDocked += HandleCapsuleDocked;
		facility.OnCapsuleUndocked += HandleCapsuleUndocked;
		facility.OnCargoContentChanged += HandleCargoContentChanged;
		if (facility is InboundCargoPort)
			facility.OnCargoQuantityZero += HandleCargoQuantityZero;
		else
			facility.OnCargoQuantityOverPercent += HandleCargoQuantityOverPercent;
	}

	protected override void OnUnregisterFacility(uint buildingId, CargoPort facility)
	{
		registeredBuildingIds.Remove(facility);
		facility.OnCapsuleDocked -= HandleCapsuleDocked;
		facility.OnCapsuleUndocked -= HandleCapsuleUndocked;
		facility.OnCargoContentChanged -= HandleCargoContentChanged;
		if (facility is InboundCargoPort)
			facility.OnCargoQuantityZero -= HandleCargoQuantityZero;
		else
			facility.OnCargoQuantityOverPercent -= HandleCargoQuantityOverPercent;
	}

	// cargoport event handlers
	private void HandleCapsuleDocked(CapsuleDock dock)
	{
		if (dock is not CargoPort port)
			return;

		if (TryGetRegisteredBuildingId(port, out uint buildingId))
			OnCapsuleDocked?.Invoke(buildingId, port);
	}

	private void HandleCapsuleUndocked(CapsuleDock dock)
	{
		if (dock is not CargoPort port)
			return;

		if (TryGetRegisteredBuildingId(port, out uint buildingId))
			OnCapsuleUndocked?.Invoke(buildingId, port);
	}

	private void HandleCargoQuantityZero(CargoPort port)
	{
		if (TryGetRegisteredBuildingId(port, out uint buildingId))
			OnCargoQuantityZero?.Invoke(buildingId, port);
	}

	private void HandleCargoContentChanged(CargoPort port)
	{
		if (TryGetRegisteredBuildingId(port, out uint buildingId))
			OnCargoContentChanged?.Invoke(buildingId, port);
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
		FacilityFilter facilityFilter)
	{
		return base.IsDestinationCandidate(facility, buildingId, interactionKind, facilityFilter)
			&& facility.IsInteractionAvailable(interactionKind);
	}

	public CargoPort FindClosestAvailablePort(
		in int3 pos,
		InteractionKind interactionKind,
		uint buildingId = 0,
		Predicate<CargoPort> predicate = null)
	{
		return FindClosestAvailablePort(pos, interactionKind, buildingId, FacilityFilter.None, predicate);
	}

	public CargoPort FindClosestAvailablePort(
		in int3 pos,
		InteractionKind interactionKind,
		uint buildingId,
		FacilityFilter facilityFilter,
		Predicate<CargoPort> predicate = null)
	{
		Predicate<CargoPort> combinedPredicate = candidate =>
			candidate != null &&
			(predicate == null || predicate(candidate));

		if (buildingId != 0)
		{
			return TryFindDestination(buildingId, pos, interactionKind, facilityFilter, out CargoPort buildingTarget, combinedPredicate)
				? buildingTarget
				: null;
		}

		if (TryGetBuildingId(pos, out uint localBuildingId) &&
			TryFindDestination(localBuildingId, pos, interactionKind, facilityFilter, out CargoPort localTarget, combinedPredicate))
		{
			return localTarget;
		}

		return TryFindDestination(0, pos, interactionKind, facilityFilter, out CargoPort globalTarget, combinedPredicate)
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
		return FindClosestAvailablePortForBox(pos, interactionKind, box, buildingId, FacilityFilter.None, predicate);
	}

	public CargoPort FindClosestAvailablePortForBox(
		in int3 pos,
		InteractionKind interactionKind,
		BoxBase box,
		uint buildingId,
		FacilityFilter facilityFilter,
		Predicate<CargoPort> predicate = null)
	{
		return FindClosestAvailablePort(
			pos,
			interactionKind,
			buildingId,
			facilityFilter,
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

	public bool IsRuleMatchedOutboundPort(
		OutboundCargoPort port,
		CargoCapsule capsule)
	{
		if (TryBuildOutboundFilters(capsule, out FacilityFilter physicalFilter, out FacilityFilter launchFilter,
				out bool hasLaunchFilter) == false)
		{
			return false;
		}

		FacilityRuleManager ruleManager = GameContext.HasInstance
			? GameContext.Instance.FacilityRuleMgr
			: null;
		return IsRuleMatchedOutboundPort(port, capsule, physicalFilter, ruleManager) ||
			(hasLaunchFilter && IsRuleMatchedOutboundPort(port, capsule, launchFilter, ruleManager));
	}

	public bool TryFindRuleMatchedOutboundPort(
		uint buildingId,
		CargoCapsule capsule,
		out OutboundCargoPort port,
		bool requireAvailable = true,
		Predicate<OutboundCargoPort> predicate = null)
	{
		port = null;
		if (buildingId == 0 || capsule == null || capsule.RouteKind != CargoRouteKind.Standard ||
			FacilityManager == null ||
			TryBuildOutboundFilters(capsule, out FacilityFilter physicalFilter, out FacilityFilter launchFilter,
				out bool hasLaunchFilter) == false ||
			TryGetBuildingFacilities(buildingId, out IReadOnlyList<CargoPort> ports) == false)
		{
			return false;
		}

		FacilityRuleManager ruleManager = GameContext.HasInstance
			? GameContext.Instance.FacilityRuleMgr
			: null;
		if (ruleManager == null)
			return false;

		int bestPriority = int.MinValue;
		for (int i = 0; i < ports.Count; ++i)
		{
			if (ports[i] is not OutboundCargoPort candidate ||
				(requireAvailable && candidate.CanPutBox() == false) ||
				(predicate != null && predicate(candidate) == false) ||
				(IsRuleMatchedOutboundPort(candidate, capsule, physicalFilter, ruleManager) == false &&
				 (hasLaunchFilter == false ||
				  IsRuleMatchedOutboundPort(candidate, capsule, launchFilter, ruleManager) == false)))
			{
				continue;
			}

			int priority = GetRulePriority(candidate, ruleManager);
			if (port != null && priority <= bestPriority)
				continue;

			port = candidate;
			bestPriority = priority;
		}

		return port != null;
	}

	public bool TryFindRuleMatchedOutboundPort(
		uint buildingId,
		in FacilityFilter filter,
		out OutboundCargoPort port,
		Predicate<OutboundCargoPort> predicate = null)
	{
		port = null;
		if (buildingId == 0 || FacilityManager == null ||
			TryGetBuildingFacilities(buildingId, out IReadOnlyList<CargoPort> ports) == false)
		{
			return false;
		}

		FacilityRuleManager ruleManager = GameContext.HasInstance
			? GameContext.Instance.FacilityRuleMgr
			: null;
		if (ruleManager == null)
			return false;

		int bestPriority = int.MinValue;
		for (int i = 0; i < ports.Count; ++i)
		{
			if (ports[i] is not OutboundCargoPort candidate ||
				(predicate != null && predicate(candidate) == false) ||
				IsRuleMatchedOutboundPort(candidate, filter, ruleManager) == false)
			{
				continue;
			}

			int priority = GetRulePriority(candidate, ruleManager);
			if (port != null && priority <= bestPriority)
				continue;

			port = candidate;
			bestPriority = priority;
		}

		return port != null;
	}

	public bool TryFindDispatchPort(
		uint buildingId,
		CapsuleBuffer buffer,
		out OutboundCargoPort port,
		bool requireAvailable = true,
		Predicate<OutboundCargoPort> predicate = null)
	{
		port = null;
		if (buffer?.DockedCapsule == null || GameContext.HasInstance == false ||
			GameContext.Instance.BuildingMgr?.TryGetBuilding(buildingId, out Building building) != true ||
			building == null || building.IsOutboundThresholdReached(buffer) == false)
		{
			return false;
		}

		return TryFindRuleMatchedOutboundPort(
			buildingId,
			buffer.DockedCapsule,
			out port,
			requireAvailable,
			predicate);
	}

	private static bool TryBuildOutboundFilters(
		CargoCapsule capsule,
		out FacilityFilter physicalFilter,
		out FacilityFilter launchFilter,
		out bool hasLaunchFilter)
	{
		launchFilter = FacilityFilter.None;
		hasLaunchFilter = false;
		if (capsule == null || capsule.RouteKind != CargoRouteKind.Standard ||
			FacilityFilter.TryForCapsule(capsule, evaluateLaunchReadiness: false, out physicalFilter) == false)
		{
			physicalFilter = FacilityFilter.None;
			return false;
		}

		if (ItemProcessStageEvaluator.IsLaunchReady(
				capsule,
				GameContext.HasInstance ? GameContext.Instance.OBWorkflowSvc : null))
		{
			hasLaunchFilter = FacilityFilter.TryForCapsule(
				capsule,
				evaluateLaunchReadiness: true,
				out launchFilter);
		}

		return true;
	}

	private bool IsRuleMatchedOutboundPort(
		OutboundCargoPort port,
		CargoCapsule capsule,
		in FacilityFilter filter,
		FacilityRuleManager ruleManager)
	{
		return port != null &&
			capsule != null &&
			capsule.RouteKind == CargoRouteKind.Standard &&
			port.FacilityRulePresetId != FacilityRuleManager.NoRulePresetId &&
			port.CanAcceptCargoRoute(capsule.RouteKind) &&
			(FacilityManager == null || FacilityManager.IsInvalidating(port) == false) &&
			ruleManager != null &&
			ruleManager.TryGetPreset(port.FacilityRulePresetId, out FacilityRulePreset preset) &&
			preset?.Rule != null &&
			preset.Rule.IsEmpty == false &&
			preset.Rule.IsFilterCapable(filter);
	}

	private bool IsRuleMatchedOutboundPort(
		OutboundCargoPort port,
		in FacilityFilter filter,
		FacilityRuleManager ruleManager)
	{
		return port != null &&
			port.FacilityRulePresetId != FacilityRuleManager.NoRulePresetId &&
			(FacilityManager == null || FacilityManager.IsInvalidating(port) == false) &&
			ruleManager != null &&
			ruleManager.TryGetPreset(port.FacilityRulePresetId, out FacilityRulePreset preset) &&
			preset?.Rule != null &&
			preset.Rule.IsEmpty == false &&
			preset.Rule.IsFilterCapable(filter);
	}

	private static int GetRulePriority(OutboundCargoPort port, FacilityRuleManager ruleManager)
	{
		return port != null &&
			ruleManager != null &&
			ruleManager.TryGetPreset(port.FacilityRulePresetId, out FacilityRulePreset preset) &&
			preset?.Rule != null
			? preset.Rule.Priority
			: 0;
	}

	private static bool CanAcceptBox(CargoPort port, BoxBase box, InteractionKind interactionKind)
	{
		if (port == null || box is not CargoCapsule capsule || port.CanAcceptCargoRoute(capsule.RouteKind) == false)
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
