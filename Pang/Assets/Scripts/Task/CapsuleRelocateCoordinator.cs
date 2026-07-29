using System;
using System.Collections.Generic;

public enum CapsuleRelocateScope
{
	SameBuilding,
	LinkedBuilding,
	GlobalAllowed,
}

public readonly struct CapsuleRelocateSendRequest
{
	public readonly CapsuleDock SourceDock;
	public readonly CapsuleDockState RequiredSourceDockState;
	public readonly CapsuleLogisticsState RequiredCapsuleState;
	public readonly CapsuleDockState WantedTargetDockState;
	public readonly CapsuleRelocateScope Scope;
	public readonly uint SourceBuildingId;
	public readonly uint RequiredTargetBuildingId;
	public readonly Func<CapsuleRelocateMatch, bool> OnMatched;

	public CapsuleRelocateSendRequest(
		CapsuleDock sourceDock,
		CapsuleDockState requiredSourceDockState,
		CapsuleLogisticsState requiredCapsuleState,
		CapsuleDockState wantedTargetDockState,
		CapsuleRelocateScope scope,
		uint sourceBuildingId,
		uint requiredTargetBuildingId = 0,
		Func<CapsuleRelocateMatch, bool> onMatched = null)
	{
		SourceDock = sourceDock;
		RequiredSourceDockState = requiredSourceDockState;
		RequiredCapsuleState = requiredCapsuleState;
		WantedTargetDockState = wantedTargetDockState;
		Scope = scope;
		SourceBuildingId = sourceBuildingId;
		RequiredTargetBuildingId = requiredTargetBuildingId;
		OnMatched = onMatched;
	}
}

public readonly struct CapsuleRelocateDemand
{
	public readonly CapsuleDock TargetDock;
	public readonly CapsuleDockState RequiredTargetDockState;
	public readonly CapsuleDockState RequiredSourceDockState;
	public readonly CapsuleLogisticsState RequiredCapsuleState;
	public readonly CapsuleRelocateScope Scope;
	public readonly uint TargetBuildingId;
	public readonly uint RequiredSourceBuildingId;
	public readonly Func<CapsuleRelocateMatch, bool> OnMatched;

	public CapsuleRelocateDemand(
		CapsuleDock targetDock,
		CapsuleDockState requiredTargetDockState,
		CapsuleDockState requiredSourceDockState,
		CapsuleLogisticsState requiredCapsuleState,
		CapsuleRelocateScope scope,
		uint targetBuildingId,
		uint requiredSourceBuildingId = 0,
		Func<CapsuleRelocateMatch, bool> onMatched = null)
	{
		TargetDock = targetDock;
		RequiredTargetDockState = requiredTargetDockState;
		RequiredSourceDockState = requiredSourceDockState;
		RequiredCapsuleState = requiredCapsuleState;
		Scope = scope;
		TargetBuildingId = targetBuildingId;
		RequiredSourceBuildingId = requiredSourceBuildingId;
		OnMatched = onMatched;
	}
}

public readonly struct CapsuleRelocateMatch
{
	public readonly CapsuleDock SourceDock;
	public readonly CapsuleDock TargetDock;
	public readonly uint SourceBuildingId;
	public readonly uint TargetBuildingId;

	public CapsuleRelocateMatch(
		CapsuleDock sourceDock,
		CapsuleDock targetDock,
		uint sourceBuildingId,
		uint targetBuildingId)
	{
		SourceDock = sourceDock;
		TargetDock = targetDock;
		SourceBuildingId = sourceBuildingId;
		TargetBuildingId = targetBuildingId;
	}
}

public sealed class CapsuleRelocateCoordinator
{
	private readonly CapsuleDockService dockService;
	private readonly LinkedList<CapsuleRelocateSendRequest> pendingSends = new();
	private readonly LinkedList<CapsuleRelocateDemand> pendingDemands = new();
	private readonly Dictionary<CapsuleDock, LinkedListNode<CapsuleRelocateSendRequest>> pendingSendNodeBySource = new();
	private readonly Dictionary<CapsuleDock, LinkedListNode<CapsuleRelocateDemand>> pendingDemandNodeByTarget = new();
	private readonly HashSet<CapsuleDock> reservedDocks = new();
	private readonly HashSet<CapsuleDock> activeRelocationSources = new();
	private readonly HashSet<CapsuleDock> activeRelocationTargets = new();
	private readonly Func<uint, uint, bool> canUseLinkedBuilding;

	public int PendingSendCount => pendingSendNodeBySource.Count;
	public int PendingDemandCount => pendingDemandNodeByTarget.Count;

	public CapsuleRelocateCoordinator(
		CapsuleDockService dockService,
		Func<uint, uint, bool> canUseLinkedBuilding = null)
	{
		this.dockService = dockService;
		this.canUseLinkedBuilding = canUseLinkedBuilding;
	}

	public bool RequestSend(CapsuleRelocateSendRequest request)
	{
		if (IsSendSourceValid(request, checkReservation: false) == false)
			return false;

		if (TryFindReceiver(request, out CapsuleDock targetDock, out uint targetBuildingId))
		{
			if (TryAcceptSendMatch(request, targetDock, targetBuildingId))
				return true;
		}

		AddPendingSend(request);
		return false;
	}

	public bool RequestDemand(CapsuleRelocateDemand demand)
	{
		if (IsDemandTargetValid(demand, checkReservation: false) == false)
			return false;

		if (TryFindSource(demand, out CapsuleDock sourceDock, out uint sourceBuildingId))
		{
			if (TryAcceptDemandMatch(demand, sourceDock, sourceBuildingId))
				return true;
		}

		AddPendingDemand(demand);
		return false;
	}

	public bool NotifyCapsuleDocked(CapsuleDock dock)
	{
		activeRelocationTargets.Remove(dock);
		ReleaseReservation(dock);
		return TryMatchPendingDemand() || TryMatchPendingSend();
	}

	public bool NotifyCapsuleUndocked(CapsuleDock dock)
	{
		activeRelocationSources.Remove(dock);
		ReleaseReservation(dock);
		return TryMatchPendingSend() || TryMatchPendingDemand();
	}

	public bool NotifyDockStateChanged(CapsuleDock dock)
	{
		ReleaseReservation(dock);
		return TryMatchPendingSend() || TryMatchPendingDemand();
	}

	public void ReleaseReservation(CapsuleDock sourceDock, CapsuleDock targetDock)
	{
		if (sourceDock != null)
			ReleaseReservation(sourceDock);
		if (targetDock != null)
			ReleaseReservation(targetDock);
	}

	public void ReleaseReservation(CapsuleDock dock)
	{
		if (dock != null)
			reservedDocks.Remove(dock);
	}

	public void NotifyRelocationEnded(CapsuleDock sourceDock, CapsuleDock targetDock)
	{
		if (sourceDock != null)
		{
			activeRelocationSources.Remove(sourceDock);
			reservedDocks.Remove(sourceDock);
		}

		if (targetDock != null)
		{
			activeRelocationTargets.Remove(targetDock);
			reservedDocks.Remove(targetDock);
		}

		TryMatchPendingSend();
		TryMatchPendingDemand();
	}

	public void ResetRuntimeState()
	{
		pendingSends.Clear();
		pendingDemands.Clear();
		pendingSendNodeBySource.Clear();
		pendingDemandNodeByTarget.Clear();
		reservedDocks.Clear();
		activeRelocationSources.Clear();
		activeRelocationTargets.Clear();
	}

	public void CancelPendingRequests(CapsuleDock dock)
	{
		if (dock == null)
			return;

		RemoveSendRequest(dock);
		RemoveDemand(dock);
	}

	public void RemoveDock(CapsuleDock dock)
	{
		if (dock == null)
			return;

		activeRelocationSources.Remove(dock);
		activeRelocationTargets.Remove(dock);
		reservedDocks.Remove(dock);
		CancelPendingRequests(dock);
	}

	public bool IsReserved(CapsuleDock dock)
	{
		return dock != null && reservedDocks.Contains(dock);
	}

	public bool IsRelocationSourceActive(CapsuleDock dock)
	{
		return dock != null && activeRelocationSources.Contains(dock);
	}

	public bool IsRelocationTargetActive(CapsuleDock dock)
	{
		return dock != null && activeRelocationTargets.Contains(dock);
	}

	public bool TryReserveActiveTarget(CapsuleDock dock)
	{
		if (IsFacilityAvailable(dock) == false ||
			dock.CanPutBox() == false ||
			reservedDocks.Contains(dock) ||
			activeRelocationTargets.Contains(dock))
		{
			return false;
		}

		reservedDocks.Add(dock);
		activeRelocationTargets.Add(dock);
		return true;
	}

	private bool TryMatchPendingSend()
	{
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
				if (TryAcceptSendMatch(request, targetDock, targetBuildingId))
				{
					RemovePendingSendNode(node);
					return true;
				}
			}

			node = next;
		}

		return false;
	}

	private bool TryMatchPendingDemand()
	{
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
				if (TryAcceptDemandMatch(demand, sourceDock, sourceBuildingId))
				{
					RemovePendingDemandNode(node);
					return true;
				}
			}

			node = next;
		}

		return false;
	}

	private bool TryFindReceiver(
		CapsuleRelocateSendRequest request,
		out CapsuleDock targetDock,
		out uint targetBuildingId)
	{
		targetDock = null;
		targetBuildingId = 0;
		if (dockService == null)
			return false;

		uint queryBuildingId = request.RequiredTargetBuildingId != 0
			? request.RequiredTargetBuildingId
			: request.Scope == CapsuleRelocateScope.SameBuilding
				? request.SourceBuildingId
				: 0;

		return dockService.TryFindDock(
			queryBuildingId,
			request.WantedTargetDockState,
			false,
			out targetDock,
			out targetBuildingId,
			(candidate, candidateBuildingId) => CanMatch(request, candidate, candidateBuildingId));
	}

	private bool TryFindSource(
		CapsuleRelocateDemand demand,
		out CapsuleDock sourceDock,
		out uint sourceBuildingId)
	{
		sourceDock = null;
		sourceBuildingId = 0;
		if (dockService == null)
			return false;

		uint queryBuildingId = demand.RequiredSourceBuildingId != 0
			? demand.RequiredSourceBuildingId
			: demand.Scope == CapsuleRelocateScope.SameBuilding
				? demand.TargetBuildingId
				: 0;

		return dockService.TryFindDock(
			queryBuildingId,
			demand.RequiredSourceDockState,
			true,
			out sourceDock,
			out sourceBuildingId,
			(candidate, candidateBuildingId) => CanMatch(candidate, candidateBuildingId, demand));
	}

	private bool CanMatch(CapsuleRelocateSendRequest request, CapsuleDock targetDock, uint targetBuildingId)
	{
		if (targetDock == null ||
			IsFacilityAvailable(request.SourceDock) == false ||
			IsFacilityAvailable(targetDock) == false ||
			reservedDocks.Contains(request.SourceDock) ||
			reservedDocks.Contains(targetDock) ||
			activeRelocationSources.Contains(request.SourceDock) ||
			activeRelocationTargets.Contains(targetDock) ||
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
			IsFacilityAvailable(sourceDock) == false ||
			IsFacilityAvailable(demand.TargetDock) == false ||
			reservedDocks.Contains(sourceDock) ||
			reservedDocks.Contains(demand.TargetDock) ||
			activeRelocationSources.Contains(sourceDock) ||
			activeRelocationTargets.Contains(demand.TargetDock) ||
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
				targetBuildingId != 0 &&
				sourceBuildingId != targetBuildingId &&
				canUseLinkedBuilding != null &&
				canUseLinkedBuilding(sourceBuildingId, targetBuildingId),
			CapsuleRelocateScope.GlobalAllowed => true,
			_ => false,
		};
	}

	private bool IsSendSourceValid(CapsuleRelocateSendRequest request, bool checkReservation = true)
	{
		return request.SourceDock != null &&
			IsFacilityAvailable(request.SourceDock) &&
			(checkReservation == false || reservedDocks.Contains(request.SourceDock) == false) &&
			activeRelocationSources.Contains(request.SourceDock) == false &&
			request.SourceDock.DockState == request.RequiredSourceDockState &&
			request.SourceDock.DockedCapsule?.LogisticsState == request.RequiredCapsuleState &&
			request.SourceDock.CanGetBox();
	}

	private bool IsDemandTargetValid(CapsuleRelocateDemand demand, bool checkReservation = true)
	{
		return demand.TargetDock != null &&
			IsFacilityAvailable(demand.TargetDock) &&
			(checkReservation == false || reservedDocks.Contains(demand.TargetDock) == false) &&
			activeRelocationTargets.Contains(demand.TargetDock) == false &&
			demand.TargetDock.DockState == demand.RequiredTargetDockState &&
			demand.TargetDock.CanPutBox();
	}

	private static bool IsFacilityAvailable(CapsuleDock dock)
	{
		return dock != null &&
			(GameContext.HasInstance == false ||
			 GameContext.Instance.FacilityMgr == null ||
			 GameContext.Instance.FacilityMgr.IsInvalidating(dock) == false);
	}

	private void Reserve(CapsuleDock sourceDock, CapsuleDock targetDock)
	{
		if (sourceDock != null)
			reservedDocks.Add(sourceDock);
		if (targetDock != null)
			reservedDocks.Add(targetDock);
	}

	private void MarkActive(CapsuleDock sourceDock, CapsuleDock targetDock)
	{
		if (sourceDock != null)
			activeRelocationSources.Add(sourceDock);
		if (targetDock != null)
			activeRelocationTargets.Add(targetDock);
	}

	private bool TryAcceptSendMatch(
		CapsuleRelocateSendRequest request,
		CapsuleDock targetDock,
		uint targetBuildingId)
	{
		CapsuleRelocateMatch match = new(request.SourceDock, targetDock, request.SourceBuildingId, targetBuildingId);
		Reserve(match.SourceDock, match.TargetDock);
		if (request.OnMatched == null)
			return true;

		if (request.OnMatched(match))
		{
			MarkActive(match.SourceDock, match.TargetDock);
			return true;
		}

		ReleaseReservation(match.SourceDock, match.TargetDock);
		return false;
	}

	private bool TryAcceptDemandMatch(
		CapsuleRelocateDemand demand,
		CapsuleDock sourceDock,
		uint sourceBuildingId)
	{
		CapsuleRelocateMatch match = new(sourceDock, demand.TargetDock, sourceBuildingId, demand.TargetBuildingId);
		Reserve(match.SourceDock, match.TargetDock);
		if (demand.OnMatched == null)
			return true;

		if (demand.OnMatched(match))
		{
			MarkActive(match.SourceDock, match.TargetDock);
			return true;
		}

		ReleaseReservation(match.SourceDock, match.TargetDock);
		return false;
	}

	private void AddPendingSend(CapsuleRelocateSendRequest request)
	{
		RemoveSendRequest(request.SourceDock);
		LinkedListNode<CapsuleRelocateSendRequest> node = pendingSends.AddLast(request);
		pendingSendNodeBySource[request.SourceDock] = node;
	}

	private void AddPendingDemand(CapsuleRelocateDemand demand)
	{
		RemoveDemand(demand.TargetDock);
		LinkedListNode<CapsuleRelocateDemand> node = pendingDemands.AddLast(demand);
		pendingDemandNodeByTarget[demand.TargetDock] = node;
	}

	private void RemoveSendRequest(CapsuleDock sourceDock)
	{
		if (sourceDock == null ||
			pendingSendNodeBySource.TryGetValue(sourceDock, out LinkedListNode<CapsuleRelocateSendRequest> node) == false)
		{
			return;
		}

		RemovePendingSendNode(node);
	}

	private void RemoveDemand(CapsuleDock targetDock)
	{
		if (targetDock == null ||
			pendingDemandNodeByTarget.TryGetValue(targetDock, out LinkedListNode<CapsuleRelocateDemand> node) == false)
		{
			return;
		}

		RemovePendingDemandNode(node);
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
}
