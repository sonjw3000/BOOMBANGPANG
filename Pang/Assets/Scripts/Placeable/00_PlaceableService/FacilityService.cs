using UnityEngine;
using System.Collections.Generic;
using System;
using Unity.Mathematics;

public class FacilityService<T> : MonoBehaviour where T : class, IFacility
{
	protected readonly Dictionary<uint, List<T>> registeredFacilities = new();

	protected delegate bool FacilityDistanceResolver(T facility, in int3 from, out int score);

	protected FacilityManager FacilityManager => GameContext.Instance.FacilityMgr;
	protected GridService GridService => GameContext.Instance.GridService;
	protected ZoneManager ZoneManager => GameContext.Instance.ZoneMgr;

	protected IReadOnlyList<T> BuildingFacilities(uint buildingId) => FacilityManager.GetFacilities<T>(buildingId);
	protected bool TryGetBuildingFacilities(uint buildingId, out IReadOnlyList<T> facilities) => FacilityManager.TryGetFacilities(buildingId, out facilities);

	protected bool TryQueryFacilities(uint buildingId, List<T> results, Predicate<T> predicate = null)
	{
		if (results == null)
			return false;

		results.Clear();
		if (TryGetBuildingFacilities(buildingId, out var facilities) == false)
			return false;

		for (int i = 0; i < facilities.Count; ++i)
		{
			T facility = facilities[i];
			if (facility == null)
				continue;

			if (predicate != null && predicate(facility) == false)
				continue;

			results.Add(facility);
		}

		return results.Count > 0;
	}

	protected bool TryFindFacility(uint buildingId, Predicate<T> predicate, out T facility)
	{
		facility = null;
		if (TryGetBuildingFacilities(buildingId, out var facilities) == false)
			return false;

		for (int i = 0; i < facilities.Count; ++i)
		{
			T candidate = facilities[i];
			if (candidate == null)
				continue;

			if (predicate != null && predicate(candidate) == false)
				continue;

			facility = candidate;
			return true;
		}

		return false;
	}

	protected bool TryFindClosestFacility(
		uint buildingId,
		in int3 from,
		FacilityDistanceResolver distanceResolver,
		out T facility,
		Predicate<T> predicate = null)
	{
		facility = null;
		if (distanceResolver == null || TryGetBuildingFacilities(buildingId, out var facilities) == false)
			return false;

		return TryFindClosestFacility(facilities, from, distanceResolver, out facility, predicate);
	}

	protected bool TryFindClosestFacility(
		in int3 from,
		FacilityDistanceResolver distanceResolver,
		out T facility,
		Predicate<T> predicate = null)
	{
		facility = null;
		if (distanceResolver == null)
			return false;

		bool found = false;
		int bestScore = int.MaxValue;

		IReadOnlyList<uint> buildingIds = FacilityManager.GetBuildingIds();
		for (int i = 0; i < buildingIds.Count; ++i)
		{
			if (TryGetBuildingFacilities(buildingIds[i], out var facilities) == false)
				continue;

			if (TryFindClosestFacility(facilities, from, distanceResolver, out T candidate, predicate, out int candidateScore) == false)
				continue;

			if (found && candidateScore >= bestScore)
				continue;

			found = true;
			bestScore = candidateScore;
			facility = candidate;
		}

		return found;
	}

	protected virtual void Start()
	{
		FacilityManager.SubscribeFacilityRegister<T>(HandleFacilityRegistered, HandleFacilityUnregistered);
		RebuildRegisteredFacilities();
	}

	protected virtual void OnDestroy()
	{
		FacilityManager.UnsubscribeFacilityRegister<T>(HandleFacilityRegistered, HandleFacilityUnregistered);
	}

	public bool TryFindDestination(
		uint buildingId,
		in int3 from,
		InteractionKind interactionKind,
		ZoneFilter zoneFilter,
		out T facility)
	{
		return TryFindDestination(buildingId, from, interactionKind, zoneFilter, out facility, null);
	}

	protected bool TryGetBuildingId(IFacility facility, out uint buildingId)
	{
		if (facility == null)
		{
			buildingId = 0;
			return false;
		}

		return TryGetBuildingId(facility.GridPosition, out buildingId);
	}

	protected bool TryGetBuildingId(in int3 position, out uint buildingId)
	{
		GridCell cell = GridService?.GetCell(position);
		buildingId = cell != null ? cell.BuildingId : 0;
		return buildingId != 0;
	}

	protected virtual bool IsDestinationCandidate(
		T facility,
		uint buildingId,
		InteractionKind interactionKind,
		ZoneFilter zoneFilter)
	{
		if (facility == null)
			return false;

		if (buildingId != 0)
		{
			if (TryGetBuildingId(facility, out uint facilityBuildingId) == false || facilityBuildingId != buildingId)
				return false;
		}

		return zoneFilter.Matches(ZoneManager, facility);
	}

	protected virtual bool TryGetDestinationScore(
		T facility,
		in int3 from,
		InteractionKind interactionKind,
		out int score)
	{
		score = int.MaxValue;
		return facility is IInteractionPoint interactionPoint
			&& InteractionPointSelector.TryGetInteractionPoint(
				interactionPoint,
				interactionKind,
				from,
				out _,
				out score);
	}

	private void RebuildRegisteredFacilities()
	{
		IReadOnlyList<uint> buildingIds = FacilityManager.GetBuildingIds();
		for (int i = 0; i < buildingIds.Count; ++i)
		{
			uint buildingId = buildingIds[i];
			if (TryGetBuildingFacilities(buildingId, out var facilities) == false)
				continue;

			for (int facilityIndex = 0; facilityIndex < facilities.Count; ++facilityIndex)
				RegisterFacility(buildingId, facilities[facilityIndex]);
		}
	}

	private void HandleFacilityRegistered(uint buildingId, IFacility facility)
	{
		if (facility is T typedFacility)
			RegisterFacility(buildingId, typedFacility);
	}

	private void HandleFacilityUnregistered(uint buildingId, IFacility facility)
	{
		if (facility is T typedFacility)
			UnregisterFacility(buildingId, typedFacility);
	}

	private static bool TryFindClosestFacility(
		IReadOnlyList<T> facilities,
		in int3 from,
		FacilityDistanceResolver distanceResolver,
		out T facility,
		Predicate<T> predicate,
		out int bestScore)
	{
		facility = null;
		bestScore = int.MaxValue;

		for (int i = 0; i < facilities.Count; ++i)
		{
			T candidate = facilities[i];
			if (candidate == null)
				continue;

			if (predicate != null && predicate(candidate) == false)
				continue;

			if (distanceResolver(candidate, from, out int score) == false || score >= bestScore)
				continue;

			bestScore = score;
			facility = candidate;
		}

		return facility != null;
	}

	private static bool TryFindClosestFacility(
		IReadOnlyList<T> facilities,
		in int3 from,
		FacilityDistanceResolver distanceResolver,
		out T facility,
		Predicate<T> predicate)
	{
		return TryFindClosestFacility(facilities, from, distanceResolver, out facility, predicate, out _);
	}
	
	private void RegisterFacility(uint buildingId, T facility)
	{
		if (facility == null)
			return;

		if (!registeredFacilities.ContainsKey(buildingId))
		{
			registeredFacilities[buildingId] = new List<T>();
		}
		registeredFacilities[buildingId].Add(facility);

		OnRegisterFacility(buildingId, facility);
	}

	private void UnregisterFacility(uint buildingId, T facility)
	{
		if (facility == null)
			return;

		if (registeredFacilities.ContainsKey(buildingId))
		{
			registeredFacilities[buildingId].Remove(facility);
		}

		OnUnregisterFacility(buildingId, facility);
	}

	protected virtual void OnRegisterFacility(uint buildingId, T facility) { }
	protected virtual void OnUnregisterFacility(uint buildingId, T facility) { }

	protected bool TryFindDestination(
		uint buildingId,
		in int3 from,
		InteractionKind interactionKind,
		ZoneFilter zoneFilter,
		out T facility,
		Predicate<T> predicate)
	{
		facility = null;
		bool found = false;
		int bestScore = int.MaxValue;

		if (buildingId != 0)
		{
			if (TryGetBuildingFacilities(buildingId, out var facilities) == false)
				return false;

			return TryFindDestination(
				facilities,
				buildingId,
				from,
				interactionKind,
				zoneFilter,
				predicate,
				ref facility,
				ref found,
				ref bestScore);
		}

		IReadOnlyList<uint> buildingIds = FacilityManager.GetBuildingIds();
		for (int i = 0; i < buildingIds.Count; ++i)
		{
			if (TryGetBuildingFacilities(buildingIds[i], out var facilities) == false)
				continue;

			TryFindDestination(
				facilities,
				buildingIds[i],
				from,
				interactionKind,
				zoneFilter,
				predicate,
				ref facility,
				ref found,
				ref bestScore);
		}

		return found;
	}

	private bool TryFindDestination(
		IReadOnlyList<T> facilities,
		uint buildingId,
		in int3 from,
		InteractionKind interactionKind,
		ZoneFilter zoneFilter,
		Predicate<T> predicate,
		ref T bestFacility,
		ref bool found,
		ref int bestScore)
	{
		if (facilities == null)
			return false;

		for (int i = 0; i < facilities.Count; ++i)
		{
			T candidate = facilities[i];
			if (predicate != null && predicate(candidate) == false)
				continue;

			if (IsDestinationCandidate(candidate, buildingId, interactionKind, zoneFilter) == false)
				continue;

			if (TryGetDestinationScore(candidate, from, interactionKind, out int candidateScore) == false)
				continue;

			if (found && candidateScore >= bestScore)
				continue;

			bestFacility = candidate;
			bestScore = candidateScore;
			found = true;
		}

		return found;
	}
}
