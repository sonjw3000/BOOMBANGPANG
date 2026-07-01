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
	private readonly List<CapsuleRelocateSendRequest> pendingSends = new();
	private readonly List<CapsuleRelocateDemand> pendingDemands = new();
	private readonly Dictionary<CapsuleDock, uint> availableReceivers = new();
	private readonly Dictionary<CapsuleDock, uint> availableSources = new();
	private readonly HashSet<CapsuleDock> reservedDocks = new();
	private readonly Func<uint, uint, bool> canUseLinkedBuilding;

	public IReadOnlyList<CapsuleRelocateSendRequest> PendingSends => pendingSends;
	public IReadOnlyList<CapsuleRelocateDemand> PendingDemands => pendingDemands;
	public IReadOnlyDictionary<CapsuleDock, uint> AvailableReceivers => availableReceivers;
	public IReadOnlyDictionary<CapsuleDock, uint> AvailableSources => availableSources;

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

		pendingSends.Add(request);
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

		pendingDemands.Add(demand);
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
		for (int i = 0; i < pendingSends.Count; ++i)
		{
			CapsuleRelocateSendRequest request = pendingSends[i];
			if (IsSendSourceValid(request) == false)
			{
				pendingSends.RemoveAt(i);
				--i;
				continue;
			}

			if (TryFindReceiver(request, out CapsuleDock targetDock, out uint targetBuildingId))
			{
				Reserve(request.SourceDock, targetDock);
				pendingSends.RemoveAt(i);
				match = new CapsuleRelocateMatch(request.RouteKind, request.SourceDock, targetDock, request.SourceBuildingId, targetBuildingId);
				return true;
			}
		}

		return false;
	}

	private bool TryMatchPendingDemand(out CapsuleRelocateMatch match)
	{
		match = default;
		for (int i = 0; i < pendingDemands.Count; ++i)
		{
			CapsuleRelocateDemand demand = pendingDemands[i];
			if (IsDemandTargetValid(demand) == false)
			{
				pendingDemands.RemoveAt(i);
				--i;
				continue;
			}

			if (TryFindSource(demand, out CapsuleDock sourceDock, out uint sourceBuildingId))
			{
				Reserve(sourceDock, demand.TargetDock);
				pendingDemands.RemoveAt(i);
				match = new CapsuleRelocateMatch(demand.RouteKind, sourceDock, demand.TargetDock, sourceBuildingId, demand.TargetBuildingId);
				return true;
			}
		}

		return false;
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
		for (int i = pendingSends.Count - 1; i >= 0; --i)
		{
			if (pendingSends[i].SourceDock == sourceDock)
				pendingSends.RemoveAt(i);
		}
	}

	private void RemoveDemands(CapsuleDock targetDock)
	{
		for (int i = pendingDemands.Count - 1; i >= 0; --i)
		{
			if (pendingDemands[i].TargetDock == targetDock)
				pendingDemands.RemoveAt(i);
		}
	}
}
