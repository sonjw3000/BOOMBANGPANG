using BlackBoardSystem;
using System;
using System.Collections.Generic;
using UnityEngine;
using static WorkerTask;

//[DefaultExecutionOrder(-100)]
public class TaskManager : MonoBehaviour
{
	private Dictionary<TaskType, LinkedList<WorkerTask>> taskQueue = new();
	public IReadOnlyDictionary<TaskType, LinkedList<WorkerTask>> TaskQueue  => taskQueue;

	private InboundWorkflowManager IBMgr => GameContext.Instance.IBWorkflowMgr;
	private OutboundWorkflowManager OBMgr => GameContext.Instance.OBWorkflowMgr;

	private void Awake()
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

	public void OnEndTask(WorkerTask task)
	{
		// todo
		//
		switch (task.Type)
		{
			// IB
			case TaskType.Unloading:
			//case TaskType.Receive:
			//case TaskType.Label:
			case TaskType.Storing:
				IBMgr.OnTaskCompleted(task);
				break;

			// OB
			case TaskType.Picking:
			//case TaskType.Sorting:
			//case TaskType.Packaging:
			case TaskType.Loading:
				OBMgr.OnTaskCompleted(task);
				break;

			default:
				Debug.LogError("ERROR!! TaskType Undef on tskmgr end task");
				break;
		}
	}

	public void Update()
	{
		Dispatch();
	}
}
