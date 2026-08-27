using System;
using UnityEngine;
using System.Collections.Generic;

public sealed class CapsuleBufferService : FacilityService<CapsuleBuffer>
{
	// capsule buffer management
	[SerializeField] private int capsulePurchaseCost = 100;

	private readonly Dictionary<uint, List<CapsuleBuffer>> registeredBuffers = new();
	private readonly Dictionary<CapsuleBuffer, uint> registeredBuildingIdByBuffer = new();
	public event Action<uint, CapsuleBuffer> OnCapsuleContentChanged;
	public event Action<uint, CapsuleBuffer> OnEmptyCapsuleRetentionChanged;
	public int CapsulePurchaseCost => Mathf.Max(0, capsulePurchaseCost);

	public bool CanPurchaseCapsule(CapsuleBuffer buffer)
	{
		if (buffer == null ||
			buffer.RetainEmptyCapsule == false ||
			buffer.HasCapsule ||
			GameContext.HasInstance == false)
			return false;

		GameContext context = GameContext.Instance;
		return context.BoxMgr != null &&
			context.EconomyService != null &&
			context.EconomyService.CanAfford(CapsulePurchaseCost);
	}

	public bool TryPurchaseCapsule(CapsuleBuffer buffer)
	{
		if (CanPurchaseCapsule(buffer) == false)
			return false;

		GameContext context = GameContext.Instance;
		if (context.BoxMgr.GetNewBox(BoxType.Capsule, out BoxBase box) == false)
			return false;
		if (box is not CargoCapsule capsule)
		{
			context.BoxMgr.DisableBox(box);
			return false;
		}

		capsule.SetLogisticsState(CapsuleLogisticsState.Empty);
		if (buffer.TryDockCapsule(capsule) == false)
		{
			context.BoxMgr.DisableBox(capsule);
			return false;
		}

		context.EconomyService.ApplyTransaction(new EconomyTransaction
		{
			moneyDelta = -CapsulePurchaseCost,
			reputationDelta = 0f,
			reason = EconomyTransaction.Reason.CapsulePurchase,
		});
		return true;
	}

	public bool TrySetRetainEmptyCapsule(CapsuleBuffer buffer, bool retain)
	{
		if (buffer == null)
			return false;
		if (buffer.SetRetainEmptyCapsule(retain) == false)
			return true;

		if (TryGetRegisteredBuildingId(buffer, out uint buildingId))
			OnEmptyCapsuleRetentionChanged?.Invoke(buildingId, buffer);

		return true;
	}

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
			InteractionKind.Put => facility.CanReceiveCapsule(),
			InteractionKind.Pick => facility.CanDispatchToOutbound(),
			_ => false,
		};
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

	public bool IsExplicitRuleMatchedBuffer(
		CapsuleBuffer buffer,
		CargoCapsule capsule,
		FacilityContentState requiredContentState,
		ItemProcessStage requiredItemProcessStage,
		bool evaluateLaunchReadiness)
	{
		if (buffer == null ||
			capsule == null ||
			buffer.FacilityRulePresetId == FacilityRuleManager.NoRulePresetId)
		{
			return false;
		}

		FacilityRuleManager ruleManager = GameContext.HasInstance
			? GameContext.Instance.FacilityRuleMgr
			: null;
		return ruleManager != null &&
			ruleManager.TryGetPreset(buffer.FacilityRulePresetId, out FacilityRulePreset preset) &&
			preset?.Rule?.RequiredContentState == requiredContentState &&
			preset.Rule.AllowsItemProcessStage(requiredItemProcessStage) &&
			IsRuleMatchedBuffer(buffer, capsule, evaluateLaunchReadiness);
	}

	public bool IsExplicitRuleMatchedBuffer(
		CapsuleBuffer buffer,
		in FacilityFilter projectedFilter,
		FacilityContentState requiredContentState,
		ItemProcessStage requiredItemProcessStage)
	{
		if (buffer == null ||
			buffer.FacilityRulePresetId == FacilityRuleManager.NoRulePresetId ||
			projectedFilter.ContentState != requiredContentState ||
			projectedFilter.ItemProcessStage != requiredItemProcessStage)
		{
			return false;
		}

		FacilityRuleManager ruleManager = GameContext.HasInstance
			? GameContext.Instance.FacilityRuleMgr
			: null;
		return ruleManager != null &&
			ruleManager.TryGetPreset(buffer.FacilityRulePresetId, out FacilityRulePreset preset) &&
			preset?.Rule?.RequiredContentState == requiredContentState &&
			preset.Rule.AllowsItemProcessStage(requiredItemProcessStage) &&
			projectedFilter.Matches(ruleManager, buffer);
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
		if (buffer == null ||
			capsule == null ||
			buffer.FacilityRulePresetId == FacilityRuleManager.NoRulePresetId ||
			ruleManager == null ||
			ruleManager.TryGetPreset(buffer.FacilityRulePresetId, out FacilityRulePreset preset) == false ||
			preset?.Rule == null ||
			preset.Rule.IsEmpty)
		{
			return false;
		}

		return
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
