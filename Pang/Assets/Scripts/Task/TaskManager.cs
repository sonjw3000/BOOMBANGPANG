using System.Collections.Generic;
using UnityEngine;
using BlackBoardSystem;

using static WorkerTask;

//[DefaultExecutionOrder(-100)]
public class TaskManager// : MonoBehaviour
{
	public Dictionary<TaskType, CustomQueue<WorkerTask, int>> TaskQueue { get; private set; } = new();

	public List<WorkerTask> EndTaskList { get; private set; } = new();

	public TaskManager()
	{
		// initialize task queues
		foreach (TaskType type in System.Enum.GetValues(typeof(TaskType)))
		{
			TaskQueue[type] = new CustomQueue<WorkerTask, int>();
		}

	}

	// dispatch task to workers
	public void Dispatch()
	{
		// find tasks to do
		foreach (var (key, queue) in TaskQueue)
		{
			while (queue.Count > 0)
			{
				var data = queue.Peek();

				AIWorker worker = WorkerManager.Instance.GetAvailableWorkers(data);

				// if no available workers break;
				if (worker == null)
					break;

				worker.SetTask(queue.Dequeue());
			}
		}
	}


}
