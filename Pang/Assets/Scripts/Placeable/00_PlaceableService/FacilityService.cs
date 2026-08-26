using UnityEngine;
using System.Collections.Generic;
using System;
using Unity.Mathematics;

public class FacilityService<T> : MonoBehaviour where T : class, IFacility
{
	protected readonly Dictionary<uint, List<T>> registeredFacilities = new();
	// Unbind from the exact owners used during binding so teardown never depends on GameContext ordering.
	private FacilityManager boundFacilityManager;
	private GridService boundGridService;

	protected delegate bool FacilityDistanceResolver(T facility, in int3 from, out int score);

	protected FacilityManager FacilityManager => boundFacilityManager;
	protected GridService GridService => boundGridService;

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

	protected virtual void OnEnable()
	{
		TryBindFacilityManager();
	}

	protected virtual void Start()
	{
		TryBindFacilityManager();
	}

	protected virtual void OnDisable()
	{
		UnbindFacilityManager();
	}

	public bool TryFindDestination(
		uint buildingId,
		in int3 from,
		InteractionKind interactionKind,
		FacilityFilter facilityFilter,
		out T facility)
	{
		return TryFindDestination(buildingId, from, interactionKind, facilityFilter, out facility, null);
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
		FacilityFilter facilityFilter)
	{
		if (facility == null)
			return false;

		if (buildingId != 0)
		{
			if (TryGetBuildingId(facility, out uint facilityBuildingId) == false || facilityBuildingId != buildingId)
				return false;
		}

		return facilityFilter.MatchesCurrentRules(facility);
	}

	protected virtual bool TryGetDestinationScore(
		T facility,
		uint buildingId,
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
		if (FacilityManager == null)
			return;

		RemoveStaleRegisteredFacilities();

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

	private void TryBindFacilityManager()
	{
		if (boundFacilityManager != null)
			return;

		if (GameContext.HasInstance == false)
			return;

		GameContext context = GameContext.Instance;
		FacilityManager facilityManager = context.FacilityMgr;
		if (facilityManager == null)
			return;

		boundFacilityManager = facilityManager;
		boundGridService = context.GridService;
		boundFacilityManager.SubscribeFacilityRegister<T>(HandleFacilityRegistered, HandleFacilityUnregistered);
		RebuildRegisteredFacilities();
	}

	private void UnbindFacilityManager()
	{
		if (boundFacilityManager != null)
		{
			boundFacilityManager.UnsubscribeFacilityRegister<T>(HandleFacilityRegistered, HandleFacilityUnregistered);
		}

		boundFacilityManager = null;
		boundGridService = null;
	}

	private void RemoveStaleRegisteredFacilities()
	{
		List<(uint BuildingId, T Facility)> staleFacilities = new();
		foreach (var buildingEntry in registeredFacilities)
		{
			uint buildingId = buildingEntry.Key;
			IReadOnlyList<T> currentFacilities = FacilityManager.GetFacilities<T>(buildingId);
			List<T> cachedFacilities = buildingEntry.Value;
			for (int i = 0; i < cachedFacilities.Count; ++i)
			{
				T cachedFacility = cachedFacilities[i];
				if (ContainsFacility(currentFacilities, cachedFacility) == false)
					staleFacilities.Add((buildingId, cachedFacility));
			}
		}

		for (int i = 0; i < staleFacilities.Count; ++i)
		{
			(uint buildingId, T facility) = staleFacilities[i];
			UnregisterFacility(buildingId, facility);
		}
	}

	private static bool ContainsFacility(IReadOnlyList<T> facilities, T target)
	{
		if (facilities == null)
			return false;

		for (int i = 0; i < facilities.Count; ++i)
		{
			if (EqualityComparer<T>.Default.Equals(facilities[i], target))
				return true;
		}

		return false;
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

		if (!registeredFacilities.TryGetValue(buildingId, out List<T> facilities))
		{
			facilities = new List<T>();
			registeredFacilities[buildingId] = facilities;
		}

		if (ContainsFacility(facilities, facility))
			return;

		facilities.Add(facility);

		OnRegisterFacility(buildingId, facility);
	}

	private void UnregisterFacility(uint buildingId, T facility)
	{
		if (facility == null)
			return;

		if (registeredFacilities.TryGetValue(buildingId, out List<T> facilities) == false ||
			facilities.Remove(facility) == false)
			return;

		if (facilities.Count == 0)
			registeredFacilities.Remove(buildingId);

		OnUnregisterFacility(buildingId, facility);
	}

	protected virtual void OnRegisterFacility(uint buildingId, T facility) { }
	protected virtual void OnUnregisterFacility(uint buildingId, T facility) { }

	protected bool TryFindDestination(
		uint buildingId,
		in int3 from,
		InteractionKind interactionKind,
		FacilityFilter facilityFilter,
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
				facilityFilter,
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
				facilityFilter,
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
		FacilityFilter facilityFilter,
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

			if (IsDestinationCandidate(candidate, buildingId, interactionKind, facilityFilter) == false)
				continue;

			if (TryGetDestinationScore(candidate, buildingId, from, interactionKind, out int candidateScore) == false)
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
