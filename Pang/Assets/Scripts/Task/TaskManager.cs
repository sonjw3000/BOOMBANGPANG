using System.Collections.Generic;
using UnityEngine;
using BlackBoardSystem;

using static WorkerTask;

//[DefaultExecutionOrder(-100)]
public class TaskManager : MonoBehaviour
{
	private Dictionary<TaskType, LinkedList<WorkerTask>> taskQueue = new();
	public IReadOnlyDictionary<TaskType, LinkedList<WorkerTask>> TaskQueue  => taskQueue;

	public List<WorkerTask> EndTaskList { get; private set; } = new();

	public TaskManager()
	{
		// initialize task queues
		foreach (TaskType type in System.Enum.GetValues(typeof(TaskType)))
		{
			taskQueue[type] = new();
		}

	}

	// dispatch task to workers
	private void Dispatch()
	{
		// find tasks to do
		foreach (var (key, queue) in taskQueue)
		{
			while (queue.Count > 0)
			{
				WorkerTask data = queue.First.Value;
				AIWorker worker = GameContext.Instance.WorkerMgr.GetAvailableWorkers(data);

				// if no available workers break;
				if (worker == null)
					break;

				queue.RemoveFirst();
				worker.SetTask(data);
			}
		}
	}

	public void Update()
	{
		Dispatch();
	}
}
