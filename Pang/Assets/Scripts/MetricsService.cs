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
	Picking,
	Storing,
	PackingInput,
	Packing,
	PackingOutput,
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
			LogisticsWorkCategory.Picking => GetTaskCountSnapshot(WorkerTask.TaskType.Picking),
			LogisticsWorkCategory.Storing => GetTaskCountSnapshot(WorkerTask.TaskType.Storing),
			LogisticsWorkCategory.PackingInput => GetTaskCountSnapshot(WorkerTask.TaskType.PackingInput),
			LogisticsWorkCategory.Packing => GetTaskCountSnapshot(WorkerTask.TaskType.Packing),
			LogisticsWorkCategory.PackingOutput => GetTaskCountSnapshot(WorkerTask.TaskType.PackingOutput),
			LogisticsWorkCategory.CapsuleRelocate => GetCapsuleRelocationTaskCountSnapshot(),
			_ => default,
		};
	}

	public WorkDemandSnapshot GetWorkDemandSnapshot(LogisticsWorkCategory category)
	{
		return category switch
		{
			LogisticsWorkCategory.Picking => GetPickingDemandSnapshot(),
			LogisticsWorkCategory.Storing => GetStoringDemandSnapshot(),
			LogisticsWorkCategory.PackingInput => GetPackingInputDemandSnapshot(),
			LogisticsWorkCategory.Packing => GetPackingDemandSnapshot(),
			LogisticsWorkCategory.PackingOutput => GetPackingOutputDemandSnapshot(),
			LogisticsWorkCategory.CapsuleRelocate => GetCapsuleRelocateDemandSnapshot(),
			_ => default,
		};
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

		IReadOnlyList<Building> buildings = context.BuildingMgr?.RegisteredBuildings;
		if (buildings != null)
		{
			for (int i = 0; i < buildings.Count; ++i)
			{
				if (buildings[i] is not StorageBuilding storageBuilding || storageBuilding.PickingPlanner == null)
					continue;

				storageBuilding.PickingPlanner.GetPendingDemand(out int plannerSources, out int plannerQuantity);
				sourceCount += plannerSources;
				itemQuantity += plannerQuantity;
			}
		}

		return new WorkDemandSnapshot(sourceCount, itemQuantity);
	}

	private static WorkDemandSnapshot GetStoringDemandSnapshot()
	{
		int sourceCount = 0;
		int itemQuantity = 0;
		GameContext context = GameContext.Instance;
		StoringPlanner planner = context.IBWorkflowSvc?.StoringPlanner;
		IReadOnlyList<Building> buildings = context.BuildingMgr?.RegisteredBuildings;
		if (planner == null || buildings == null)
			return default;

		for (int i = 0; i < buildings.Count; ++i)
		{
			if (buildings[i] is not StorageBuilding storageBuilding || storageBuilding.RuntimeBuildingId == 0)
				continue;

			planner.GetPendingDemand(storageBuilding.RuntimeBuildingId, out int buildingSources, out int buildingQuantity);
			sourceCount += buildingSources;
			itemQuantity += buildingQuantity;
		}

		return new WorkDemandSnapshot(sourceCount, itemQuantity);
	}

	private static WorkDemandSnapshot GetPackingInputDemandSnapshot()
	{
		return GetPackingBuildingDemand(input: true);
	}

	private static WorkDemandSnapshot GetPackingOutputDemandSnapshot()
	{
		return GetPackingBuildingDemand(input: false);
	}

	private static WorkDemandSnapshot GetPackingBuildingDemand(bool input)
	{
		int sourceCount = 0;
		int itemQuantity = 0;
		IReadOnlyList<Building> buildings = GameContext.Instance.BuildingMgr?.RegisteredBuildings;
		if (buildings == null)
			return default;

		for (int i = 0; i < buildings.Count; ++i)
		{
			if (buildings[i] is not PackingBuilding packingBuilding)
				continue;

			int buildingSources;
			int buildingQuantity;
			if (input)
				packingBuilding.GetPackingInputDemand(out buildingSources, out buildingQuantity);
			else
				packingBuilding.GetPackingOutputDemand(out buildingSources, out buildingQuantity);

			sourceCount += buildingSources;
			itemQuantity += buildingQuantity;
		}

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

	private static WorkDemandSnapshot GetCapsuleRelocateDemandSnapshot()
	{
		CapsuleRelocateDemandSnapshot demand = GameContext.Instance.CapsuleRelocateCoordinator.GetDemandSnapshot();
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
