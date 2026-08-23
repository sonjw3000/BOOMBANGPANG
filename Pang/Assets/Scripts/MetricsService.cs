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

	public TaskCountSnapshot GetCapsuleRelocationTaskCountSnapshot()
	{
		return new TaskCountSnapshot(
			CountTasks<CapsuleRelocationTask>(TaskQueue.Values),
			CountTasks<CapsuleRelocationTask>(TaskMgr.ReturnedTaskQueue),
			CountTasks<CapsuleRelocationTask>(TaskOnProgress.Values),
			CountBlockedTasks<CapsuleRelocationTask>(TaskOnProgress.Values));
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
