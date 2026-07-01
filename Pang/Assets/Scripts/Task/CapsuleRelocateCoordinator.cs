using System;
using System.Collections.Generic;

public enum CapsuleRelocateRouteKind
{
	InboundReceive,
	CapsuleClear,
	CapsuleSupply,
	OutboundDispatch,
	CargoTransfer,
}

public enum CapsuleRelocateScope
{
	SameBuilding,
	LinkedBuilding,
	GlobalAllowed,
}

public readonly struct CapsuleRelocateSendRequest
{
	public readonly CapsuleDock SourceDock;
	public readonly CapsuleDockState WantedTargetDockState;
	public readonly CapsuleRelocateRouteKind RouteKind;
	public readonly CapsuleRelocateScope Scope;
	public readonly CapsuleDockState RequiredSourceDockState;
	public readonly CapsuleLogisticsState RequiredCapsuleState;
	public readonly uint SourceBuildingId;
	public readonly uint RequiredTargetBuildingId;
	public readonly int Priority;

	public CapsuleRelocateSendRequest(
		CapsuleDock sourceDock,
		CapsuleDockState wantedTargetDockState,
		CapsuleRelocateRouteKind routeKind,
		CapsuleRelocateScope scope,
		CapsuleDockState requiredSourceDockState,
		CapsuleLogisticsState requiredCapsuleState,
		uint sourceBuildingId,
		uint requiredTargetBuildingId = 0,
		int priority = 0)
	{
		SourceDock = sourceDock;
		WantedTargetDockState = wantedTargetDockState;
		RouteKind = routeKind;
		Scope = scope;
		RequiredSourceDockState = requiredSourceDockState;
		RequiredCapsuleState = requiredCapsuleState;
		SourceBuildingId = sourceBuildingId;
		RequiredTargetBuildingId = requiredTargetBuildingId;
		Priority = priority;
	}
}

public readonly struct CapsuleRelocateDemand
{
	public readonly CapsuleDock TargetDock;
	public readonly CapsuleDockState RequiredSourceDockState;
	public readonly CapsuleLogisticsState RequiredCapsuleState;
	public readonly CapsuleRelocateRouteKind RouteKind;
	public readonly CapsuleRelocateScope Scope;
	public readonly uint TargetBuildingId;
	public readonly uint RequiredSourceBuildingId;
	public readonly int Priority;

	public CapsuleRelocateDemand(
		CapsuleDock targetDock,
		CapsuleDockState requiredSourceDockState,
		CapsuleLogisticsState requiredCapsuleState,
		CapsuleRelocateRouteKind routeKind,
		CapsuleRelocateScope scope,
		uint targetBuildingId,
		uint requiredSourceBuildingId = 0,
		int priority = 0)
	{
		TargetDock = targetDock;
		RequiredSourceDockState = requiredSourceDockState;
		RequiredCapsuleState = requiredCapsuleState;
		RouteKind = routeKind;
		Scope = scope;
		TargetBuildingId = targetBuildingId;
		RequiredSourceBuildingId = requiredSourceBuildingId;
		Priority = priority;
	}
}

public readonly struct CapsuleRelocateMatch
{
	public readonly CapsuleRelocateRouteKind RouteKind;
	public readonly CapsuleDock SourceDock;
	public readonly CapsuleDock TargetDock;
	public readonly uint SourceBuildingId;
	public readonly uint TargetBuildingId;

	public CapsuleRelocateMatch(
		CapsuleRelocateRouteKind routeKind,
		CapsuleDock sourceDock,
		CapsuleDock targetDock,
		uint sourceBuildingId,
		uint targetBuildingId)
	{
		RouteKind = routeKind;
		SourceDock = sourceDock;
		TargetDock = targetDock;
		SourceBuildingId = sourceBuildingId;
		TargetBuildingId = targetBuildingId;
	}
}

public sealed class CapsuleRelocateCoordinator
{
	private readonly Dictionary<uint, Dictionary<CapsuleDockState, LinkedList<CapsuleRelocateSendRequest>>> sameBuildingSends = new();
	private readonly Dictionary<CapsuleDockState, LinkedList<CapsuleRelocateSendRequest>> linkedBuildingSends = new();
	private readonly Dictionary<CapsuleDockState, LinkedList<CapsuleRelocateSendRequest>> globalSends = new();
	private readonly Dictionary<CapsuleDock, LinkedListNode<CapsuleRelocateSendRequest>> sendNodeBySource = new();

	private readonly Dictionary<uint, Dictionary<CapsuleDockState, LinkedList<CapsuleRelocateDemand>>> sameBuildingDemands = new();
	private readonly Dictionary<CapsuleDockState, LinkedList<CapsuleRelocateDemand>> linkedBuildingDemands = new();
	private readonly Dictionary<CapsuleDockState, LinkedList<CapsuleRelocateDemand>> globalDemands = new();
	private readonly Dictionary<CapsuleDock, LinkedListNode<CapsuleRelocateDemand>> demandNodeByTarget = new();

	private readonly Dictionary<CapsuleDock, uint> availableReceivers = new();
	private readonly Dictionary<CapsuleDock, uint> availableSources = new();
	private readonly HashSet<CapsuleDock> reservedDocks = new();
	private readonly Func<uint, uint, bool> canUseLinkedBuilding;

	public IReadOnlyDictionary<CapsuleDock, uint> AvailableReceivers => availableReceivers;
	public IReadOnlyDictionary<CapsuleDock, uint> AvailableSources => availableSources;
	public int PendingSendCount => sendNodeBySource.Count;
	public int PendingDemandCount => demandNodeByTarget.Count;

	public CapsuleRelocateCoordinator(Func<uint, uint, bool> canUseLinkedBuilding = null)
	{
		this.canUseLinkedBuilding = canUseLinkedBuilding;
	}

	public void RegisterReceiver(CapsuleDock dock, uint buildingId)
	{
		if (dock == null)
			return;

		availableReceivers[dock] = buildingId;
	}

	public void RegisterSource(CapsuleDock dock, uint buildingId)
	{
		if (dock == null)
			return;

		availableSources[dock] = buildingId;
	}

	public void UnregisterDock(CapsuleDock dock)
	{
		if (dock == null)
			return;

		availableReceivers.Remove(dock);
		availableSources.Remove(dock);
		reservedDocks.Remove(dock);
		RemoveSendRequests(dock);
		RemoveDemands(dock);
	}

	public bool RequestSend(CapsuleRelocateSendRequest request, out CapsuleRelocateMatch match)
	{
		match = default;
		if (IsSendSourceValid(request) == false)
			return false;

		if (TryFindReceiver(request, out CapsuleDock targetDock, out uint targetBuildingId))
		{
			Reserve(request.SourceDock, targetDock);
			match = new CapsuleRelocateMatch(request.RouteKind, request.SourceDock, targetDock, request.SourceBuildingId, targetBuildingId);
			return true;
		}

		AddPendingSend(request);
		return false;
	}

	public bool RequestDemand(CapsuleRelocateDemand demand, out CapsuleRelocateMatch match)
	{
		match = default;
		if (IsDemandTargetValid(demand) == false)
			return false;

		if (TryFindSource(demand, out CapsuleDock sourceDock, out uint sourceBuildingId))
		{
			Reserve(sourceDock, demand.TargetDock);
			match = new CapsuleRelocateMatch(demand.RouteKind, sourceDock, demand.TargetDock, sourceBuildingId, demand.TargetBuildingId);
			return true;
		}

		AddPendingDemand(demand);
		return false;
	}

	public void ReleaseReservation(CapsuleDock sourceDock, CapsuleDock targetDock)
	{
		if (sourceDock != null)
			reservedDocks.Remove(sourceDock);
		if (targetDock != null)
			reservedDocks.Remove(targetDock);
	}

	public bool IsReserved(CapsuleDock dock)
	{
		return dock != null && reservedDocks.Contains(dock);
	}

	public bool TryMatchPending(out CapsuleRelocateMatch match)
	{
		if (TryMatchPendingSend(out match))
			return true;

		return TryMatchPendingDemand(out match);
	}

	private bool TryMatchPendingSend(out CapsuleRelocateMatch match)
	{
		match = default;
		return TryMatchPendingSendBuckets(sameBuildingSends, out match) ||
			TryMatchPendingSendBuckets(linkedBuildingSends, out match) ||
			TryMatchPendingSendBuckets(globalSends, out match);
	}

	private bool TryMatchPendingDemand(out CapsuleRelocateMatch match)
	{
		match = default;
		return TryMatchPendingDemandBuckets(sameBuildingDemands, out match) ||
			TryMatchPendingDemandBuckets(linkedBuildingDemands, out match) ||
			TryMatchPendingDemandBuckets(globalDemands, out match);
	}

	private bool TryFindReceiver(CapsuleRelocateSendRequest request, out CapsuleDock targetDock, out uint targetBuildingId)
	{
		targetDock = null;
		targetBuildingId = 0;

		foreach (var (candidate, buildingId) in availableReceivers)
		{
			if (CanMatch(request, candidate, buildingId) == false)
				continue;

			targetDock = candidate;
			targetBuildingId = buildingId;
			return true;
		}

		return false;
	}

	private bool TryFindSource(CapsuleRelocateDemand demand, out CapsuleDock sourceDock, out uint sourceBuildingId)
	{
		sourceDock = null;
		sourceBuildingId = 0;

		foreach (var (candidate, buildingId) in availableSources)
		{
			if (CanMatch(candidate, buildingId, demand) == false)
				continue;

			sourceDock = candidate;
			sourceBuildingId = buildingId;
			return true;
		}

		return false;
	}

	private bool CanMatch(CapsuleRelocateSendRequest request, CapsuleDock targetDock, uint targetBuildingId)
	{
		if (targetDock == null ||
			reservedDocks.Contains(request.SourceDock) ||
			reservedDocks.Contains(targetDock) ||
			targetDock.DockState != request.WantedTargetDockState ||
			targetDock.CanPutBox() == false)
		{
			return false;
		}

		return CanUseBuilding(request.Scope, request.SourceBuildingId, targetBuildingId) &&
			(request.RequiredTargetBuildingId == 0 || request.RequiredTargetBuildingId == targetBuildingId);
	}

	private bool CanMatch(CapsuleDock sourceDock, uint sourceBuildingId, CapsuleRelocateDemand demand)
	{
		if (sourceDock == null ||
			reservedDocks.Contains(sourceDock) ||
			reservedDocks.Contains(demand.TargetDock) ||
			sourceDock.DockState != demand.RequiredSourceDockState ||
			sourceDock.DockedCapsule?.LogisticsState != demand.RequiredCapsuleState ||
			sourceDock.CanGetBox() == false ||
			demand.TargetDock.CanPutBox() == false)
		{
			return false;
		}

		return CanUseBuilding(demand.Scope, sourceBuildingId, demand.TargetBuildingId) &&
			(demand.RequiredSourceBuildingId == 0 || demand.RequiredSourceBuildingId == sourceBuildingId);
	}

	private bool CanUseBuilding(CapsuleRelocateScope scope, uint sourceBuildingId, uint targetBuildingId)
	{
		return scope switch
		{
			CapsuleRelocateScope.SameBuilding => sourceBuildingId != 0 && sourceBuildingId == targetBuildingId,
			CapsuleRelocateScope.LinkedBuilding =>
				sourceBuildingId != 0 &&
				targetBuildingId != 0 &&
				sourceBuildingId != targetBuildingId &&
				(canUseLinkedBuilding == null || canUseLinkedBuilding(sourceBuildingId, targetBuildingId)),
			CapsuleRelocateScope.GlobalAllowed => true,
			_ => false,
		};
	}

	private bool IsSendSourceValid(CapsuleRelocateSendRequest request)
	{
		return request.SourceDock != null &&
			reservedDocks.Contains(request.SourceDock) == false &&
			request.SourceDock.DockState == request.RequiredSourceDockState &&
			request.SourceDock.DockedCapsule?.LogisticsState == request.RequiredCapsuleState &&
			request.SourceDock.CanGetBox();
	}

	private bool IsDemandTargetValid(CapsuleRelocateDemand demand)
	{
		return demand.TargetDock != null &&
			reservedDocks.Contains(demand.TargetDock) == false &&
			demand.TargetDock.CanPutBox();
	}

	private void Reserve(CapsuleDock sourceDock, CapsuleDock targetDock)
	{
		if (sourceDock != null)
			reservedDocks.Add(sourceDock);
		if (targetDock != null)
			reservedDocks.Add(targetDock);
	}

	private void RemoveSendRequests(CapsuleDock sourceDock)
	{
		if (sourceDock == null || sendNodeBySource.TryGetValue(sourceDock, out LinkedListNode<CapsuleRelocateSendRequest> node) == false)
			return;

		node.List?.Remove(node);
		sendNodeBySource.Remove(sourceDock);
	}

	private void RemoveDemands(CapsuleDock targetDock)
	{
		if (targetDock == null || demandNodeByTarget.TryGetValue(targetDock, out LinkedListNode<CapsuleRelocateDemand> node) == false)
			return;

		node.List?.Remove(node);
		demandNodeByTarget.Remove(targetDock);
	}

	private void AddPendingSend(CapsuleRelocateSendRequest request)
	{
		RemoveSendRequests(request.SourceDock);

		LinkedList<CapsuleRelocateSendRequest> bucket = GetSendBucket(request);
		LinkedListNode<CapsuleRelocateSendRequest> node = bucket.AddLast(request);
		sendNodeBySource[request.SourceDock] = node;
	}

	private void AddPendingDemand(CapsuleRelocateDemand demand)
	{
		RemoveDemands(demand.TargetDock);

		LinkedList<CapsuleRelocateDemand> bucket = GetDemandBucket(demand);
		LinkedListNode<CapsuleRelocateDemand> node = bucket.AddLast(demand);
		demandNodeByTarget[demand.TargetDock] = node;
	}

	private LinkedList<CapsuleRelocateSendRequest> GetSendBucket(CapsuleRelocateSendRequest request)
	{
		return request.Scope switch
		{
			CapsuleRelocateScope.SameBuilding => GetBuildingBucket(sameBuildingSends, request.SourceBuildingId, request.WantedTargetDockState),
			CapsuleRelocateScope.LinkedBuilding => GetStateBucket(linkedBuildingSends, request.WantedTargetDockState),
			CapsuleRelocateScope.GlobalAllowed => GetStateBucket(globalSends, request.WantedTargetDockState),
			_ => GetStateBucket(globalSends, request.WantedTargetDockState),
		};
	}

	private LinkedList<CapsuleRelocateDemand> GetDemandBucket(CapsuleRelocateDemand demand)
	{
		return demand.Scope switch
		{
			CapsuleRelocateScope.SameBuilding => GetBuildingBucket(sameBuildingDemands, demand.TargetBuildingId, demand.RequiredSourceDockState),
			CapsuleRelocateScope.LinkedBuilding => GetStateBucket(linkedBuildingDemands, demand.RequiredSourceDockState),
			CapsuleRelocateScope.GlobalAllowed => GetStateBucket(globalDemands, demand.RequiredSourceDockState),
			_ => GetStateBucket(globalDemands, demand.RequiredSourceDockState),
		};
	}

	private bool TryMatchPendingSendBuckets(
		Dictionary<uint, Dictionary<CapsuleDockState, LinkedList<CapsuleRelocateSendRequest>>> buckets,
		out CapsuleRelocateMatch match)
	{
		match = default;
		foreach (Dictionary<CapsuleDockState, LinkedList<CapsuleRelocateSendRequest>> stateBuckets in buckets.Values)
		{
			if (TryMatchPendingSendBuckets(stateBuckets, out match))
				return true;
		}

		return false;
	}

	private bool TryMatchPendingSendBuckets(
		Dictionary<CapsuleDockState, LinkedList<CapsuleRelocateSendRequest>> buckets,
		out CapsuleRelocateMatch match)
	{
		match = default;
		foreach (LinkedList<CapsuleRelocateSendRequest> bucket in buckets.Values)
		{
			if (TryMatchPendingSendBucket(bucket, out match))
				return true;
		}

		return false;
	}

	private bool TryMatchPendingSendBucket(LinkedList<CapsuleRelocateSendRequest> bucket, out CapsuleRelocateMatch match)
	{
		match = default;
		LinkedListNode<CapsuleRelocateSendRequest> node = bucket.First;
		while (node != null)
		{
			LinkedListNode<CapsuleRelocateSendRequest> next = node.Next;
			CapsuleRelocateSendRequest request = node.Value;
			if (IsSendSourceValid(request) == false)
			{
				RemoveSendNode(node);
				node = next;
				continue;
			}

			if (TryFindReceiver(request, out CapsuleDock targetDock, out uint targetBuildingId))
			{
				Reserve(request.SourceDock, targetDock);
				RemoveSendNode(node);
				match = new CapsuleRelocateMatch(request.RouteKind, request.SourceDock, targetDock, request.SourceBuildingId, targetBuildingId);
				return true;
			}

			node = next;
		}

		return false;
	}

	private bool TryMatchPendingDemandBuckets(
		Dictionary<uint, Dictionary<CapsuleDockState, LinkedList<CapsuleRelocateDemand>>> buckets,
		out CapsuleRelocateMatch match)
	{
		match = default;
		foreach (Dictionary<CapsuleDockState, LinkedList<CapsuleRelocateDemand>> stateBuckets in buckets.Values)
		{
			if (TryMatchPendingDemandBuckets(stateBuckets, out match))
				return true;
		}

		return false;
	}

	private bool TryMatchPendingDemandBuckets(
		Dictionary<CapsuleDockState, LinkedList<CapsuleRelocateDemand>> buckets,
		out CapsuleRelocateMatch match)
	{
		match = default;
		foreach (LinkedList<CapsuleRelocateDemand> bucket in buckets.Values)
		{
			if (TryMatchPendingDemandBucket(bucket, out match))
				return true;
		}

		return false;
	}

	private bool TryMatchPendingDemandBucket(LinkedList<CapsuleRelocateDemand> bucket, out CapsuleRelocateMatch match)
	{
		match = default;
		LinkedListNode<CapsuleRelocateDemand> node = bucket.First;
		while (node != null)
		{
			LinkedListNode<CapsuleRelocateDemand> next = node.Next;
			CapsuleRelocateDemand demand = node.Value;
			if (IsDemandTargetValid(demand) == false)
			{
				RemoveDemandNode(node);
				node = next;
				continue;
			}

			if (TryFindSource(demand, out CapsuleDock sourceDock, out uint sourceBuildingId))
			{
				Reserve(sourceDock, demand.TargetDock);
				RemoveDemandNode(node);
				match = new CapsuleRelocateMatch(demand.RouteKind, sourceDock, demand.TargetDock, sourceBuildingId, demand.TargetBuildingId);
				return true;
			}

			node = next;
		}

		return false;
	}

	private void RemoveSendNode(LinkedListNode<CapsuleRelocateSendRequest> node)
	{
		if (node == null)
			return;

		sendNodeBySource.Remove(node.Value.SourceDock);
		node.List?.Remove(node);
	}

	private void RemoveDemandNode(LinkedListNode<CapsuleRelocateDemand> node)
	{
		if (node == null)
			return;

		demandNodeByTarget.Remove(node.Value.TargetDock);
		node.List?.Remove(node);
	}

	private static LinkedList<T> GetBuildingBucket<T>(
		Dictionary<uint, Dictionary<CapsuleDockState, LinkedList<T>>> buckets,
		uint buildingId,
		CapsuleDockState dockState)
	{
		if (buckets.TryGetValue(buildingId, out Dictionary<CapsuleDockState, LinkedList<T>> stateBuckets) == false)
		{
			stateBuckets = new Dictionary<CapsuleDockState, LinkedList<T>>();
			buckets[buildingId] = stateBuckets;
		}

		return GetStateBucket(stateBuckets, dockState);
	}

	private static LinkedList<T> GetStateBucket<T>(
		Dictionary<CapsuleDockState, LinkedList<T>> buckets,
		CapsuleDockState dockState)
	{
		if (buckets.TryGetValue(dockState, out LinkedList<T> bucket) == false)
		{
			bucket = new LinkedList<T>();
			buckets[dockState] = bucket;
		}

		return bucket;
	}
}
