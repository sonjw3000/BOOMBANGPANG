using System;
using System.Collections.Generic;
using UnityEngine;
using static WorkerTask;

public enum TaskInvalidationReason
{
	Unknown,
	FacilityInvalidated,
	PlayerDockPreemption,
	PlayerWorkerTakeover,
	DispatchInvalid,
	PayloadRecoveryFailed,
	SourceUnavailable,
	CoordinatorOwnershipLost,
	PayloadMissing,
	RestoreInvalidReference,
}

//[DefaultExecutionOrder(-100)]
public partial class TaskManager : MonoBehaviour
{
	private Dictionary<TaskType, LinkedList<WorkerTask>> taskQueue = new();
	private Dictionary<TaskType, LinkedList<WorkerTask>> taskOnProgress = new();
	private readonly LinkedList<WorkerTask> returnedTaskQueue = new();
	private readonly LinkedList<TaskBuildRequest> taskBuildQueue = new();
	private readonly Dictionary<object, LinkedListNode<TaskBuildRequest>> taskBuildRequestsByKey = new();
	private readonly HashSet<WorkerTask> facilityAffectedTasks = new();
	private readonly HashSet<CapsuleRelocationTask> playerPreemptedCapsuleTasks = new();
	private FacilityManager boundFacilityManager;

	private ProcessStatsCollector Stats => GameContext.Instance.ProcessStats;

	private InboundWorkflowService IBService => GameContext.Instance.IBWorkflowSvc;
	private OutboundWorkflowService OBService => GameContext.Instance.OBWorkflowSvc;

	public IReadOnlyDictionary<TaskType, LinkedList<WorkerTask>> TaskQueue => taskQueue;
	public IReadOnlyDictionary<TaskType, LinkedList<WorkerTask>> TaskOnProgress => taskOnProgress;
	public IReadOnlyCollection<WorkerTask> ReturnedTaskQueue => returnedTaskQueue;
	public IReadOnlyCollection<TaskBuildRequest> TaskBuildQueue => taskBuildQueue;

	private void Awake()
	{
		// initialize task queues
		foreach (TaskType type in System.Enum.GetValues(typeof(TaskType)))
		{
			taskQueue[type] = new();
			taskOnProgress[type] = new();
		}

	}
	
	public void EnqueueTask(WorkerTask task)
	{
		if (task == null || task.CurrentStatus != WorkerTask.Status.Ready)
			return;

		taskQueue[task.Type].AddLast(task);
		Stats.AddQueue(task.Type);
	}

	public bool EnqueueTaskBuildRequest(TaskBuildRequest request)
	{
		if (request == null || request.IsStillValid == false)
			return false;

		object key = request.RequestKey;
		if (key != null && taskBuildRequestsByKey.ContainsKey(key))
			return false;

		LinkedListNode<TaskBuildRequest> node = taskBuildQueue.AddLast(request);
		if (key != null)
			taskBuildRequestsByKey[key] = node;

		return true;
	}

	public bool CancelTaskBuildRequest(object requestKey)
	{
		if (requestKey == null || taskBuildRequestsByKey.TryGetValue(requestKey, out var node) == false)
			return false;

		RemoveTaskBuildRequest(node);
		return true;
	}

	private void ProcessTaskBuildQueue()
	{
		LinkedListNode<TaskBuildRequest> node = taskBuildQueue.First;
		while (node != null)
		{
			LinkedListNode<TaskBuildRequest> next = node.Next;
			TaskBuildRequest request = node.Value;
			if (request == null || request.IsStillValid == false)
			{
				RemoveTaskBuildRequest(node);
				node = next;
				continue;
			}

			if (request.TryBuild(out WorkerTask task) && task != null)
			{
				RemoveTaskBuildRequest(node);
				EnqueueTask(task);
				request.OnTaskQueued(task);
			}

			node = next;
		}
	}

	private void RemoveTaskBuildRequest(LinkedListNode<TaskBuildRequest> node)
	{
		if (node == null)
			return;

		object key = node.Value?.RequestKey;
		if (key != null)
			taskBuildRequestsByKey.Remove(key);

		taskBuildQueue.Remove(node);
	}

	public void BindFacilityInvalidation(FacilityManager facilityManager)
	{
		if (boundFacilityManager == facilityManager)
			return;

		UnbindFacilityInvalidation();
		boundFacilityManager = facilityManager;
		if (boundFacilityManager != null)
			boundFacilityManager.OnFacilityInvalidating += HandleFacilityInvalidating;
	}

	public void UnbindFacilityInvalidation()
	{
		if (boundFacilityManager != null)
			boundFacilityManager.OnFacilityInvalidating -= HandleFacilityInvalidating;

		boundFacilityManager = null;
	}

	public bool HasFacilityDependency(IFacility facility)
	{
		if (facility == null)
			return false;

		foreach (TaskBuildRequest request in taskBuildQueue)
		{
			if (request != null && request.IsStillValid && request.DependsOnFacility(facility))
				return true;
		}

		foreach (LinkedList<WorkerTask> queue in taskQueue.Values)
		{
			if (QueueDependsOnFacility(queue, facility))
				return true;
		}

		foreach (LinkedList<WorkerTask> queue in taskOnProgress.Values)
		{
			if (QueueDependsOnFacility(queue, facility))
				return true;
		}

		return QueueDependsOnFacility(returnedTaskQueue, facility);
	}

	private void HandleFacilityInvalidating(
		IFacility facility,
		FacilityInvalidationContext context)
	{
		if (facility == null)
			return;

		RemoveFacilityTaskBuildRequests(facility);
		CollectFacilityAffectedTasks(facility);

		foreach (WorkerTask task in facilityAffectedTasks)
		{
			if (task == null)
				continue;

			FacilityTaskInvalidationAction action = task.HandleFacilityInvalidating(facility, in context);
			switch (action)
			{
				case FacilityTaskInvalidationAction.Invalidate:
					InvalidateTask(task, TaskInvalidationReason.FacilityInvalidated);
					break;

				case FacilityTaskInvalidationAction.Reevaluate:
					if (task.CurrentStatus == WorkerTask.Status.Assigned)
						task.OccupyWorker?.ReevaluateTask(task);
					break;
			}
		}

		facilityAffectedTasks.Clear();
		if (facility is CapsuleDock dock && GameContext.HasInstance)
			GameContext.Instance.CapsuleRelocateCoordinator?.RemoveDock(dock);

		IBService?.OnFacilityInvalidating(facility, in context);
		OBService?.OnFacilityInvalidating(facility, in context);
		RemoveFacilityTaskBuildRequests(facility);
	}

	private void RemoveFacilityTaskBuildRequests(IFacility facility)
	{
		LinkedListNode<TaskBuildRequest> node = taskBuildQueue.First;
		while (node != null)
		{
			LinkedListNode<TaskBuildRequest> next = node.Next;
			if (node.Value == null || node.Value.DependsOnFacility(facility))
				RemoveTaskBuildRequest(node);

			node = next;
		}
	}

	private void CollectFacilityAffectedTasks(IFacility facility)
	{
		facilityAffectedTasks.Clear();
		foreach (LinkedList<WorkerTask> queue in taskQueue.Values)
			AddFacilityAffectedTasks(queue, facility);

		foreach (LinkedList<WorkerTask> queue in taskOnProgress.Values)
			AddFacilityAffectedTasks(queue, facility);

		AddFacilityAffectedTasks(returnedTaskQueue, facility);
	}

	private void AddFacilityAffectedTasks(IEnumerable<WorkerTask> tasks, IFacility facility)
	{
		foreach (WorkerTask task in tasks)
		{
			if (task != null && task.DependsOnFacility(facility))
				facilityAffectedTasks.Add(task);
		}
	}

	private static bool QueueDependsOnFacility(IEnumerable<WorkerTask> tasks, IFacility facility)
	{
		foreach (WorkerTask task in tasks)
		{
			if (task != null && task.DependsOnFacility(facility))
				return true;
		}

		return false;
	}

	public bool TryPreemptCapsuleDockForPlayer(CapsuleDock dock)
	{
		if (dock == null)
			return false;

		playerPreemptedCapsuleTasks.Clear();
		CollectCapsuleTasksUsingDock(taskQueue.Values, dock);
		CollectCapsuleTasksUsingDock(taskOnProgress.Values, dock);
		CollectCapsuleTasksUsingDock(returnedTaskQueue, dock);

		foreach (CapsuleRelocationTask task in playerPreemptedCapsuleTasks)
		{
			if (task == null)
				continue;

			switch (task.PreemptDockForPlayer(dock))
			{
				case CapsuleDockPlayerPreemptionAction.Reevaluate:
					if (task.CurrentStatus == WorkerTask.Status.Assigned)
						task.OccupyWorker?.ReevaluateTask(task);
					break;

				case CapsuleDockPlayerPreemptionAction.Invalidate:
					InvalidateTask(task, TaskInvalidationReason.PlayerDockPreemption);
					break;
			}
		}

		playerPreemptedCapsuleTasks.Clear();
		return true;
	}

	private void CollectCapsuleTasksUsingDock(
		IEnumerable<LinkedList<WorkerTask>> queues,
		CapsuleDock dock)
	{
		foreach (LinkedList<WorkerTask> queue in queues)
			CollectCapsuleTasksUsingDock(queue, dock);
	}

	private void CollectCapsuleTasksUsingDock(IEnumerable<WorkerTask> tasks, CapsuleDock dock)
	{
		foreach (WorkerTask task in tasks)
		{
			if (task is CapsuleRelocationTask relocationTask && relocationTask.UsesDock(dock))
				playerPreemptedCapsuleTasks.Add(relocationTask);
		}
	}

	// dispatch task to workers
	private void Dispatch()
	{
		DispatchReturnedTasks();

		// find tasks to do
		foreach (var (key, queue) in taskQueue)
		{
			var node = queue.First;
			while (node != null)
			{
				var next = node.Next;
				WorkerTask data = node.Value;
				if (data == null)
				{
					queue.Remove(node);
					node = next;
					continue;
				}

				if (data.IsValidForDispatch == false)
				{
					InvalidateTask(data, TaskInvalidationReason.DispatchInvalid);
					node = next;
					continue;
				}

				AIWorker worker = GameContext.Instance.WorkerMgr.GetAvailableWorkers(data);

				if (worker != null)
				{
					if (worker.SetTask(data))
					{
						queue.Remove(node);
						taskOnProgress[key].AddLast(data);
					}
				}

				node = next;
			}
		}
	}

	private void DispatchReturnedTasks()
	{
		LinkedListNode<WorkerTask> node = returnedTaskQueue.First;
		while (node != null)
		{
			LinkedListNode<WorkerTask> next = node.Next;
			WorkerTask task = node.Value;
			if (task == null || task.CurrentStatus != WorkerTask.Status.Returned)
			{
				returnedTaskQueue.Remove(node);
				node = next;
				continue;
			}

			if (task.IsValidForDispatch == false)
			{
				InvalidateTask(task, TaskInvalidationReason.DispatchInvalid);
				node = next;
				continue;
			}

			AIWorker worker = GameContext.Instance.WorkerMgr.GetAvailableWorkers(task);
			if (worker != null)
			{
				if (worker.SetTask(task))
				{
					returnedTaskQueue.Remove(node);
					taskOnProgress[task.Type].AddLast(task);
				}
			}

			node = next;
		}
	}

	public bool ReturnTask(AIWorker worker)
	{
		WorkerTask task = worker != null ? worker.CurrentTask : null;
		BoxBase recoveryBox = worker?.CarryingAbility?.CarryingBox;
		if (task == null || task.MarkReturned(worker, recoveryBox, worker.GridPosition) == false)
			return false;

		if (recoveryBox != null &&
			(worker.CarryingAbility.DropBoxForTaskRecovery(out BoxBase droppedBox) == false || droppedBox != recoveryBox))
		{
			InvalidateTask(task, TaskInvalidationReason.PayloadRecoveryFailed);
			worker.ClearTask(task, becomeIdle: false);
			return false;
		}

		taskOnProgress[task.Type].Remove(task);
		worker.ClearTask(task, becomeIdle: false);
		returnedTaskQueue.AddLast(task);

		if (task is ItemTransferTask && GameContext.HasInstance)
			GameContext.Instance.ItemTransferTaskScheduler.NotifyTaskReturned(task);

		return true;
	}

	public bool InvalidateTask(
		WorkerTask task,
		TaskInvalidationReason reason = TaskInvalidationReason.Unknown)
	{
		WorkerTask.Status previousStatus = task != null ? task.CurrentStatus : WorkerTask.Status.Invalidated;
		if (task == null || task.MarkInvalidated(out AIWorker worker) == false)
			return false;

		taskQueue[task.Type].Remove(task);
		returnedTaskQueue.Remove(task);
		taskOnProgress[task.Type].Remove(task);

		if (worker != null)
			worker.ClearTask(task, becomeIdle: worker.IsOperational);

		Stats.RemoveQueue(task.Type);
		if (task is ItemTransferTask && GameContext.HasInstance)
			GameContext.Instance.ItemTransferTaskScheduler.NotifyTaskInvalidated(task);

		NotifyWorkflowTaskInvalidated(task);
		NotifyBuildingCapsuleRelocationEnded(task);
		LogTaskInvalidation(task, worker, previousStatus, reason);

		return true;
	}

	private static void NotifyBuildingCapsuleRelocationEnded(WorkerTask task)
	{
		if (task is not CapsuleRelocationTask relocationTask ||
			relocationTask.BuildingId == 0 ||
			GameContext.HasInstance == false)
		{
			return;
		}

		BuildingManager buildingManager = GameContext.Instance.BuildingMgr;
		if (buildingManager != null &&
			buildingManager.TryGetBuilding(relocationTask.BuildingId, out Building building) &&
			building != null)
		{
			building.OnCapsuleRelocationTaskEnded(relocationTask);
		}
	}

	private static void LogTaskInvalidation(
		WorkerTask task,
		AIWorker worker,
		WorkerTask.Status previousStatus,
		TaskInvalidationReason reason)
	{
		string workerName = worker != null ? worker.Name : "None";
		if (task is CapsuleRelocationTask relocationTask)
		{
			string sourceName = relocationTask.SourceDock != null ? relocationTask.SourceDock.name : "None";
			string targetName = relocationTask.TargetDock != null ? relocationTask.TargetDock.name : "None";
			Debug.Log(
				$"[TaskInvalidated] type={task.Type}, reason={reason}, previous={previousStatus}, worker={workerName}, building={relocationTask.BuildingId}, source={sourceName}, target={targetName}");
			return;
		}

		Debug.Log(
			$"[TaskInvalidated] type={task.Type}, reason={reason}, previous={previousStatus}, worker={workerName}");
	}

	private void NotifyWorkflowTaskInvalidated(WorkerTask task)
	{
		if (task == null)
			return;

		switch (task.Type)
		{
			case TaskType.Unloading:
			case TaskType.IB:
			case TaskType.CapsuleClear:
			case TaskType.Labeling:
			case TaskType.Storing:
				IBService?.OnTaskInvalidated(task);
				break;

			case TaskType.CapsuleSupply:
			case TaskType.OB:
			case TaskType.Picking:
			case TaskType.Packing:
			case TaskType.Loading:
				OBService?.OnTaskInvalidated(task);
				break;

			case TaskType.CargoTransfer:
				if (task is CapsuleRelocationTask invalidatedRelocation &&
					invalidatedRelocation.RouteKind == CargoRouteKind.Waste)
					GameContext.Instance.WasteCollectionPlanner?.OnCargoRelocationEnded(invalidatedRelocation);
				else
					OBService?.OnTaskInvalidated(task);
				break;

			case TaskType.WasteCollection:
				break;
		}
	}

	public void CompleteTask(WorkerTask task)
	{
		if (task == null || task.MarkCompleted(out AIWorker worker) == false)
			return;

		taskOnProgress[task.Type].Remove(task);
		if (task is ItemTransferTask completedTransferTask)
			completedTransferTask.NotifyPlannerCompleted();
		worker.OnTaskCompleted();
		worker.ClearTask(task, becomeIdle: true);
		Stats.CompleteProcess(task.Type);

		if (task is ItemTransferTask && GameContext.HasInstance)
			GameContext.Instance.ItemTransferTaskScheduler.NotifyTaskCompleted(task);

		if (task is CapsuleRelocationTask relocationTask)
		{
			relocationTask.NotifyRelocationEnded();
			NotifyBuildingCapsuleRelocationEnded(relocationTask);
		}

		// todo
		//
		switch (task.Type)
		{
			// IB
			case TaskType.Unloading:
			case TaskType.IB:
			case TaskType.CapsuleClear:
			case TaskType.Labeling:
			//case TaskType.Receive:
			//case TaskType.Label:
			case TaskType.Storing:
				IBService.OnTaskCompleted(task);
				break;

			// OB
			case TaskType.CapsuleSupply:
			case TaskType.OB:
			case TaskType.Picking:
			case TaskType.Packing:
			//case TaskType.Sorting:
			//case TaskType.Packaging:
			case TaskType.Loading:
				OBService.OnTaskCompleted(task);
				break;

			case TaskType.CargoTransfer:
				if (task is CapsuleRelocationTask completedRelocation &&
					completedRelocation.RouteKind == CargoRouteKind.Waste)
					GameContext.Instance.WasteCollectionPlanner?.OnCargoRelocationEnded(completedRelocation);
				else
					OBService.OnTaskCompleted(task);
				break;

			case TaskType.PackingInput:
			case TaskType.PackingOutput:
			case TaskType.LaunchSort:
			case TaskType.WasteCollection:
				break;

			default:
				Debug.LogError("ERROR!! TaskType Undef on tskmgr end task");
				break;
		}
	}

	public void Update()
	{
		ProcessTaskBuildQueue();
		Dispatch();
	}

	public void AddRestoredInProgressTask(WorkerTask task)
	{
		if (task == null)
			return;

		taskOnProgress[task.Type].AddLast(task);
	}

	public void AddRestoredReturnedTask(WorkerTask task)
	{
		if (task == null)
			return;

		task.RestoreReturnedState();
		returnedTaskQueue.AddLast(task);
	}

	public int GetReturnedTaskCount(TaskType taskType)
	{
		int count = 0;
		foreach (WorkerTask task in returnedTaskQueue)
		{
			if (task != null && task.Type == taskType)
				++count;
		}

		return count;
	}

}
