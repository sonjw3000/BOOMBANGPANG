using System.Collections.Generic;
using UnityEngine;

// 1. 병목 측정

public readonly struct TaskCountSnapshot
{
	public int Ready { get; }
	public int Returned { get; }
	public int Active { get; }
	public int Blocked { get; }
	public int Waiting => Ready + Returned;
	public int Total => Waiting + Active;

	public TaskCountSnapshot(int ready, int returned, int active, int blocked)
	{
		Ready = ready;
		Returned = returned;
		Active = active;
		Blocked = blocked;
	}
}

public enum LogisticsWorkCategory
{
	Labeling,
	Picking,
	Storing,
	PackingInput,
	Packing,
	PackingOutput,
	LaunchSort,
	CapsuleRelocate,
}

// SourceCount is an owner-defined workload source, not a projected Task count.
// Keep it separate from TaskCountSnapshot because one Task may process multiple sources.
public readonly struct WorkDemandSnapshot
{
	public int SourceCount { get; }
	public int ItemQuantity { get; }
	public bool HasDemand => SourceCount > 0 || ItemQuantity > 0;

	public WorkDemandSnapshot(int sourceCount, int itemQuantity)
	{
		SourceCount = sourceCount;
		ItemQuantity = itemQuantity;
	}
}

public class MetricsService : MonoBehaviour
{
	private TaskManager TaskMgr => GameContext.Instance.TaskMgr;
	private IReadOnlyDictionary<WorkerTask.TaskType, LinkedList<WorkerTask>> TaskQueue
		=> TaskMgr.TaskQueue;
	private IReadOnlyDictionary<WorkerTask.TaskType, LinkedList<WorkerTask>> TaskOnProgress
		=> TaskMgr.TaskOnProgress;
	private IReadOnlyDictionary<OrderTotalStatus, LinkedList<Order>> OrderStatusMap
		=> GameContext.Instance.OrderMgr.OrderStatusMap;
	private WorkerManager WorkerMgr => GameContext.Instance.WorkerMgr;

	// workers
	public int GetQueueLength(WorkerTask.TaskType type)
		=> GetTaskCountSnapshot(type).Waiting;
	public int GetOnProgressLength(WorkerTask.TaskType type) => GetTaskCountSnapshot(type).Active;
	public int GetTaskWorkerStatusCount(WorkerTask.TaskType type, WorkerStatusAction statusAction)
		=> WorkerMgr.GetTaskWorkerStatusCount(type, statusAction);
	public int GetWorkerStatusCount(WorkerStatusAction statusAction)
		=> WorkerMgr.GetWorkerStatusCount(statusAction);

	public TaskCountSnapshot GetTaskCountSnapshot(WorkerTask.TaskType type)
	{
		TaskQueue.TryGetValue(type, out LinkedList<WorkerTask> readyTasks);
		TaskOnProgress.TryGetValue(type, out LinkedList<WorkerTask> activeTasks);

		return new TaskCountSnapshot(
			readyTasks?.Count ?? 0,
			TaskMgr.GetReturnedTaskCount(type),
			activeTasks?.Count ?? 0,
			CountBlockedTasks(activeTasks));
	}

	public TaskCountSnapshot GetTaskCountSnapshot(LogisticsWorkCategory category)
	{
		return category switch
		{
			LogisticsWorkCategory.Labeling => GetTaskCountSnapshot(WorkerTask.TaskType.Labeling),
			LogisticsWorkCategory.Picking => GetTaskCountSnapshot(WorkerTask.TaskType.Picking),
			LogisticsWorkCategory.Storing => GetTaskCountSnapshot(WorkerTask.TaskType.Storing),
			LogisticsWorkCategory.PackingInput => GetTaskCountSnapshot(WorkerTask.TaskType.PackingInput),
			LogisticsWorkCategory.Packing => GetTaskCountSnapshot(WorkerTask.TaskType.Packing),
			LogisticsWorkCategory.PackingOutput => GetTaskCountSnapshot(WorkerTask.TaskType.PackingOutput),
			LogisticsWorkCategory.LaunchSort => GetTaskCountSnapshot(WorkerTask.TaskType.LaunchSort),
			LogisticsWorkCategory.CapsuleRelocate => GetCapsuleRelocationTaskCountSnapshot(),
			_ => default,
		};
	}

	// Building ID 0 is Hub / Unassigned, including tasks whose former owner is no longer registered.
	public TaskCountSnapshot GetTaskCountSnapshot(LogisticsWorkCategory category, uint buildingId)
	{
		BuildingManager buildingManager = GameContext.Instance.BuildingMgr;
		if (buildingId != 0 &&
			(buildingManager == null || buildingManager.TryGetBuilding(buildingId, out _) == false))
		{
			return default;
		}

		return new TaskCountSnapshot(
			CountTasks(TaskQueue.Values, category, buildingId),
			CountTasks(TaskMgr.ReturnedTaskQueue, category, buildingId),
			CountTasks(TaskOnProgress.Values, category, buildingId),
			CountBlockedTasks(TaskOnProgress.Values, category, buildingId));
	}

	public WorkDemandSnapshot GetWorkDemandSnapshot(LogisticsWorkCategory category)
	{
		return category switch
		{
			LogisticsWorkCategory.Labeling => GetLabelingDemandSnapshot(),
			LogisticsWorkCategory.Picking => GetPickingDemandSnapshot(),
			LogisticsWorkCategory.Storing => GetStoringDemandSnapshot(),
			LogisticsWorkCategory.PackingInput => GetPackingInputDemandSnapshot(),
			LogisticsWorkCategory.Packing => GetPackingDemandSnapshot(),
			LogisticsWorkCategory.PackingOutput => GetPackingOutputDemandSnapshot(),
			LogisticsWorkCategory.LaunchSort => GetLaunchSortDemandSnapshot(),
			LogisticsWorkCategory.CapsuleRelocate => GetCapsuleRelocateDemandSnapshot(),
			_ => default,
		};
	}

	// Building ID 0 is Hub / Unassigned, not an all-buildings filter.
	public WorkDemandSnapshot GetWorkDemandSnapshot(LogisticsWorkCategory category, uint buildingId)
	{
		if (buildingId == 0)
			return GetUnassignedWorkDemandSnapshot(category);

		BuildingManager buildingManager = GameContext.Instance.BuildingMgr;
		if (buildingManager == null ||
			buildingManager.TryGetBuilding(buildingId, out Building building) == false)
		{
			return default;
		}

		return GetBuildingWorkDemandSnapshot(category, building);
	}

	public TaskCountSnapshot GetCapsuleRelocationTaskCountSnapshot()
	{
		return new TaskCountSnapshot(
			CountTasks<CapsuleRelocationTask>(TaskQueue.Values),
			CountTasks<CapsuleRelocationTask>(TaskMgr.ReturnedTaskQueue),
			CountTasks<CapsuleRelocationTask>(TaskOnProgress.Values),
			CountBlockedTasks<CapsuleRelocationTask>(TaskOnProgress.Values));
	}

	private static WorkDemandSnapshot GetPickingDemandSnapshot()
	{
		int sourceCount = 0;
		int itemQuantity = 0;
		GameContext context = GameContext.Instance;

		if (context.OrderMgr != null)
		{
			context.OrderMgr.GetPendingPickingDemand(out int orderSources, out int orderQuantity);
			sourceCount += orderSources;
			itemQuantity += orderQuantity;
		}

		IEnumerable<PickingPlanner> planners = context.OBWorkflowSvc?.PickingPlanners;
		if (planners != null)
		{
			foreach (PickingPlanner planner in planners)
			{
				if (planner == null)
					continue;

				planner.GetPendingDemand(out int plannerSources, out int plannerQuantity);
				sourceCount += plannerSources;
				itemQuantity += plannerQuantity;
			}
		}

		return new WorkDemandSnapshot(sourceCount, itemQuantity);
	}

	private WorkDemandSnapshot GetUnassignedWorkDemandSnapshot(LogisticsWorkCategory category)
	{
		WorkDemandSnapshot all = GetWorkDemandSnapshot(category);
		int assignedSourceCount = 0;
		int assignedItemQuantity = 0;
		IReadOnlyList<Building> buildings = GameContext.Instance.BuildingMgr?.RegisteredBuildings;

		if (buildings != null)
		{
			for (int i = 0; i < buildings.Count; ++i)
			{
				Building building = buildings[i];
				if (building == null || building.RuntimeBuildingId == 0)
					continue;

				WorkDemandSnapshot assigned = GetBuildingWorkDemandSnapshot(category, building);
				assignedSourceCount += assigned.SourceCount;
				assignedItemQuantity += assigned.ItemQuantity;
			}
		}

		return new WorkDemandSnapshot(
			Mathf.Max(0, all.SourceCount - assignedSourceCount),
			Mathf.Max(0, all.ItemQuantity - assignedItemQuantity));
	}

	private static WorkDemandSnapshot GetBuildingWorkDemandSnapshot(
		LogisticsWorkCategory category,
		Building building)
	{
		if (building == null || building.RuntimeBuildingId == 0)
			return default;

		return category switch
		{
			LogisticsWorkCategory.Labeling => GetLabelingDemandSnapshot(building.RuntimeBuildingId),
			LogisticsWorkCategory.Picking => GetPickingDemandSnapshot(building.RuntimeBuildingId),
			LogisticsWorkCategory.Storing => GetStoringDemandSnapshot(building.RuntimeBuildingId),
			LogisticsWorkCategory.PackingInput => GetPackingTransferDemand(building.RuntimeBuildingId, input: true),
			LogisticsWorkCategory.Packing => GetPackingDemandSnapshot(building.RuntimeBuildingId),
			LogisticsWorkCategory.PackingOutput => GetPackingTransferDemand(building.RuntimeBuildingId, input: false),
			LogisticsWorkCategory.LaunchSort => GetLaunchSortDemandSnapshot(building.RuntimeBuildingId),
			LogisticsWorkCategory.CapsuleRelocate => GetCapsuleRelocateDemandSnapshot(building.RuntimeBuildingId),
			_ => default,
		};
	}

	private static WorkDemandSnapshot GetPickingDemandSnapshot(uint buildingId)
	{
		OutboundWorkflowService outbound = GameContext.Instance.OBWorkflowSvc;
		if (outbound == null || outbound.TryGetPickingPlanner(buildingId, out PickingPlanner planner) == false)
			return default;

		planner.GetPendingDemand(out int sourceCount, out int itemQuantity);
		return new WorkDemandSnapshot(sourceCount, itemQuantity);
	}

	private static WorkDemandSnapshot GetLabelingDemandSnapshot()
	{
		InboundWorkflowService inbound = GameContext.Instance.IBWorkflowSvc;
		if (inbound == null)
			return default;

		inbound.GetLabelingDemand(out int sourceCount, out int itemQuantity);
		return new WorkDemandSnapshot(sourceCount, itemQuantity);
	}

	private static WorkDemandSnapshot GetLabelingDemandSnapshot(uint buildingId)
	{
		InboundWorkflowService inbound = GameContext.Instance.IBWorkflowSvc;
		if (inbound == null)
			return default;

		inbound.GetLabelingDemand(buildingId, out int sourceCount, out int itemQuantity);
		return new WorkDemandSnapshot(sourceCount, itemQuantity);
	}

	private static WorkDemandSnapshot GetStoringDemandSnapshot()
	{
		int sourceCount = 0;
		int itemQuantity = 0;
		GameContext context = GameContext.Instance;
		StoringPlanner planner = context.IBWorkflowSvc?.StoringPlanner;
		if (planner == null)
			return default;

		planner.GetPendingDemand(0, out sourceCount, out itemQuantity);

		return new WorkDemandSnapshot(sourceCount, itemQuantity);
	}

	private static WorkDemandSnapshot GetStoringDemandSnapshot(uint buildingId)
	{
		StoringPlanner planner = GameContext.Instance.IBWorkflowSvc?.StoringPlanner;
		if (planner == null || buildingId == 0)
			return default;

		planner.GetPendingDemand(
			buildingId,
			out int sourceCount,
			out int itemQuantity);
		return new WorkDemandSnapshot(sourceCount, itemQuantity);
	}

	private static WorkDemandSnapshot GetPackingInputDemandSnapshot()
	{
		return GetPackingTransferDemand(input: true);
	}

	private static WorkDemandSnapshot GetPackingOutputDemandSnapshot()
	{
		return GetPackingTransferDemand(input: false);
	}

	private static WorkDemandSnapshot GetPackingTransferDemand(bool input)
	{
		OutboundWorkflowService outbound = GameContext.Instance.OBWorkflowSvc;
		if (outbound == null)
			return default;

		int sourceCount;
		int itemQuantity;
		if (input)
			outbound.GetPackingInputDemand(out sourceCount, out itemQuantity);
		else
			outbound.GetPackingOutputDemand(out sourceCount, out itemQuantity);

		return new WorkDemandSnapshot(sourceCount, itemQuantity);
	}

	private static WorkDemandSnapshot GetPackingTransferDemand(uint buildingId, bool input)
	{
		OutboundWorkflowService outbound = GameContext.Instance.OBWorkflowSvc;
		if (outbound == null || buildingId == 0)
			return default;

		int sourceCount;
		int itemQuantity;
		if (input)
			outbound.GetPackingInputDemand(buildingId, out sourceCount, out itemQuantity);
		else
			outbound.GetPackingOutputDemand(buildingId, out sourceCount, out itemQuantity);

		return new WorkDemandSnapshot(sourceCount, itemQuantity);
	}

	private static WorkDemandSnapshot GetPackingDemandSnapshot()
	{
		PackingStationService service = GameContext.Instance.OBWorkflowSvc?.PackingStationService;
		if (service == null)
			return default;

		service.GetPendingPackingDemand(out int sourceCount, out int itemQuantity);
		return new WorkDemandSnapshot(sourceCount, itemQuantity);
	}

	private static WorkDemandSnapshot GetPackingDemandSnapshot(uint buildingId)
	{
		PackingStationService service = GameContext.Instance.OBWorkflowSvc?.PackingStationService;
		if (service == null)
			return default;

		service.GetPendingPackingDemand(buildingId, out int sourceCount, out int itemQuantity);
		return new WorkDemandSnapshot(sourceCount, itemQuantity);
	}

	private static WorkDemandSnapshot GetCapsuleRelocateDemandSnapshot()
	{
		CapsuleRelocateDemandSnapshot demand = GameContext.Instance.CapsuleRelocateCoordinator.GetDemandSnapshot();
		return new WorkDemandSnapshot(demand.SourceCount, 0);
	}

	private static WorkDemandSnapshot GetLaunchSortDemandSnapshot()
	{
		int sourceCount = 0;
		int itemQuantity = 0;
		IEnumerable<LaunchSortPlanner> planners = GameContext.Instance.OBWorkflowSvc?.LaunchSortPlanners;
		if (planners == null)
			return default;

		foreach (LaunchSortPlanner planner in planners)
		{
			planner.GetPendingDemand(out int plannerSources, out int plannerQuantity);
			sourceCount += plannerSources;
			itemQuantity += plannerQuantity;
		}

		return new WorkDemandSnapshot(sourceCount, itemQuantity);
	}

	private static WorkDemandSnapshot GetLaunchSortDemandSnapshot(uint buildingId)
	{
		OutboundWorkflowService outbound = GameContext.Instance.OBWorkflowSvc;
		if (outbound == null || outbound.TryGetLaunchSortPlanner(buildingId, out LaunchSortPlanner planner) == false)
			return default;

		planner.GetPendingDemand(out int sourceCount, out int itemQuantity);
		return new WorkDemandSnapshot(sourceCount, itemQuantity);
	}

	private static WorkDemandSnapshot GetCapsuleRelocateDemandSnapshot(uint buildingId)
	{
		CapsuleRelocateDemandSnapshot demand =
			GameContext.Instance.CapsuleRelocateCoordinator.GetDemandSnapshot(buildingId);
		return new WorkDemandSnapshot(demand.SourceCount, 0);
	}

	private static int CountTasks<TTask>(IEnumerable<WorkerTask> tasks) where TTask : WorkerTask
	{
		if (tasks == null)
			return 0;

		int count = 0;
		foreach (WorkerTask task in tasks)
		{
			if (task is TTask)
				++count;
		}

		return count;
	}

	private static int CountTasks<TTask>(IEnumerable<LinkedList<WorkerTask>> taskQueues) where TTask : WorkerTask
	{
		if (taskQueues == null)
			return 0;

		int count = 0;
		foreach (LinkedList<WorkerTask> tasks in taskQueues)
			count += CountTasks<TTask>(tasks);

		return count;
	}

	private static int CountTasks(
		IEnumerable<WorkerTask> tasks,
		LogisticsWorkCategory category,
		uint buildingId)
	{
		if (tasks == null)
			return 0;

		int count = 0;
		foreach (WorkerTask task in tasks)
		{
			if (IsTaskInScope(task, category, buildingId))
				++count;
		}

		return count;
	}

	private static int CountTasks(
		IEnumerable<LinkedList<WorkerTask>> taskQueues,
		LogisticsWorkCategory category,
		uint buildingId)
	{
		if (taskQueues == null)
			return 0;

		int count = 0;
		foreach (LinkedList<WorkerTask> tasks in taskQueues)
			count += CountTasks(tasks, category, buildingId);

		return count;
	}

	private static int CountBlockedTasks(IEnumerable<WorkerTask> tasks)
	{
		if (tasks == null)
			return 0;

		int count = 0;
		foreach (WorkerTask task in tasks)
		{
			if (IsTaskBlocked(task))
				++count;
		}

		return count;
	}

	private static int CountBlockedTasks<TTask>(IEnumerable<LinkedList<WorkerTask>> taskQueues) where TTask : WorkerTask
	{
		if (taskQueues == null)
			return 0;

		int count = 0;
		foreach (LinkedList<WorkerTask> tasks in taskQueues)
		{
			foreach (WorkerTask task in tasks)
			{
				if (task is TTask && IsTaskBlocked(task))
					++count;
			}
		}

		return count;
	}

	private static int CountBlockedTasks(
		IEnumerable<LinkedList<WorkerTask>> taskQueues,
		LogisticsWorkCategory category,
		uint buildingId)
	{
		if (taskQueues == null)
			return 0;

		int count = 0;
		foreach (LinkedList<WorkerTask> tasks in taskQueues)
		{
			foreach (WorkerTask task in tasks)
			{
				if (IsTaskInScope(task, category, buildingId) && IsTaskBlocked(task))
					++count;
			}
		}

		return count;
	}

	private static bool IsTaskInScope(
		WorkerTask task,
		LogisticsWorkCategory category,
		uint buildingId)
	{
		return IsTaskInCategory(task, category) && ResolveTaskBuildingId(task) == buildingId;
	}

	private static bool IsTaskInCategory(WorkerTask task, LogisticsWorkCategory category)
	{
		if (task == null)
			return false;

		return category switch
		{
			LogisticsWorkCategory.Labeling => task.Type == WorkerTask.TaskType.Labeling,
			LogisticsWorkCategory.Picking => task.Type == WorkerTask.TaskType.Picking,
			LogisticsWorkCategory.Storing => task.Type == WorkerTask.TaskType.Storing,
			LogisticsWorkCategory.PackingInput => task.Type == WorkerTask.TaskType.PackingInput,
			LogisticsWorkCategory.Packing => task.Type == WorkerTask.TaskType.Packing,
			LogisticsWorkCategory.PackingOutput => task.Type == WorkerTask.TaskType.PackingOutput,
			LogisticsWorkCategory.LaunchSort => task.Type == WorkerTask.TaskType.LaunchSort,
			LogisticsWorkCategory.CapsuleRelocate => task is CapsuleRelocationTask,
			_ => false,
		};
	}

	private static uint ResolveTaskBuildingId(WorkerTask task)
	{
		uint candidateBuildingId = task switch
		{
			ItemTransferTask itemTransferTask => itemTransferTask.BuildingId,
			LabelingTask labelingTask => labelingTask.BuildingId,
			PickingTask pickingTask => pickingTask.BuildingId,
			StoringTask storingTask => storingTask.BuildingId,
			PackingTask packingTask => ResolvePackingTaskBuildingId(packingTask),
			CapsuleRelocationTask relocationTask => relocationTask.BuildingId,
			_ => 0,
		};

		BuildingManager buildingManager = GameContext.Instance.BuildingMgr;
		return candidateBuildingId != 0 &&
			buildingManager != null &&
			buildingManager.TryGetBuilding(candidateBuildingId, out _)
				? candidateBuildingId
				: 0;
	}

	private static uint ResolvePackingTaskBuildingId(PackingTask task)
	{
		FacilityManager facilityManager = GameContext.Instance.FacilityMgr;
		return task?.TargetStation != null &&
			facilityManager != null &&
			facilityManager.TryGetBuildingId(task.TargetStation, out uint buildingId)
				? buildingId
				: 0;
	}

	private static bool IsTaskBlocked(WorkerTask task)
	{
		AIWorker worker = task?.OccupyWorker;
		if (worker == null)
			return false;
		if (worker.IsTrafficBlocked)
			return true;

		return worker.WorkerState.Action is
			WorkerStatusAction.WaitingForItems or
			WorkerStatusAction.WaitingForTargetBuilding or
			WorkerStatusAction.TrafficBlock or
			WorkerStatusAction.WaitingForNavigationCoverage or
			WorkerStatusAction.WaitingForOrchestrationCapacity or
			WorkerStatusAction.BlockedByCasualty;
	}

	// orders
	public int GetOrderStatusLength(OrderTotalStatus status) => OrderStatusMap[status].Count;
}
