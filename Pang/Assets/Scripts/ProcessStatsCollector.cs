using System;
using System.Collections.Generic;
using UnityEditor.VersionControl;
using UnityEngine;

public class ProcessStats
{
	public int CurrentQueue;
	public int ProcessedThisMonth;
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

	public void AddQueue(WorkerTask.TaskType workerTask, int amount = 1)
	{
		stats[workerTask].CurrentQueue += amount;
	}

	public void CompleteProcess(WorkerTask.TaskType workerTask)
	{
		stats[workerTask].CurrentQueue = Math.Max(0, stats[workerTask].CurrentQueue - 1);
		stats[workerTask].ProcessedThisMonth++;
	}

	public ProcessStats GetStats(WorkerTask.TaskType workerTask)
	{
		return stats[workerTask];
	}
}
