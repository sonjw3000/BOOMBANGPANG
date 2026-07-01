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

	public CapsuleRelocateSendRequest(
		CapsuleDock sourceDock,
		CapsuleDockState wantedTargetDockState,
		CapsuleRelocateRouteKind routeKind,
		CapsuleRelocateScope scope,
		CapsuleDockState requiredSourceDockState,
		CapsuleLogisticsState requiredCapsuleState,
		uint sourceBuildingId,
		uint requiredTargetBuildingId = 0)
	{
		SourceDock = sourceDock;
		WantedTargetDockState = wantedTargetDockState;
		RouteKind = routeKind;
		Scope = scope;
		RequiredSourceDockState = requiredSourceDockState;
		RequiredCapsuleState = requiredCapsuleState;
		SourceBuildingId = sourceBuildingId;
		RequiredTargetBuildingId = requiredTargetBuildingId;
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

	public CapsuleRelocateDemand(
		CapsuleDock targetDock,
		CapsuleDockState requiredSourceDockState,
		CapsuleLogisticsState requiredCapsuleState,
		CapsuleRelocateRouteKind routeKind,
		CapsuleRelocateScope scope,
		uint targetBuildingId,
		uint requiredSourceBuildingId = 0)
	{
		TargetDock = targetDock;
		RequiredSourceDockState = requiredSourceDockState;
		RequiredCapsuleState = requiredCapsuleState;
		RouteKind = routeKind;
		Scope = scope;
		TargetBuildingId = targetBuildingId;
		RequiredSourceBuildingId = requiredSourceBuildingId;
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
	// --------------------------------------------------------------
	// pending requests
	private readonly LinkedList<CapsuleRelocateSendRequest> pendingSends = new();
	private readonly LinkedList<CapsuleRelocateDemand> pendingDemands = new();
	private readonly Dictionary<CapsuleDock, LinkedListNode<CapsuleRelocateSendRequest>> pendingSendNodeBySource = new();
	private readonly Dictionary<CapsuleDock, LinkedListNode<CapsuleRelocateDemand>> pendingDemandNodeByTarget = new();

	// --------------------------------------------------------------
	// registered dock states
	private readonly Dictionary<uint, Dictionary<CapsuleDockState, LinkedList<CapsuleDock>>> receiversByBuilding = new();
	private readonly Dictionary<uint, Dictionary<CapsuleDockState, LinkedList<CapsuleDock>>> sourcesByBuilding = new();

	private readonly HashSet<CapsuleDock> reservedDocks = new();
	private readonly Func<uint, uint, bool> canUseLinkedBuilding;

	public int PendingSendCount => pendingSendNodeBySource.Count;
	public int PendingDemandCount => pendingDemandNodeByTarget.Count;

	public CapsuleRelocateCoordinator(Func<uint, uint, bool> canUseLinkedBuilding = null)
	{
		this.canUseLinkedBuilding = canUseLinkedBuilding;
	}

	public void RegisterReceiver(CapsuleDock dock, uint buildingId, CapsuleDockState dockState)
	{
		if (dock == null)
			return;

		LinkedList<CapsuleDock> bucket = GetBuildingBucket(receiversByBuilding, buildingId, dockState);
		if (ContainsDock(bucket, dock) == false)
			bucket.AddLast(dock);
	}

	public void RegisterSource(CapsuleDock dock, uint buildingId, CapsuleDockState dockState)
	{
		if (dock == null)
			return;

		LinkedList<CapsuleDock> bucket = GetBuildingBucket(sourcesByBuilding, buildingId, dockState);
		if (ContainsDock(bucket, dock) == false)
			bucket.AddLast(dock);
	}

	public void UnregisterReceiver(CapsuleDock dock, uint buildingId, CapsuleDockState dockState)
	{
		RemoveDockFromBuildingStateBucket(receiversByBuilding, dock, buildingId, dockState);
	}

	public void UnregisterSource(CapsuleDock dock, uint buildingId, CapsuleDockState dockState)
	{
		RemoveDockFromBuildingStateBucket(sourcesByBuilding, dock, buildingId, dockState);
	}

	public void UnregisterDock(CapsuleDock dock)
	{
		if (dock == null)
			return;

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
		LinkedListNode<CapsuleRelocateSendRequest> node = pendingSends.First;
		while (node != null)
		{
			LinkedListNode<CapsuleRelocateSendRequest> next = node.Next;
			CapsuleRelocateSendRequest request = node.Value;
			if (IsSendSourceValid(request) == false)
			{
				RemovePendingSendNode(node);
				node = next;
				continue;
			}

			if (TryFindReceiver(request, out CapsuleDock targetDock, out uint targetBuildingId))
			{
				Reserve(request.SourceDock, targetDock);
				RemovePendingSendNode(node);
				match = new CapsuleRelocateMatch(request.RouteKind, request.SourceDock, targetDock, request.SourceBuildingId, targetBuildingId);
				return true;
			}

			node = next;
		}

		return false;
	}

	private bool TryMatchPendingDemand(out CapsuleRelocateMatch match)
	{
		match = default;
		LinkedListNode<CapsuleRelocateDemand> node = pendingDemands.First;
		while (node != null)
		{
			LinkedListNode<CapsuleRelocateDemand> next = node.Next;
			CapsuleRelocateDemand demand = node.Value;
			if (IsDemandTargetValid(demand) == false)
			{
				RemovePendingDemandNode(node);
				node = next;
				continue;
			}

			if (TryFindSource(demand, out CapsuleDock sourceDock, out uint sourceBuildingId))
			{
				Reserve(sourceDock, demand.TargetDock);
				RemovePendingDemandNode(node);
				match = new CapsuleRelocateMatch(demand.RouteKind, sourceDock, demand.TargetDock, sourceBuildingId, demand.TargetBuildingId);
				return true;
			}

			node = next;
		}

		return false;
	}

	private bool TryFindReceiver(CapsuleRelocateSendRequest request, out CapsuleDock targetDock, out uint targetBuildingId)
	{
		targetDock = null;
		targetBuildingId = 0;

		if (request.RequiredTargetBuildingId != 0)
			return TryFindReceiverInBuilding(request, request.RequiredTargetBuildingId, out targetDock, out targetBuildingId);

		return request.Scope switch
		{
			CapsuleRelocateScope.SameBuilding =>
				TryFindReceiverInBuilding(request, request.SourceBuildingId, out targetDock, out targetBuildingId),
			CapsuleRelocateScope.LinkedBuilding =>
				TryFindReceiverInLinkedBuildings(request, out targetDock, out targetBuildingId),
			CapsuleRelocateScope.GlobalAllowed =>
				TryFindReceiverInAllBuildings(request, out targetDock, out targetBuildingId),
			_ => false,
		};
	}

	private bool TryFindSource(CapsuleRelocateDemand demand, out CapsuleDock sourceDock, out uint sourceBuildingId)
	{
		sourceDock = null;
		sourceBuildingId = 0;

		if (demand.RequiredSourceBuildingId != 0)
			return TryFindSourceInBuilding(demand, demand.RequiredSourceBuildingId, out sourceDock, out sourceBuildingId);

		return demand.Scope switch
		{
			CapsuleRelocateScope.SameBuilding =>
				TryFindSourceInBuilding(demand, demand.TargetBuildingId, out sourceDock, out sourceBuildingId),
			CapsuleRelocateScope.LinkedBuilding =>
				TryFindSourceInLinkedBuildings(demand, out sourceDock, out sourceBuildingId),
			CapsuleRelocateScope.GlobalAllowed =>
				TryFindSourceInAllBuildings(demand, out sourceDock, out sourceBuildingId),
			_ => false,
		};
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
				canUseLinkedBuilding != null &&
				canUseLinkedBuilding(sourceBuildingId, targetBuildingId),
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
		if (sourceDock == null ||
			pendingSendNodeBySource.TryGetValue(sourceDock, out LinkedListNode<CapsuleRelocateSendRequest> node) == false)
		{
			return;
		}

		RemovePendingSendNode(node);
	}

	private void RemoveDemands(CapsuleDock targetDock)
	{
		if (targetDock == null ||
			pendingDemandNodeByTarget.TryGetValue(targetDock, out LinkedListNode<CapsuleRelocateDemand> node) == false)
		{
			return;
		}

		RemovePendingDemandNode(node);
	}

	private void AddPendingSend(CapsuleRelocateSendRequest request)
	{
		RemoveSendRequests(request.SourceDock);
		LinkedListNode<CapsuleRelocateSendRequest> node = pendingSends.AddLast(request);
		pendingSendNodeBySource[request.SourceDock] = node;
	}

	private void AddPendingDemand(CapsuleRelocateDemand demand)
	{
		RemoveDemands(demand.TargetDock);
		LinkedListNode<CapsuleRelocateDemand> node = pendingDemands.AddLast(demand);
		pendingDemandNodeByTarget[demand.TargetDock] = node;
	}

	private bool TryFindReceiverInBuilding(
		CapsuleRelocateSendRequest request,
		uint buildingId,
		out CapsuleDock targetDock,
		out uint targetBuildingId)
	{
		targetDock = null;
		targetBuildingId = 0;
		if (TryGetBuildingStateBucket(receiversByBuilding, buildingId, request.WantedTargetDockState, out LinkedList<CapsuleDock> bucket) == false)
			return false;

		return TryFindReceiverInBucket(request, bucket, buildingId, out targetDock, out targetBuildingId);
	}

	private bool TryFindReceiverInLinkedBuildings(
		CapsuleRelocateSendRequest request,
		out CapsuleDock targetDock,
		out uint targetBuildingId)
	{
		targetDock = null;
		targetBuildingId = 0;
		foreach (uint buildingId in receiversByBuilding.Keys)
		{
			if (CanUseBuilding(request.Scope, request.SourceBuildingId, buildingId) == false)
				continue;

			if (TryFindReceiverInBuilding(request, buildingId, out targetDock, out targetBuildingId))
				return true;
		}

		return false;
	}

	private bool TryFindReceiverInAllBuildings(
		CapsuleRelocateSendRequest request,
		out CapsuleDock targetDock,
		out uint targetBuildingId)
	{
		targetDock = null;
		targetBuildingId = 0;
		foreach (uint buildingId in receiversByBuilding.Keys)
		{
			if (TryFindReceiverInBuilding(request, buildingId, out targetDock, out targetBuildingId))
				return true;
		}

		return false;
	}

	private bool TryFindReceiverInBucket(
		CapsuleRelocateSendRequest request,
		LinkedList<CapsuleDock> bucket,
		uint buildingId,
		out CapsuleDock targetDock,
		out uint targetBuildingId)
	{
		targetDock = null;
		targetBuildingId = 0;
		LinkedListNode<CapsuleDock> node = bucket.First;
		while (node != null)
		{
			CapsuleDock candidate = node.Value;
			if (CanMatch(request, candidate, buildingId))
			{
				targetDock = candidate;
				targetBuildingId = buildingId;
				return true;
			}

			node = node.Next;
		}

		return false;
	}

	private bool TryFindSourceInBuilding(
		CapsuleRelocateDemand demand,
		uint buildingId,
		out CapsuleDock sourceDock,
		out uint sourceBuildingId)
	{
		sourceDock = null;
		sourceBuildingId = 0;
		if (TryGetBuildingStateBucket(sourcesByBuilding, buildingId, demand.RequiredSourceDockState, out LinkedList<CapsuleDock> bucket) == false)
			return false;

		return TryFindSourceInBucket(demand, bucket, buildingId, out sourceDock, out sourceBuildingId);
	}

	private bool TryFindSourceInLinkedBuildings(
		CapsuleRelocateDemand demand,
		out CapsuleDock sourceDock,
		out uint sourceBuildingId)
	{
		sourceDock = null;
		sourceBuildingId = 0;
		foreach (uint buildingId in sourcesByBuilding.Keys)
		{
			if (CanUseBuilding(demand.Scope, buildingId, demand.TargetBuildingId) == false)
				continue;

			if (TryFindSourceInBuilding(demand, buildingId, out sourceDock, out sourceBuildingId))
				return true;
		}

		return false;
	}

	private bool TryFindSourceInAllBuildings(
		CapsuleRelocateDemand demand,
		out CapsuleDock sourceDock,
		out uint sourceBuildingId)
	{
		sourceDock = null;
		sourceBuildingId = 0;
		foreach (uint buildingId in sourcesByBuilding.Keys)
		{
			if (TryFindSourceInBuilding(demand, buildingId, out sourceDock, out sourceBuildingId))
				return true;
		}

		return false;
	}

	private bool TryFindSourceInBucket(
		CapsuleRelocateDemand demand,
		LinkedList<CapsuleDock> bucket,
		uint buildingId,
		out CapsuleDock sourceDock,
		out uint sourceBuildingId)
	{
		sourceDock = null;
		sourceBuildingId = 0;
		LinkedListNode<CapsuleDock> node = bucket.First;
		while (node != null)
		{
			CapsuleDock candidate = node.Value;
			if (CanMatch(candidate, buildingId, demand))
			{
				sourceDock = candidate;
				sourceBuildingId = buildingId;
				return true;
			}

			node = node.Next;
		}

		return false;
	}

	private void RemovePendingSendNode(LinkedListNode<CapsuleRelocateSendRequest> node)
	{
		if (node == null)
			return;

		pendingSendNodeBySource.Remove(node.Value.SourceDock);
		node.List?.Remove(node);
	}

	private void RemovePendingDemandNode(LinkedListNode<CapsuleRelocateDemand> node)
	{
		if (node == null)
			return;

		pendingDemandNodeByTarget.Remove(node.Value.TargetDock);
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

	private static bool TryGetBuildingStateBucket<T>(
		Dictionary<uint, Dictionary<CapsuleDockState, LinkedList<T>>> buckets,
		uint buildingId,
		CapsuleDockState dockState,
		out LinkedList<T> bucket)
	{
		bucket = null;
		return buckets.TryGetValue(buildingId, out Dictionary<CapsuleDockState, LinkedList<T>> stateBuckets) &&
			stateBuckets.TryGetValue(dockState, out bucket);
	}

	private static bool ContainsDock(LinkedList<CapsuleDock> bucket, CapsuleDock dock)
	{
		if (bucket == null || dock == null)
			return false;

		LinkedListNode<CapsuleDock> node = bucket.First;
		while (node != null)
		{
			if (node.Value == dock)
				return true;

			node = node.Next;
		}

		return false;
	}

	private static void RemoveDockFromBuildingStateBucket(
		Dictionary<uint, Dictionary<CapsuleDockState, LinkedList<CapsuleDock>>> buckets,
		CapsuleDock dock,
		uint buildingId,
		CapsuleDockState dockState)
	{
		if (dock == null ||
			TryGetBuildingStateBucket(buckets, buildingId, dockState, out LinkedList<CapsuleDock> bucket) == false)
		{
			return;
		}

		LinkedListNode<CapsuleDock> node = bucket.First;
		while (node != null)
		{
			LinkedListNode<CapsuleDock> next = node.Next;
			if (node.Value == dock)
			{
				bucket.Remove(node);
				return;
			}

			node = next;
		}
	}
}
