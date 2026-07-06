using System;
using System.Collections.Generic;
using UnityEngine;

public enum ItemTransferScheduleMode
{
	Undefined = 0,
	Picking,
	Storing,
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
	public readonly ItemTransferScheduleMode Mode;

	public ItemTransferScheduleKey(uint buildingId, ItemTransferScheduleMode mode)
	{
		BuildingId = buildingId;
		Mode = mode;
	}

	public bool Equals(ItemTransferScheduleKey other)
	{
		return BuildingId == other.BuildingId &&
			Mode == other.Mode;
	}

	public override bool Equals(object obj)
	{
		return obj is ItemTransferScheduleKey other && Equals(other);
	}

	public override int GetHashCode()
	{
		return HashCode.Combine(BuildingId, Mode);
	}

	public override string ToString()
	{
		return $"{BuildingId}:{Mode}";
	}
}

public readonly struct ItemTransferScheduleRequest
{
	public readonly ItemTransferScheduleKey Key;
	public readonly WorkerTask.TaskType TaskType;
	public readonly AIWorker Worker;

	public uint BuildingId => Key.BuildingId;
	public ItemTransferScheduleMode Mode => Key.Mode;

	public ItemTransferScheduleRequest(ItemTransferScheduleKey key, WorkerTask.TaskType taskType, AIWorker worker)
	{
		Key = key;
		TaskType = taskType;
		Worker = worker;
	}
}

public delegate ItemTransferScheduleResult ItemTransferTaskBuildHandler(
	ItemTransferScheduleRequest request,
	out WorkerTask task);

public sealed class ItemTransferTaskScheduler
{
	private sealed class ScheduleEntry
	{
		public readonly WorkerTask.TaskType TaskType;
		public readonly ItemTransferTaskBuildHandler Handler;

		public ScheduleEntry(WorkerTask.TaskType taskType, ItemTransferTaskBuildHandler handler)
		{
			TaskType = taskType;
			Handler = handler;
		}
	}

	private sealed class WorkerQueue
	{
		public readonly LinkedList<AIWorker> Queue = new();
		public readonly HashSet<AIWorker> Set = new();
	}

	private readonly Dictionary<ItemTransferScheduleKey, ScheduleEntry> entriesByKey = new();
	private readonly HashSet<ItemTransferScheduleKey> dirtyKeys = new();
	private readonly Dictionary<WorkerTask.TaskType, WorkerQueue> idleWorkersByTaskType = new();
	private readonly Dictionary<WorkerTask, ItemTransferScheduleKey> scheduledKeysByTask = new();
	private readonly Dictionary<WorkerTask, AIWorker> scheduledWorkersByTask = new();
	private readonly HashSet<AIWorker> reservedWorkers = new();

	private TaskManager TaskManager => GameContext.HasInstance ? GameContext.Instance.TaskMgr : null;

	public int DirtyCount => dirtyKeys.Count;
	public int HandlerCount => entriesByKey.Count;

	public bool Register(
		uint buildingId,
		ItemTransferScheduleMode mode,
		WorkerTask.TaskType taskType,
		ItemTransferTaskBuildHandler handler)
	{
		ItemTransferScheduleKey key = new(buildingId, mode);
		if (buildingId == 0 ||
			mode == ItemTransferScheduleMode.Undefined ||
			taskType == WorkerTask.TaskType.Undefined ||
			handler == null)
		{
			return false;
		}

		entriesByKey[key] = new ScheduleEntry(taskType, handler);
		if (dirtyKeys.Contains(key))
			TryScheduleDirtyKeys();

		return true;
	}

	public bool Unregister(uint buildingId, ItemTransferScheduleMode mode)
	{
		ItemTransferScheduleKey key = new(buildingId, mode);
		dirtyKeys.Remove(key);
		return entriesByKey.Remove(key);
	}

	public void MarkDirty(uint buildingId, ItemTransferScheduleMode mode)
	{
		ItemTransferScheduleKey key = new(buildingId, mode);
		if (IsValidKey(key) == false)
			return;

		dirtyKeys.Add(key);
		TryScheduleDirtyKeys();
	}

	public void ClearDirty(uint buildingId, ItemTransferScheduleMode mode)
	{
		dirtyKeys.Remove(new ItemTransferScheduleKey(buildingId, mode));
	}

	public void NotifyIdleWorker(AIWorker worker)
	{
		if (worker == null || worker.CurrentTask != null || reservedWorkers.Contains(worker))
			return;

		for (int i = 0; i < worker.AssignedTaskTypes.Count; ++i)
		{
			WorkerTask.TaskType taskType = worker.AssignedTaskTypes[i];
			if (worker.CanAcceptGeneralTask(taskType) == false)
				continue;

			AddIdleWorker(worker, taskType);
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
		if (scheduledWorkersByTask.TryGetValue(task, out AIWorker worker))
		{
			scheduledWorkersByTask.Remove(task);
			reservedWorkers.Remove(worker);
		}

		if (dirtyKeys.Count > 0)
			TryScheduleDirtyKeys();
	}

	public bool HasDirty(uint buildingId, ItemTransferScheduleMode mode)
	{
		return dirtyKeys.Contains(new ItemTransferScheduleKey(buildingId, mode));
	}

	public bool HasHandler(uint buildingId, ItemTransferScheduleMode mode)
	{
		return entriesByKey.ContainsKey(new ItemTransferScheduleKey(buildingId, mode));
	}

	public int GetIdleWorkerCount(WorkerTask.TaskType taskType)
	{
		return idleWorkersByTaskType.TryGetValue(taskType, out WorkerQueue queue) ? queue.Set.Count : 0;
	}

	private bool TrySchedule(AIWorker worker)
	{
		if (worker == null || worker.CurrentTask != null || reservedWorkers.Contains(worker))
			return false;

		foreach (ItemTransferScheduleKey key in CopyDirtyKeys())
		{
			if (entriesByKey.TryGetValue(key, out ScheduleEntry entry) == false ||
				CanWorkerTryKey(worker, key, entry) == false)
				continue;

			if (TryBuildAndEnqueue(key, entry, worker))
				return true;
		}

		return false;
	}

	private bool TryScheduleDirtyKeys()
	{
		foreach (ItemTransferScheduleKey key in CopyDirtyKeys())
		{
			if (TrySchedule(key))
				return true;
		}

		return false;
	}

	private bool TrySchedule(ItemTransferScheduleKey key)
	{
		if (dirtyKeys.Contains(key) == false ||
			entriesByKey.TryGetValue(key, out ScheduleEntry entry) == false)
		{
			return false;
		}

		RefreshIdleWorkers(entry.TaskType);
		if (idleWorkersByTaskType.TryGetValue(entry.TaskType, out WorkerQueue queue) == false)
			return false;

		while (dirtyKeys.Contains(key) && queue.Queue.Count > 0)
		{
			AIWorker worker = queue.Queue.First.Value;
			queue.Queue.RemoveFirst();
			queue.Set.Remove(worker);

			if (CanWorkerTryKey(worker, key, entry) == false)
				continue;

			if (TryBuildAndEnqueue(key, entry, worker))
				return true;
		}

		return false;
	}

	private void RefreshIdleWorkers(WorkerTask.TaskType taskType)
	{
		WorkerManager workerManager = GameContext.HasInstance ? GameContext.Instance.WorkerMgr : null;
		if (workerManager == null)
			return;

		foreach (AIWorker worker in workerManager.Workers)
		{
			if (worker == null ||
				worker.CurrentTask != null ||
				reservedWorkers.Contains(worker) ||
				worker.CanAcceptGeneralTask(taskType) == false)
			{
				continue;
			}

			AddIdleWorker(worker, taskType);
		}
	}

	private void AddIdleWorker(AIWorker worker, WorkerTask.TaskType taskType)
	{
		WorkerQueue queue = GetOrCreateWorkerQueue(taskType);
		if (queue.Set.Add(worker))
			queue.Queue.AddLast(worker);
	}

	private bool TryBuildAndEnqueue(ItemTransferScheduleKey key, ScheduleEntry entry, AIWorker worker)
	{
		if (TaskManager == null ||
			worker == null ||
			entry?.Handler == null)
		{
			return false;
		}

		ItemTransferScheduleRequest request = new(key, entry.TaskType, worker);
		ItemTransferScheduleResult result = entry.Handler(request, out WorkerTask task);
		switch (result)
		{
			case ItemTransferScheduleResult.Scheduled:
				if (task == null)
					return false;

				RemoveIdleWorker(worker);
				scheduledKeysByTask[task] = key;
				scheduledWorkersByTask[task] = worker;
				reservedWorkers.Add(worker);
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

	private bool CanWorkerTryKey(AIWorker worker, ItemTransferScheduleKey key, ScheduleEntry entry)
	{
		return IsValidKey(key) &&
			entry != null &&
			worker != null &&
			worker.CurrentTask == null &&
			reservedWorkers.Contains(worker) == false &&
			worker.CanAcceptGeneralTask(entry.TaskType);
	}

	private static bool IsValidKey(ItemTransferScheduleKey key)
	{
		return key.BuildingId != 0 &&
			key.Mode != ItemTransferScheduleMode.Undefined;
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
		List<ItemTransferScheduleKey> keys = new(dirtyKeys);
		keys.Sort((left, right) => GetSchedulePriority(left.Mode).CompareTo(GetSchedulePriority(right.Mode)));
		return keys;
	}

	private static int GetSchedulePriority(ItemTransferScheduleMode mode)
	{
		return mode switch
		{
			ItemTransferScheduleMode.PackingOutput => 0,
			ItemTransferScheduleMode.Picking => 5,
			ItemTransferScheduleMode.Storing => 6,
			ItemTransferScheduleMode.PackingInput => 10,
			_ => 100,
		};
	}
}
