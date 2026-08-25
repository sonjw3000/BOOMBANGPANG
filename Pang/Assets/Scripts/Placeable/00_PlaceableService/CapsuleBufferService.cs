using System;
using UnityEngine;
using System.Collections.Generic;

public sealed class CapsuleBufferService : FacilityService<CapsuleBuffer>
{
	// capsule buffer management

	private readonly Dictionary<uint, List<CapsuleBuffer>> registeredBuffers = new();
	private readonly Dictionary<CapsuleBuffer, uint> registeredBuildingIdByBuffer = new();
	public event Action<uint, CapsuleBuffer> OnCapsuleContentChanged;

	protected override bool IsDestinationCandidate(
		CapsuleBuffer facility,
		uint buildingId,
		InteractionKind interactionKind,
		FacilityFilter facilityFilter)
	{
		if (base.IsDestinationCandidate(facility, buildingId, interactionKind, facilityFilter) == false)
			return false;

		return interactionKind switch
		{
			InteractionKind.Put => facility.CanReceiveFromInbound(),
			InteractionKind.Pick => facility.CanDispatchToOutbound(),
			_ => false,
		};
	}

	public bool SetDockState(CapsuleBuffer facility, CapsuleDockState newState)
	{
		if (facility == null)
			return false;

		facility.SetDockState(newState);
		return true;
	}

	public bool TryQueryRuleMatchedDestinations(
		uint buildingId,
		CargoCapsule capsule,
		List<CapsuleBuffer> results,
		bool evaluateLaunchReadiness,
		Predicate<CapsuleBuffer> predicate = null)
	{
		if (results == null)
			return false;

		results.Clear();
		if (capsule == null ||
			FacilityFilter.TryForCapsule(
				capsule,
				evaluateLaunchReadiness,
				out FacilityFilter filter) == false)
		{
			return false;
		}

		FacilityRuleManager ruleManager = GameContext.HasInstance
			? GameContext.Instance.FacilityRuleMgr
			: null;
		FacilityManager facilityManager = FacilityManager;
		if (ruleManager == null || facilityManager == null)
			return false;

		foreach (CapsuleBuffer candidate in GetBuffers(buildingId))
		{
			if (IsRuleMatchedBuffer(candidate, capsule, filter, ruleManager, facilityManager) == false ||
				candidate.CanPutBox() == false ||
				(predicate != null && predicate(candidate) == false))
			{
				continue;
			}

			InsertByRulePriority(results, candidate, ruleManager);
		}

		return results.Count > 0;
	}

	public bool IsRuleMatchedBuffer(
		CapsuleBuffer buffer,
		CargoCapsule capsule,
		bool evaluateLaunchReadiness)
	{
		if (buffer == null || capsule == null ||
			FacilityFilter.TryForCapsule(capsule, evaluateLaunchReadiness, out FacilityFilter filter) == false)
		{
			return false;
		}

		FacilityRuleManager ruleManager = GameContext.HasInstance
			? GameContext.Instance.FacilityRuleMgr
			: null;
		return IsRuleMatchedBuffer(buffer, capsule, filter, ruleManager, FacilityManager);
	}

	public bool TryGetRegisteredBuildingId(CapsuleBuffer buffer, out uint buildingId)
	{
		if (buffer != null && registeredBuildingIdByBuffer.TryGetValue(buffer, out buildingId))
			return true;

		buildingId = 0;
		return false;
	}

	public IEnumerable<CapsuleBuffer> GetBuffers(uint buildingId = 0)
	{
		if (buildingId != 0)
		{
			if (registeredBuffers.TryGetValue(buildingId, out List<CapsuleBuffer> buffers) == false)
				yield break;

			for (int i = 0; i < buffers.Count; ++i)
			{
				if (buffers[i] != null)
					yield return buffers[i];
			}

			yield break;
		}

		foreach (var kvp in registeredBuffers)
		{
			List<CapsuleBuffer> buffers = kvp.Value;
			if (buffers == null)
				continue;

			for (int i = 0; i < buffers.Count; ++i)
			{
				if (buffers[i] != null)
					yield return buffers[i];
			}
		}
	}

	protected override void OnRegisterFacility(uint buildingId, CapsuleBuffer facility)
	{
		if (registeredBuffers.TryGetValue(buildingId, out List<CapsuleBuffer> buffers) == false)
		{
			buffers = new List<CapsuleBuffer>();
			registeredBuffers.Add(buildingId, buffers);
		}

		if (buffers.Contains(facility) == false)
			buffers.Add(facility);
		registeredBuildingIdByBuffer[facility] = buildingId;

		facility.OnCapsuleContentChanged += HandleCapsuleContentChanged;
	}

	protected override void OnUnregisterFacility(uint buildingId, CapsuleBuffer facility)
	{
		if (facility != null)
		{
			facility.OnCapsuleContentChanged -= HandleCapsuleContentChanged;
			if (registeredBuildingIdByBuffer.TryGetValue(facility, out uint registeredBuildingId) &&
				registeredBuildingId == buildingId)
			{
				registeredBuildingIdByBuffer.Remove(facility);
			}
		}

		if (registeredBuffers.TryGetValue(buildingId, out List<CapsuleBuffer> buffers) == false)
			return;

		buffers.Remove(facility);
		if (buffers.Count <= 0)
			registeredBuffers.Remove(buildingId);
	}

	private static bool IsRuleMatchedBuffer(
		CapsuleBuffer buffer,
		CargoCapsule capsule,
		FacilityFilter filter,
		FacilityRuleManager ruleManager,
		FacilityManager facilityManager)
	{
		return buffer != null &&
			capsule != null &&
			buffer.FacilityRulePresetId != FacilityRuleManager.NoRulePresetId &&
			buffer.CanAcceptCargoRoute(capsule.RouteKind) &&
			(facilityManager == null || facilityManager.IsInvalidating(buffer) == false) &&
			filter.Matches(ruleManager, buffer);
	}

	private static void InsertByRulePriority(
		List<CapsuleBuffer> results,
		CapsuleBuffer candidate,
		FacilityRuleManager ruleManager)
	{
		int candidatePriority = GetRulePriority(candidate, ruleManager);
		int insertIndex = results.Count;
		for (int i = 0; i < results.Count; ++i)
		{
			if (candidatePriority <= GetRulePriority(results[i], ruleManager))
				continue;

			insertIndex = i;
			break;
		}

		results.Insert(insertIndex, candidate);
	}

	private static int GetRulePriority(CapsuleBuffer buffer, FacilityRuleManager ruleManager)
	{
		return buffer != null &&
			ruleManager != null &&
			ruleManager.TryGetPreset(buffer.FacilityRulePresetId, out FacilityRulePreset preset) &&
			preset?.Rule != null
			? preset.Rule.Priority
			: 0;
	}

	private void HandleCapsuleContentChanged(CapsuleBuffer buffer)
	{
		if (TryGetRegisteredBuildingId(buffer, out uint buildingId))
			OnCapsuleContentChanged?.Invoke(buildingId, buffer);
	}
}
