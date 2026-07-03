using System;
using System.Collections.Generic;
using UnityEngine;

public enum ItemTransferScheduleMode
{
	Undefined = 0,
	PackingInput,
	PackingOutput,
}

public enum ItemTransferScheduleResult
{
	None = 0,
	Scheduled,
	NoWork,
	Waiting,
	WorkerRejected,
}

public readonly struct ItemTransferScheduleKey : IEquatable<ItemTransferScheduleKey>
{
	public readonly uint BuildingId;
	public readonly WorkerTask.TaskType TaskType;
	public readonly ItemTransferScheduleMode Mode;

	public ItemTransferScheduleKey(uint buildingId, WorkerTask.TaskType taskType, ItemTransferScheduleMode mode)
	{
		BuildingId = buildingId;
		TaskType = taskType;
		Mode = mode;
	}

	public bool Equals(ItemTransferScheduleKey other)
	{
		return BuildingId == other.BuildingId &&
			TaskType == other.TaskType &&
			Mode == other.Mode;
	}

	public override bool Equals(object obj)
	{
		return obj is ItemTransferScheduleKey other && Equals(other);
	}

	public override int GetHashCode()
	{
		return HashCode.Combine(BuildingId, TaskType, Mode);
	}

	public override string ToString()
	{
		return $"{BuildingId}:{TaskType}:{Mode}";
	}
}

public readonly struct ItemTransferScheduleRequest
{
	public readonly ItemTransferScheduleKey Key;
	public readonly AIWorker Worker;

	public uint BuildingId => Key.BuildingId;
	public WorkerTask.TaskType TaskType => Key.TaskType;
	public ItemTransferScheduleMode Mode => Key.Mode;

	public ItemTransferScheduleRequest(ItemTransferScheduleKey key, AIWorker worker)
	{
		Key = key;
		Worker = worker;
	}
}

public delegate ItemTransferScheduleResult ItemTransferTaskBuildHandler(
	ItemTransferScheduleRequest request,
	out WorkerTask task);

public sealed class ItemTransferTaskScheduler
{
	private sealed class WorkerQueue
	{
		public readonly LinkedList<AIWorker> Queue = new();
		public readonly HashSet<AIWorker> Set = new();
	}

	private readonly Dictionary<ItemTransferScheduleKey, ItemTransferTaskBuildHandler> handlersByKey = new();
	private readonly HashSet<ItemTransferScheduleKey> dirtyKeys = new();
	private readonly Dictionary<WorkerTask.TaskType, WorkerQueue> idleWorkersByTaskType = new();
	private readonly Dictionary<WorkerTask, ItemTransferScheduleKey> scheduledKeysByTask = new();

	private TaskManager TaskManager => GameContext.HasInstance ? GameContext.Instance.TaskMgr : null;

	public int DirtyCount => dirtyKeys.Count;
	public int HandlerCount => handlersByKey.Count;

	public bool Register(
		uint buildingId,
		ItemTransferScheduleMode mode,
		WorkerTask.TaskType taskType,
		ItemTransferTaskBuildHandler handler)
	{
		ItemTransferScheduleKey key = new(buildingId, taskType, mode);
		if (buildingId == 0 || mode == ItemTransferScheduleMode.Undefined || handler == null)
			return false;

		handlersByKey[key] = handler;
		return true;
	}

	public bool Unregister(uint buildingId, ItemTransferScheduleMode mode, WorkerTask.TaskType taskType)
	{
		ItemTransferScheduleKey key = new(buildingId, taskType, mode);
		dirtyKeys.Remove(key);
		return handlersByKey.Remove(key);
	}

	public void MarkDirty(uint buildingId, ItemTransferScheduleMode mode, WorkerTask.TaskType taskType)
	{
		ItemTransferScheduleKey key = new(buildingId, taskType, mode);
		if (IsValidKey(key) == false)
			return;

		dirtyKeys.Add(key);
		TrySchedule(key);
	}

	public void ClearDirty(uint buildingId, ItemTransferScheduleMode mode, WorkerTask.TaskType taskType)
	{
		dirtyKeys.Remove(new ItemTransferScheduleKey(buildingId, taskType, mode));
	}

	public void NotifyIdleWorker(AIWorker worker)
	{
		if (worker == null || worker.CurrentTask != null)
			return;

		for (int i = 0; i < worker.AssignedTaskTypes.Count; ++i)
		{
			WorkerTask.TaskType taskType = worker.AssignedTaskTypes[i];
			if (worker.CanAcceptGeneralTask(taskType) == false)
				continue;

			WorkerQueue queue = GetOrCreateWorkerQueue(taskType);
			if (queue.Set.Add(worker))
				queue.Queue.AddLast(worker);
		}

		TrySchedule(worker);
	}

	public void NotifyWorkerUnavailable(AIWorker worker)
	{
		RemoveIdleWorker(worker);
	}

	public void NotifyTaskCompleted(WorkerTask task)
	{
		if (task == null)
			return;

		if (scheduledKeysByTask.TryGetValue(task, out ItemTransferScheduleKey key) == false)
			return;

		scheduledKeysByTask.Remove(task);
		if (dirtyKeys.Contains(key))
			TrySchedule(key);
	}

	public bool HasDirty(uint buildingId, ItemTransferScheduleMode mode, WorkerTask.TaskType taskType)
	{
		return dirtyKeys.Contains(new ItemTransferScheduleKey(buildingId, taskType, mode));
	}

	public bool HasHandler(uint buildingId, ItemTransferScheduleMode mode, WorkerTask.TaskType taskType)
	{
		return handlersByKey.ContainsKey(new ItemTransferScheduleKey(buildingId, taskType, mode));
	}

	public int GetIdleWorkerCount(WorkerTask.TaskType taskType)
	{
		return idleWorkersByTaskType.TryGetValue(taskType, out WorkerQueue queue) ? queue.Set.Count : 0;
	}

	private bool TrySchedule(AIWorker worker)
	{
		if (worker == null || worker.CurrentTask != null)
			return false;

		foreach (ItemTransferScheduleKey key in CopyDirtyKeys())
		{
			if (CanWorkerTryKey(worker, key) == false)
				continue;

			if (TryBuildAndEnqueue(key, worker))
				return true;
		}

		return false;
	}

	private bool TrySchedule(ItemTransferScheduleKey key)
	{
		if (dirtyKeys.Contains(key) == false ||
			handlersByKey.ContainsKey(key) == false ||
			idleWorkersByTaskType.TryGetValue(key.TaskType, out WorkerQueue queue) == false)
		{
			return false;
		}

		while (dirtyKeys.Contains(key) && queue.Queue.Count > 0)
		{
			AIWorker worker = queue.Queue.First.Value;
			queue.Queue.RemoveFirst();
			queue.Set.Remove(worker);

			if (CanWorkerTryKey(worker, key) == false)
				continue;

			if (TryBuildAndEnqueue(key, worker))
				return true;
		}

		return false;
	}

	private bool TryBuildAndEnqueue(ItemTransferScheduleKey key, AIWorker worker)
	{
		if (TaskManager == null ||
			worker == null ||
			handlersByKey.TryGetValue(key, out ItemTransferTaskBuildHandler handler) == false)
		{
			return false;
		}

		ItemTransferScheduleRequest request = new(key, worker);
		ItemTransferScheduleResult result = handler(request, out WorkerTask task);
		switch (result)
		{
			case ItemTransferScheduleResult.Scheduled:
				if (task == null)
					return false;

				RemoveIdleWorker(worker);
				scheduledKeysByTask[task] = key;
				TaskManager.EnqueueTask(task);
				return true;

			case ItemTransferScheduleResult.NoWork:
				dirtyKeys.Remove(key);
				return false;

			case ItemTransferScheduleResult.WorkerRejected:
				return false;

			case ItemTransferScheduleResult.Waiting:
			case ItemTransferScheduleResult.None:
			default:
				return false;
		}
	}

	private bool CanWorkerTryKey(AIWorker worker, ItemTransferScheduleKey key)
	{
		return IsValidKey(key) &&
			worker != null &&
			worker.CurrentTask == null &&
			worker.CanAcceptGeneralTask(key.TaskType);
	}

	private static bool IsValidKey(ItemTransferScheduleKey key)
	{
		return key.BuildingId != 0 &&
			key.Mode != ItemTransferScheduleMode.Undefined &&
			key.TaskType != WorkerTask.TaskType.Undefined;
	}

	private WorkerQueue GetOrCreateWorkerQueue(WorkerTask.TaskType taskType)
	{
		if (idleWorkersByTaskType.TryGetValue(taskType, out WorkerQueue queue) == false)
		{
			queue = new WorkerQueue();
			idleWorkersByTaskType[taskType] = queue;
		}

		return queue;
	}

	private void RemoveIdleWorker(AIWorker worker)
	{
		if (worker == null)
			return;

		foreach (var entry in idleWorkersByTaskType)
		{
			WorkerQueue queue = entry.Value;
			if (queue.Set.Remove(worker))
				queue.Queue.Remove(worker);
		}
	}

	private List<ItemTransferScheduleKey> CopyDirtyKeys()
	{
		return new List<ItemTransferScheduleKey>(dirtyKeys);
	}
}
