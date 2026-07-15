using System;
using System.Collections.Generic;
using UnityEngine;

public class ProcessStats
{
	public int CurrentQueue;
	public int ProcessedThisMonth;
	public int ProcessedThisWeek;
	public int ProcessedLastWeek;
	public float RecentThroughput;
}

public class ProcessStatsCollector : MonoBehaviour
{
	private readonly Dictionary<WorkerTask.TaskType, ProcessStats> stats = new();

	private void Awake()
	{
		foreach (WorkerTask.TaskType workerTask in Enum.GetValues(typeof(WorkerTask.TaskType)))
		{
			stats[workerTask] = new ProcessStats();
		}
	}

	private void Start()
	{
		if (GameContext.Instance.GameTime != null)
		{
			GameContext.Instance.GameTime.OnWeekPassed += HandleWeekPassed;
		}
	}

	private void OnDestroy()
	{
		if (GameContext.Instance != null && GameContext.Instance.GameTime != null)
		{
			GameContext.Instance.GameTime.OnWeekPassed -= HandleWeekPassed;
		}
	}

	private void HandleWeekPassed()
	{
		foreach (var s in stats.Values)
		{
			s.ProcessedLastWeek = s.ProcessedThisWeek;
			s.ProcessedThisWeek = 0;
		}
	}

	public void AddQueue(WorkerTask.TaskType workerTask, int amount = 1)
	{
		stats[workerTask].CurrentQueue += amount;
	}

	public void CompleteProcess(WorkerTask.TaskType workerTask)
	{
		stats[workerTask].CurrentQueue = Math.Max(0, stats[workerTask].CurrentQueue - 1);
		stats[workerTask].ProcessedThisMonth++;
		stats[workerTask].ProcessedThisWeek++;
	}

	public void RemoveQueue(WorkerTask.TaskType workerTask)
	{
		stats[workerTask].CurrentQueue = Math.Max(0, stats[workerTask].CurrentQueue - 1);
	}

	public ProcessStats GetStats(WorkerTask.TaskType workerTask)
	{
		return stats[workerTask];
	}
}
