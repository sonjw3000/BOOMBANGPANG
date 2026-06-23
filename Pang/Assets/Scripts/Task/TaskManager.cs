using System;
using System.Collections.Generic;
using UnityEngine;
using static WorkerTask;

//[DefaultExecutionOrder(-100)]
public partial class TaskManager : MonoBehaviour
{
	private Dictionary<TaskType, LinkedList<WorkerTask>> taskQueue = new();
	private Dictionary<TaskType, LinkedList<WorkerTask>> taskOnProgress = new();

	private ProcessStatsCollector Stats => GameContext.Instance.ProcessStats;

	private InboundWorkflowService IBService => GameContext.Instance.IBWorkflowSvc;
	private OutboundWorkflowService OBService => GameContext.Instance.OBWorkflowSvc;

	public IReadOnlyDictionary<TaskType, LinkedList<WorkerTask>> TaskQueue => taskQueue;
	public IReadOnlyDictionary<TaskType, LinkedList<WorkerTask>> TaskOnProgress => taskOnProgress;

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
		taskQueue[task.Type].AddLast(task);
		Stats.AddQueue(task.Type);
	}

	// dispatch task to workers
	private void Dispatch()
	{
		// find tasks to do
		foreach (var (key, queue) in taskQueue)
		{
			var node = queue.First;
			while (node != null)
			{
				var next = node.Next;
				WorkerTask data = node.Value;
				AIWorker worker = GameContext.Instance.WorkerMgr.GetAvailableWorkers(data);

				if (worker != null)
				{
					queue.Remove(node);
					worker.SetTask(data);
					taskOnProgress[key].AddLast(data);
				}

				node = next;
			}
		}
	}

	public void OnEndTask(WorkerTask task)
	{
		taskOnProgress[task.Type].Remove(task);
		Stats.CompleteProcess(task.Type);

		// todo
		//
		switch (task.Type)
		{
			// IB
			case TaskType.Unloading:
			case TaskType.IB:
			case TaskType.Labeling:
			//case TaskType.Receive:
			//case TaskType.Label:
			case TaskType.Storing:
				IBService.OnTaskCompleted(task);
				break;

			// OB
			case TaskType.OB:
			case TaskType.CargoTransfer:
			case TaskType.Picking:
			case TaskType.Packing:
			//case TaskType.Sorting:
			//case TaskType.Packaging:
			case TaskType.Loading:
				OBService.OnTaskCompleted(task);
				break;

			case TaskType.Water:
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

	public void AddRestoredInProgressTask(WorkerTask task)
	{
		if (task == null)
			return;

		taskOnProgress[task.Type].AddLast(task);
	}

}
