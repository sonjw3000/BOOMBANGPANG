using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

public sealed class WasteCollectionPlanner :
	IItemTransferPlanner,
	IItemTransferTaskInvalidationHandler,
	IItemTransferTaskCompletionHandler
{
	private const uint GlobalScheduleBuildingId = 0;

	private readonly HashSet<uint> dirtyBuildingIds = new();
	private readonly Dictionary<uint, AIWorker> activeWorkerByBuilding = new();
	private readonly Dictionary<AIWorker, uint> activeBuildingByWorker = new();
	private readonly Dictionary<AIWorker, uint> lastBuildingByWorker = new();
	private readonly Dictionary<ItemTransferTask, uint> activeBuildingByTask = new();
	private readonly Dictionary<ItemTransferTask, AIWorker> activeWorkerByTask = new();

	private ItemTransferTaskScheduler scheduler;
	private CapsuleDockService dockService;
	private bool isBound;
	private bool isRestoring;

	private BuildingManager BuildingManager => GameContext.HasInstance ? GameContext.Instance.BuildingMgr : null;
	private TaskManager TaskManager => GameContext.HasInstance ? GameContext.Instance.TaskMgr : null;

	public int DirtyBuildingCount => dirtyBuildingIds.Count;
	public int ActiveBuildingCount => activeWorkerByBuilding.Count;

	public WasteCollectionTaskSaveData CaptureTaskState(ItemTransferTask task)
	{
		if (task == null ||
			task.Type != WorkerTask.TaskType.WasteCollection ||
			activeBuildingByTask.TryGetValue(task, out uint buildingId) == false ||
			activeWorkerByTask.TryGetValue(task, out AIWorker worker) == false ||
			worker == null)
		{
			return null;
		}

		return new WasteCollectionTaskSaveData
		{
			PreferredWorkerId = worker.WorkerID,
			SessionBuildingId = buildingId,
		};
	}

	public ItemTransferTask RestoreTaskState(WasteCollectionTaskSaveData data, AIWorker preferredWorker)
	{
		if (data == null ||
			preferredWorker == null ||
			data.SessionBuildingId == 0 ||
			BuildingManager == null ||
			BuildingManager.TryGetBuilding(data.SessionBuildingId, out Building building) == false ||
			building == null ||
			activeWorkerByBuilding.ContainsKey(data.SessionBuildingId) ||
			activeBuildingByWorker.ContainsKey(preferredWorker))
		{
			return null;
		}

		ItemTransferTask task = new(
			WorkerTask.TaskType.WasteCollection,
			new ItemTransferJob(this, TransferObjectType.Item, TransferObjectType.Item, 0, preferredWorker));
		RegisterSession(task, preferredWorker, data.SessionBuildingId);
		return task;
	}

	public void Initialize(ItemTransferTaskScheduler taskScheduler, CapsuleDockService capsuleDockService)
	{
		scheduler = taskScheduler;
		dockService = capsuleDockService;
		RegisterSchedulerHandler();
		BindDockEvents();
	}

	public void Unbind()
	{
		if (isBound == false || dockService == null)
			return;

		dockService.OnCapsuleDocked -= HandleCapsuleDocked;
		dockService.OnCapsuleUndocked -= HandleCapsuleUndocked;
		isBound = false;
	}

	public void ResetRuntimeState()
	{
		isRestoring = false;
		dirtyBuildingIds.Clear();
		activeWorkerByBuilding.Clear();
		activeBuildingByWorker.Clear();
		lastBuildingByWorker.Clear();
		activeBuildingByTask.Clear();
		activeWorkerByTask.Clear();
		RegisterSchedulerHandler();
	}

	public void BeginRestore()
	{
		isRestoring = true;
	}

	public void EndRestore()
	{
		isRestoring = false;
		dirtyBuildingIds.Clear();
		if (BuildingManager == null)
			return;

		IReadOnlyList<Building> buildings = BuildingManager.RegisteredBuildings;
		for (int i = 0; i < buildings.Count; ++i)
		{
			Building building = buildings[i];
			NotifyBuildingChanged(building);
			if (building == null)
				continue;

			for (int portIndex = 0; portIndex < building.OccupiedCargoPorts.Count; ++portIndex)
			{
				if (building.OccupiedCargoPorts[portIndex] is OutboundCargoPort outboundPort &&
					outboundPort.DockedCapsule?.RouteKind == CargoRouteKind.Waste)
				{
					TryRequestExternalExport(outboundPort);
				}
			}
		}
	}

	public void NotifyBuildingChanged(Building building)
	{
		if (building == null || building.RuntimeBuildingId == 0 || building.State != BuildingState.Active)
			return;

		if (isRestoring)
			return;

		TryRequestFullBinExport(building);
		if (HasLooseWaste(building))
		{
			dirtyBuildingIds.Add(building.RuntimeBuildingId);
			scheduler?.MarkDirty(GlobalScheduleBuildingId, ItemTransferScheduleMode.WasteCollection);
		}
		else
		{
			dirtyBuildingIds.Remove(building.RuntimeBuildingId);
			if (dirtyBuildingIds.Count <= 0)
				scheduler?.ClearDirty(GlobalScheduleBuildingId, ItemTransferScheduleMode.WasteCollection);
		}
	}

	public WorkPlanResult TryGetCollectLine(AIWorker worker, uint buildingId, out WorkLine line)
	{
		line = null;
		if (worker == null || TryGetSessionBuilding(worker, out Building building) == false)
			return WorkPlanResult.Completed;
		if (building.State != BuildingState.Active)
			return WorkPlanResult.Waiting;

		BoxBase workerBox = worker.CarryingAbility?.CarryingBox;
		if (workerBox == null)
			return WorkPlanResult.Waiting;

		if (TryFindCollectLine(worker, workerBox, building, out line))
			return WorkPlanResult.Issued;

		return HasWastePayload(workerBox)
			? WorkPlanResult.SwitchPhase
			: WorkPlanResult.Completed;
	}

	public WorkPlanResult OnCollectCompleted(AIWorker worker, WorkLine line, ItemTransferResult result)
	{
		if (worker == null || TryGetSessionBuilding(worker, out Building building) == false)
			return WorkPlanResult.Completed;
		if (building.State != BuildingState.Active)
			return WorkPlanResult.Waiting;

		BoxBase workerBox = worker.CarryingAbility?.CarryingBox;
		if (result.Moved <= 0)
			return HasWastePayload(workerBox) ? WorkPlanResult.SwitchPhase : WorkPlanResult.Completed;

		NotifyBuildingChanged(building);
		if (TryFindCollectLine(worker, workerBox, building, out _))
			return WorkPlanResult.Issued;

		return WorkPlanResult.SwitchPhase;
	}

	public WorkPlanResult TryGetPlaceLine(
		AIWorker worker,
		uint buildingId,
		WorkLine collectedLine,
		int remainingQuantity,
		out WorkLine line)
	{
		line = null;
		if (worker == null ||
			collectedLine == null ||
			remainingQuantity <= 0)
		{
			return WorkPlanResult.Completed;
		}
		if (TryGetSessionBuilding(worker, out Building building) == false)
			return WorkPlanResult.Completed;
		if (building.State != BuildingState.Active)
			return WorkPlanResult.Waiting;

		BoxBase workerBox = worker.CarryingAbility?.CarryingBox;
		if (workerBox == null || HasWastePayload(workerBox) == false)
			return WorkPlanResult.Completed;

		if (TryFindWasteBinDock(
			worker,
			building,
			workerBox,
			collectedLine.ItemID,
			remainingQuantity,
			collectedLine.RequiredStatus,
			out WasteBinDock target,
			out int movable) == false)
		{
			TryRequestFullBinExport(building);
			return WorkPlanResult.Waiting;
		}

		line = new WorkLine(
			WorkLineAction.Put,
			target,
			target,
			collectedLine.ItemID,
			movable,
			requiredStatus: collectedLine.RequiredStatus,
			requiredQuality: ItemQuality.Waste,
			consumeSourcePickReservation: false);
		return WorkPlanResult.Issued;
	}

	public WorkPlanResult OnPlaceCompleted(
		AIWorker worker,
		WorkLine collectedLine,
		WorkLine placeLine,
		ItemTransferResult result)
	{
		if (worker == null || TryGetSessionBuilding(worker, out Building building) == false)
			return WorkPlanResult.Completed;
		if (building.State != BuildingState.Active)
			return WorkPlanResult.Waiting;

		if (result.Moved <= 0)
			return WorkPlanResult.Waiting;

		if (placeLine?.Target is WasteBinDock targetDock && targetDock.DockedCapsule is WasteBin wasteBin && wasteBin.IsFull)
		{
			wasteBin.SetLogisticsState(CapsuleLogisticsState.Waste);
			TryRequestFullBinExport(building);
		}

		BoxBase workerBox = worker.CarryingAbility?.CarryingBox;
		if (HasWastePayload(workerBox))
			return WorkPlanResult.Issued;

		NotifyBuildingChanged(building);
		return HasLooseWaste(building) && HasAvailableWasteBin(building)
			? WorkPlanResult.SwitchPhase
			: WorkPlanResult.Completed;
	}

	public void OnTaskInvalidated(ItemTransferTask task)
	{
		if (task == null || task.IsReevaluatingFacility)
			return;

		activeWorkerByTask.TryGetValue(task, out AIWorker worker);
		ReleaseSession(task, worker);
	}

	public void OnTaskCompleted(ItemTransferTask task)
	{
		if (task != null)
			ReleaseSession(task, task.OccupyWorker);
	}

	internal bool TryGetPreferredWorker(ItemTransferTask task, out AIWorker worker)
	{
		worker = null;
		if (task == null || activeWorkerByTask.TryGetValue(task, out AIWorker candidate) == false || candidate == null)
			return false;

		if (ReferenceEquals(task.OccupyWorker, candidate) ||
			(candidate.CurrentTask == null && candidate.CanAcceptGeneralTask(WorkerTask.TaskType.WasteCollection)))
		{
			worker = candidate;
			return true;
		}

		return false;
	}

	internal void OnTaskAssigned(ItemTransferTask task, AIWorker worker)
	{
		if (task == null || worker == null || activeBuildingByTask.TryGetValue(task, out uint buildingId) == false)
			return;

		if (activeWorkerByTask.TryGetValue(task, out AIWorker previousWorker) &&
			previousWorker != null &&
			ReferenceEquals(previousWorker, worker) == false)
		{
			if (activeBuildingByWorker.TryGetValue(previousWorker, out uint previousBuildingId) &&
				previousBuildingId == buildingId)
			{
				activeBuildingByWorker.Remove(previousWorker);
			}
		}

		activeWorkerByTask[task] = worker;
		activeWorkerByBuilding[buildingId] = worker;
		activeBuildingByWorker[worker] = buildingId;
		lastBuildingByWorker[worker] = buildingId;
	}

	public void OnCargoRelocationEnded(CapsuleRelocationTask task)
	{
		if (task == null || task.Reason != CapsuleRelocationReason.WasteExport)
			return;

		if (task.SourceDock is WasteBinDock &&
			GameContext.HasInstance &&
			GameContext.Instance.FacilityMgr.TryGetBuildingId(task.SourceDock, out uint sourceBuildingId) &&
			BuildingManager?.TryGetBuilding(sourceBuildingId, out Building building) == true)
		{
			NotifyBuildingChanged(building);
		}

		if (task.TargetDock is OutboundCargoPort outboundPort &&
			outboundPort.DockedCapsule?.RouteKind == CargoRouteKind.Waste)
		{
			TryRequestExternalExport(outboundPort);
		}
	}

	private void RegisterSchedulerHandler()
	{
		scheduler?.Register(
			GlobalScheduleBuildingId,
			ItemTransferScheduleMode.WasteCollection,
			WorkerTask.TaskType.WasteCollection,
			TryBuildTask);
	}

	private void BindDockEvents()
	{
		if (isBound || dockService == null)
			return;

		dockService.OnCapsuleDocked += HandleCapsuleDocked;
		dockService.OnCapsuleUndocked += HandleCapsuleUndocked;
		isBound = true;
	}

	private ItemTransferScheduleResult TryBuildTask(ItemTransferScheduleRequest request, out WorkerTask task)
	{
		task = null;
		AIWorker worker = request.Worker;
		if (worker == null ||
			worker.PrimaryBuildingId != 0 ||
			activeBuildingByWorker.ContainsKey(worker) ||
			worker.CanAcceptGeneralTask(request.TaskType) == false)
			return ItemTransferScheduleResult.WorkerRejected;

		CleanupDirtyBuildings();
		if (TrySelectBuilding(worker, out Building building) == false)
			return dirtyBuildingIds.Count > 0 ? ItemTransferScheduleResult.Waiting : ItemTransferScheduleResult.NoWork;

		ItemTransferTask itemTransferTask = new(
			WorkerTask.TaskType.WasteCollection,
			new ItemTransferJob(this, TransferObjectType.Item, TransferObjectType.Item, 0, worker));
		RegisterSession(itemTransferTask, worker, building.RuntimeBuildingId);
		task = itemTransferTask;
		return ItemTransferScheduleResult.Scheduled;
	}

	private bool TrySelectBuilding(AIWorker worker, out Building building)
	{
		building = null;
		if (worker == null || BuildingManager == null)
			return false;

		if (lastBuildingByWorker.TryGetValue(worker, out uint lastBuildingId) &&
			TryUseBuilding(worker, lastBuildingId, out building) &&
			TryGetClosestWasteDistance(worker, building, out _))
		{
			return true;
		}

		int bestDistance = int.MaxValue;
		foreach (uint candidateId in dirtyBuildingIds)
		{
			if (TryUseBuilding(worker, candidateId, out Building candidate) == false ||
				TryGetClosestWasteDistance(worker, candidate, out int distance) == false ||
				distance >= bestDistance)
			{
				continue;
			}

			building = candidate;
			bestDistance = distance;
		}

		return building != null;
	}

	private bool TryUseBuilding(AIWorker worker, uint buildingId, out Building building)
	{
		building = null;
		return buildingId != 0 &&
			activeWorkerByBuilding.ContainsKey(buildingId) == false &&
			BuildingManager != null &&
			BuildingManager.TryGetBuilding(buildingId, out building) &&
			building != null &&
			building.State == BuildingState.Active &&
			HasLooseWaste(building) &&
			HasAvailableWasteBin(building);
	}

	private bool TryGetSessionBuilding(AIWorker worker, out Building building)
	{
		building = null;
		return worker != null &&
			activeBuildingByWorker.TryGetValue(worker, out uint buildingId) &&
			BuildingManager != null &&
			BuildingManager.TryGetBuilding(buildingId, out building) &&
			building != null;
	}

	private void RegisterSession(ItemTransferTask task, AIWorker worker, uint buildingId)
	{
		if (task == null || worker == null || buildingId == 0)
			return;

		activeBuildingByTask[task] = buildingId;
		activeWorkerByTask[task] = worker;
		activeWorkerByBuilding[buildingId] = worker;
		activeBuildingByWorker[worker] = buildingId;
		lastBuildingByWorker[worker] = buildingId;
	}

	private void ReleaseSession(ItemTransferTask task, AIWorker fallbackWorker)
	{
		if (task == null || activeBuildingByTask.TryGetValue(task, out uint buildingId) == false)
			return;

		activeBuildingByTask.Remove(task);
		AIWorker worker = activeWorkerByTask.Remove(task, out AIWorker registeredWorker)
			? registeredWorker
			: fallbackWorker;
		if (worker != null && activeBuildingByWorker.TryGetValue(worker, out uint ownedBuildingId) && ownedBuildingId == buildingId)
			activeBuildingByWorker.Remove(worker);
		if (activeWorkerByBuilding.TryGetValue(buildingId, out AIWorker buildingWorker) &&
			(worker == null || ReferenceEquals(buildingWorker, worker)))
		{
			activeWorkerByBuilding.Remove(buildingId);
		}
		if (BuildingManager?.TryGetBuilding(buildingId, out Building building) == true)
			NotifyBuildingChanged(building);
	}

	private bool TryFindCollectLine(AIWorker worker, BoxBase workerBox, Building building, out WorkLine line)
	{
		line = null;
		if (worker == null || workerBox == null || building?.ItemIndex == null || HasAvailableWasteBin(building) == false)
			return false;

		IItemContainer bestContainer = null;
		IGridPlaceable bestTarget = null;
		ItemStack bestStack = null;
		int bestQuantity = 0;
		int bestDistance = int.MaxValue;

		foreach (var entry in building.ItemIndex.WasteQuantityByContainer)
		{
			IItemContainer container = entry.Key;
			if (IsWasteBinContainer(container) ||
				container is not IGridPlaceable target ||
				container is not IInteractionPoint interactionTarget)
				continue;

			if (InteractionPointSelector.TryGetInteractionPointInBuilding(
				interactionTarget,
				InteractionKind.Pick,
				worker.GridPosition,
				building.RuntimeBuildingId,
				out _,
				out int distance) == false)
			{
				continue;
			}

			for (int stackIndex = container.Stacks.Count - 1; stackIndex >= 0; --stackIndex)
			{
				ItemStack stack = container.Stacks[stackIndex];
				if (IsCollectableWaste(container, stack) == false)
					continue;

				int movable = ItemTransferUtility.GetMovableQuantity(
					container,
					workerBox,
					stack.ItemID,
					stack.Quantity,
					candidate => candidate.HasQuality(ItemQuality.Waste) && candidate.Status == stack.Status);
				if (movable <= 0 || (distance > bestDistance || (distance == bestDistance && movable <= bestQuantity)))
					continue;

				bestContainer = container;
				bestTarget = target;
				bestStack = stack;
				bestQuantity = movable;
				bestDistance = distance;
			}
		}

		if (bestContainer == null || bestTarget == null || bestStack == null || bestQuantity <= 0)
			return false;

		line = new WorkLine(
			WorkLineAction.Pick,
			bestContainer,
			bestTarget,
			bestStack.ItemID,
			bestQuantity,
			requiredStatus: bestStack.Status,
			requiredQuality: ItemQuality.Waste,
			consumeSourcePickReservation: false);
		return true;
	}

	private static bool TryFindWasteBinDock(
		AIWorker worker,
		Building building,
		IItemContainer source,
		uint itemId,
		int requested,
		ItemStatus? requiredStatus,
		out WasteBinDock target,
		out int movable)
	{
		target = null;
		movable = 0;
		if (worker == null || building == null || source == null || itemId == 0 || requested <= 0)
			return false;

		int bestDistance = int.MaxValue;
		for (int i = 0; i < building.OccupiedCapsuleBuffers.Count; ++i)
		{
			if (building.OccupiedCapsuleBuffers[i] is not WasteBinDock candidate ||
				candidate.DockedCapsule is not WasteBin wasteBin ||
				wasteBin.IsFull)
			{
				continue;
			}

			int candidateMovable = ItemTransferUtility.GetMovableQuantity(
				source,
				candidate,
				itemId,
				requested,
				stack => stack.HasQuality(ItemQuality.Waste) &&
					(requiredStatus.HasValue == false || stack.Status == requiredStatus.Value));
			if (candidateMovable <= 0 ||
				InteractionPointSelector.TryGetInteractionPointInBuilding(
					candidate,
					InteractionKind.Put,
					worker.GridPosition,
					building.RuntimeBuildingId,
					out _,
					out int distance) == false ||
				distance >= bestDistance)
			{
				continue;
			}

			target = candidate;
			movable = candidateMovable;
			bestDistance = distance;
		}

		return target != null && movable > 0;
	}

	private bool TryGetClosestWasteDistance(AIWorker worker, Building building, out int distance)
	{
		distance = int.MaxValue;
		if (worker == null || building?.ItemIndex == null)
			return false;

		bool found = false;
		foreach (var entry in building.ItemIndex.WasteQuantityByContainer)
		{
			IItemContainer container = entry.Key;
			if (IsWasteBinContainer(container) ||
				container is not IGridPlaceable ||
				container is not IInteractionPoint interactionTarget)
				continue;

			bool hasCollectableWaste = false;
			for (int stackIndex = 0; stackIndex < container.Stacks.Count; ++stackIndex)
			{
				if (IsCollectableWaste(container, container.Stacks[stackIndex]))
				{
					hasCollectableWaste = true;
					break;
				}
			}
			if (hasCollectableWaste == false)
				continue;

			if (InteractionPointSelector.TryGetInteractionPointInBuilding(
				interactionTarget,
				InteractionKind.Pick,
				worker.GridPosition,
				building.RuntimeBuildingId,
				out _,
				out int candidateDistance) == false)
			{
				continue;
			}

			found = true;
			distance = Mathf.Min(distance, candidateDistance);
		}

		return found;
	}

	private static bool HasAvailableWasteBin(Building building)
	{
		if (building == null)
			return false;

		for (int i = 0; i < building.OccupiedCapsuleBuffers.Count; ++i)
		{
			if (building.OccupiedCapsuleBuffers[i] is WasteBinDock dock &&
				dock.DockedCapsule is WasteBin bin &&
				bin.IsFull == false)
			{
				return true;
			}
		}

		return false;
	}

	private static bool HasLooseWaste(Building building)
	{
		if (building?.ItemIndex == null)
			return false;

		foreach (var entry in building.ItemIndex.WasteQuantityByContainer)
		{
			IItemContainer container = entry.Key;
			if (entry.Value <= 0 || IsWasteBinContainer(container))
				continue;

			for (int i = 0; i < container.Stacks.Count; ++i)
			{
				if (IsCollectableWaste(container, container.Stacks[i]))
					return true;
			}
		}

		return false;
	}

	private static bool IsCollectableWaste(IItemContainer container, ItemStack stack)
	{
		if (stack == null || stack.Quantity <= 0 || stack.HasQuality(ItemQuality.Waste) == false)
			return false;

		if (container is IItemPickReservable reservable && reservable.ItemToBePicked.GetValueOrDefault(stack.ItemID) > 0)
			return false;

		if (stack.Status != ItemStatus.Packed || GameContext.HasInstance == false)
			return true;

		BoxBase manifestOwner = container switch
		{
			BoxBase box => box,
			CapsuleBuffer buffer => buffer.DockedCapsule,
			_ => null,
		};
		if (manifestOwner == null ||
			GameContext.Instance.OBWorkflowSvc?.TryGetPickingManifest(manifestOwner, out PickingManifest manifest) != true)
		{
			return true;
		}

		for (int i = 0; i < manifest.Lines.Count; ++i)
		{
			PickingManifestLine line = manifest.Lines[i];
			if (line != null && line.ItemId == stack.ItemID && line.PackedQuantity > 0)
				return false;
		}

		return true;
	}

	private static bool IsWasteBinContainer(IItemContainer container)
	{
		return container is WasteBin ||
			container is WasteBinDock ||
			container is CapsuleBuffer buffer && buffer.DockedCapsule?.RouteKind == CargoRouteKind.Waste;
	}

	private static bool HasWastePayload(BoxBase box)
	{
		if (box == null)
			return false;

		for (int i = 0; i < box.Stacks.Count; ++i)
		{
			if (box.Stacks[i]?.HasQuality(ItemQuality.Waste) == true)
				return true;
		}

		return false;
	}

	private void CleanupDirtyBuildings()
	{
		if (dirtyBuildingIds.Count <= 0)
			return;

		List<uint> ids = new(dirtyBuildingIds);
		for (int i = 0; i < ids.Count; ++i)
		{
			uint buildingId = ids[i];
			if (BuildingManager == null ||
				BuildingManager.TryGetBuilding(buildingId, out Building building) == false ||
				building == null ||
				building.State != BuildingState.Active ||
				HasLooseWaste(building) == false)
			{
				dirtyBuildingIds.Remove(buildingId);
			}
		}
	}

	private void TryRequestFullBinExport(Building building)
	{
		if (building == null || GameContext.HasInstance == false)
			return;

		OutboundCargoPort target = null;
		for (int i = 0; i < building.OccupiedCargoPorts.Count; ++i)
		{
			if (building.OccupiedCargoPorts[i] is OutboundCargoPort candidate &&
				candidate.CanAcceptCargoRoute(CargoRouteKind.Waste) &&
				candidate.CanPutBox())
			{
				target = candidate;
				break;
			}
		}

		if (target == null)
			return;

		for (int i = 0; i < building.OccupiedCapsuleBuffers.Count; ++i)
		{
			if (building.OccupiedCapsuleBuffers[i] is not WasteBinDock source ||
				source.DockedCapsule is not WasteBin wasteBin ||
				wasteBin.IsFull == false)
			{
				continue;
			}

			wasteBin.SetLogisticsState(CapsuleLogisticsState.Waste);
			GameContext.Instance.CapsuleRelocateCoordinator.RequestSend(new CapsuleRelocateSendRequest(
				source,
				CapsuleDockState.WasteBin,
				CapsuleLogisticsState.Waste,
				CapsuleDockState.OB,
				CapsuleRelocateScope.SameBuilding,
				building.RuntimeBuildingId,
				onMatched: EnqueueWasteRelocation,
				requiredRouteKind: CargoRouteKind.Waste));
			return;
		}
	}

	private void TryRequestExternalExport(OutboundCargoPort source)
	{
		if (source?.DockedCapsule is not WasteBin wasteBin || wasteBin.IsFull == false || GameContext.HasInstance == false)
			return;

		uint sourceBuildingId = 0;
		GameContext.Instance.FacilityMgr?.TryGetBuildingId(source, out sourceBuildingId);
		wasteBin.SetLogisticsState(CapsuleLogisticsState.Waste);
		GameContext.Instance.CapsuleRelocateCoordinator.RequestSend(new CapsuleRelocateSendRequest(
			source,
			CapsuleDockState.OB,
			CapsuleLogisticsState.Waste,
			CapsuleDockState.WasteContainer,
			CapsuleRelocateScope.GlobalAllowed,
			sourceBuildingId,
			onMatched: EnqueueWasteRelocation,
			requiredRouteKind: CargoRouteKind.Waste));
	}

	private bool EnqueueWasteRelocation(CapsuleRelocateMatch match)
	{
		if (TaskManager == null || match.SourceDock == null || match.TargetDock == null)
			return false;

		TaskManager.EnqueueTask(new CapsuleRelocationTask(
			WorkerTask.TaskType.CargoTransfer,
			match.SourceDock,
			match.TargetDock,
			0,
			CapsuleRelocationReason.WasteExport));
		return true;
	}

	private void HandleCapsuleDocked(uint buildingId, CapsuleDock dock)
	{
		if (isRestoring)
			return;

		if (dock is OutboundCargoPort outboundPort &&
			outboundPort.DockedCapsule?.RouteKind == CargoRouteKind.Waste)
		{
			TryRequestExternalExport(outboundPort);
			return;
		}

		if (dock is WasteBinDock && BuildingManager?.TryGetBuilding(buildingId, out Building building) == true)
			NotifyBuildingChanged(building);
	}

	private void HandleCapsuleUndocked(uint buildingId, CapsuleDock dock)
	{
		if (isRestoring)
			return;

		if ((dock is WasteBinDock || dock is OutboundCargoPort) &&
			BuildingManager?.TryGetBuilding(buildingId, out Building building) == true)
		{
			NotifyBuildingChanged(building);
		}
	}
}
