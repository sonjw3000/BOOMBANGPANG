using System;
using System.Collections.Generic;

public sealed class CapsuleDockService : FacilityService<CapsuleDock>
{
	private sealed class DockBucket
	{
		public readonly LinkedList<CapsuleDock> Docked = new();
		public readonly LinkedList<CapsuleDock> Undocked = new();
	}

	private readonly struct DockIndexEntry
	{
		public readonly uint BuildingId;
		public readonly CapsuleDockState DockState;
		public readonly bool HasCapsule;
		public readonly LinkedListNode<CapsuleDock> Node;

		public DockIndexEntry(
			uint buildingId,
			CapsuleDockState dockState,
			bool hasCapsule,
			LinkedListNode<CapsuleDock> node)
		{
			BuildingId = buildingId;
			DockState = dockState;
			HasCapsule = hasCapsule;
			Node = node;
		}
	}

	private readonly Dictionary<uint, Dictionary<CapsuleDockState, DockBucket>> docksByBuildingState = new();
	private readonly Dictionary<CapsuleDock, DockIndexEntry> indexByDock = new();

	public event Action<uint, CapsuleDock> OnCapsuleDocked;
	public event Action<uint, CapsuleDock> OnCapsuleUndocked;
	public event Action<uint, CapsuleDock> OnDockStateChanged;

	protected override void OnRegisterFacility(uint buildingId, CapsuleDock facility)
	{
		facility.OnCapsuleDocked += HandleCapsuleDocked;
		facility.OnCapsuleUndocked += HandleCapsuleUndocked;

		if (facility is CapsuleBuffer buffer)
			buffer.OnDockStateChanged += HandleBufferDockStateChanged;

		AddOrMoveDock(buildingId, facility);
		if (facility.HasCapsule)
			OnCapsuleDocked?.Invoke(buildingId, facility);
		else
			OnCapsuleUndocked?.Invoke(buildingId, facility);
	}

	protected override void OnUnregisterFacility(uint buildingId, CapsuleDock facility)
	{
		facility.OnCapsuleDocked -= HandleCapsuleDocked;
		facility.OnCapsuleUndocked -= HandleCapsuleUndocked;

		if (facility is CapsuleBuffer buffer)
			buffer.OnDockStateChanged -= HandleBufferDockStateChanged;

		RemoveDock(facility);
	}

	public bool TryFindDock(
		uint buildingId,
		CapsuleDockState dockState,
		bool hasCapsule,
		out CapsuleDock dock,
		Predicate<CapsuleDock> predicate = null)
	{
		Func<CapsuleDock, uint, bool> buildingPredicate = predicate != null
			? (candidate, _) => predicate(candidate)
			: null;

		return TryFindDock(
			buildingId,
			dockState,
			hasCapsule,
			out dock,
			out _,
			buildingPredicate);
	}

	public bool TryFindDock(
		uint buildingId,
		CapsuleDockState dockState,
		bool hasCapsule,
		out CapsuleDock dock,
		out uint foundBuildingId,
		Func<CapsuleDock, uint, bool> predicate = null)
	{
		dock = null;
		foundBuildingId = 0;
		if (buildingId != 0)
		{
			if (TryFindDockInBuilding(buildingId, dockState, hasCapsule, out dock, predicate) == false)
				return false;

			foundBuildingId = buildingId;
			return true;
		}

		foreach (uint candidateBuildingId in docksByBuildingState.Keys)
		{
			if (TryFindDockInBuilding(candidateBuildingId, dockState, hasCapsule, out dock, predicate))
			{
				foundBuildingId = candidateBuildingId;
				return true;
			}
		}

		return false;
	}

	public bool TryQueryDocks(
		uint buildingId,
		CapsuleDockState dockState,
		bool hasCapsule,
		List<CapsuleDock> results,
		Predicate<CapsuleDock> predicate = null)
	{
		if (results == null)
			return false;

		results.Clear();
		if (buildingId != 0)
		{
			AddDocks(buildingId, dockState, hasCapsule, results, predicate);
			return results.Count > 0;
		}

		foreach (uint candidateBuildingId in docksByBuildingState.Keys)
			AddDocks(candidateBuildingId, dockState, hasCapsule, results, predicate);

		return results.Count > 0;
	}

	private void HandleCapsuleDocked(CapsuleDock dock)
	{
		if (TryGetIndexedBuildingId(dock, out uint buildingId) == false)
			return;

		AddOrMoveDock(buildingId, dock);
		OnCapsuleDocked?.Invoke(buildingId, dock);
	}

	private void HandleCapsuleUndocked(CapsuleDock dock)
	{
		if (TryGetIndexedBuildingId(dock, out uint buildingId) == false)
			return;

		AddOrMoveDock(buildingId, dock);
		OnCapsuleUndocked?.Invoke(buildingId, dock);
	}

	private void HandleBufferDockStateChanged(CapsuleBuffer buffer)
	{
		if (TryGetIndexedBuildingId(buffer, out uint buildingId) == false)
			return;

		AddOrMoveDock(buildingId, buffer);
		OnDockStateChanged?.Invoke(buildingId, buffer);
	}

	private bool TryGetIndexedBuildingId(CapsuleDock dock, out uint buildingId)
	{
		if (dock != null && indexByDock.TryGetValue(dock, out DockIndexEntry entry))
		{
			buildingId = entry.BuildingId;
			return true;
		}

		buildingId = 0;
		return false;
	}

	private void AddOrMoveDock(uint buildingId, CapsuleDock dock)
	{
		if (dock == null)
			return;

		RemoveDock(dock);

		CapsuleDockState dockState = dock.DockState;
		bool hasCapsule = dock.HasCapsule;
		DockBucket bucket = GetBucket(buildingId, dockState);
		LinkedList<CapsuleDock> targetList = hasCapsule ? bucket.Docked : bucket.Undocked;
		LinkedListNode<CapsuleDock> node = targetList.AddLast(dock);
		indexByDock[dock] = new DockIndexEntry(buildingId, dockState, hasCapsule, node);
	}

	private void RemoveDock(CapsuleDock dock)
	{
		if (dock == null || indexByDock.TryGetValue(dock, out DockIndexEntry entry) == false)
			return;

		entry.Node.List?.Remove(entry.Node);
		indexByDock.Remove(dock);

		if (TryGetBucket(entry.BuildingId, entry.DockState, out DockBucket bucket))
			RemoveEmptyBucket(entry.BuildingId, entry.DockState, bucket);
	}

	private bool TryFindDockInBuilding(
		uint buildingId,
		CapsuleDockState dockState,
		bool hasCapsule,
		out CapsuleDock dock,
		Func<CapsuleDock, uint, bool> predicate)
	{
		dock = null;
		if (TryGetBucket(buildingId, dockState, out DockBucket bucket) == false)
			return false;

		LinkedList<CapsuleDock> candidates = hasCapsule ? bucket.Docked : bucket.Undocked;
		LinkedListNode<CapsuleDock> node = candidates.First;
		while (node != null)
		{
			CapsuleDock candidate = node.Value;
			if (candidate != null && (predicate == null || predicate(candidate, buildingId)))
			{
				dock = candidate;
				return true;
			}

			node = node.Next;
		}

		return false;
	}

	private void AddDocks(
		uint buildingId,
		CapsuleDockState dockState,
		bool hasCapsule,
		List<CapsuleDock> results,
		Predicate<CapsuleDock> predicate)
	{
		if (TryGetBucket(buildingId, dockState, out DockBucket bucket) == false)
			return;

		LinkedList<CapsuleDock> candidates = hasCapsule ? bucket.Docked : bucket.Undocked;
		LinkedListNode<CapsuleDock> node = candidates.First;
		while (node != null)
		{
			CapsuleDock candidate = node.Value;
			if (candidate != null && (predicate == null || predicate(candidate)))
				results.Add(candidate);

			node = node.Next;
		}
	}

	private DockBucket GetBucket(uint buildingId, CapsuleDockState dockState)
	{
		if (docksByBuildingState.TryGetValue(buildingId, out Dictionary<CapsuleDockState, DockBucket> stateBuckets) == false)
		{
			stateBuckets = new Dictionary<CapsuleDockState, DockBucket>();
			docksByBuildingState[buildingId] = stateBuckets;
		}

		if (stateBuckets.TryGetValue(dockState, out DockBucket bucket) == false)
		{
			bucket = new DockBucket();
			stateBuckets[dockState] = bucket;
		}

		return bucket;
	}

	private bool TryGetBucket(uint buildingId, CapsuleDockState dockState, out DockBucket bucket)
	{
		bucket = null;
		return docksByBuildingState.TryGetValue(buildingId, out Dictionary<CapsuleDockState, DockBucket> stateBuckets) &&
			stateBuckets.TryGetValue(dockState, out bucket);
	}

	private void RemoveEmptyBucket(uint buildingId, CapsuleDockState dockState, DockBucket bucket)
	{
		if (bucket.Docked.Count > 0 || bucket.Undocked.Count > 0)
			return;

		if (docksByBuildingState.TryGetValue(buildingId, out Dictionary<CapsuleDockState, DockBucket> stateBuckets) == false)
			return;

		stateBuckets.Remove(dockState);
		if (stateBuckets.Count <= 0)
			docksByBuildingState.Remove(buildingId);
	}
}
