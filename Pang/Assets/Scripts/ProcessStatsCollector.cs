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
	private GameTime boundGameTime;

	private void Awake()
	{
		EnsureStatsInitialized();
	}

	private void OnEnable()
	{
		EnsureStatsInitialized();
		TryBindGameTime();
	}

	private void Start()
	{
		TryBindGameTime();
	}

	private void OnDisable()
	{
		UnbindGameTime();
	}

	private void EnsureStatsInitialized()
	{
		foreach (WorkerTask.TaskType workerTask in Enum.GetValues(typeof(WorkerTask.TaskType)))
		{
			if (stats.ContainsKey(workerTask) == false)
				stats[workerTask] = new ProcessStats();
		}
	}

	private void TryBindGameTime()
	{
		if (boundGameTime != null || GameContext.HasInstance == false)
			return;

		GameTime gameTime = GameContext.Instance.GameTime;
		if (gameTime == null)
			return;

		boundGameTime = gameTime;
		boundGameTime.OnWeekPassed += HandleWeekPassed;
	}

	private void UnbindGameTime()
	{
		if (boundGameTime != null)
			boundGameTime.OnWeekPassed -= HandleWeekPassed;

		boundGameTime = null;
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
