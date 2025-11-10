using System.Collections.Generic;
using UnityEngine;
using BlackBoardSystem;

using static WorkerTask;

[DefaultExecutionOrder(-100)]
public class TaskManager : MonoBehaviour
{
	private Dictionary<TaskType, PriorityQueue<WorkerTask>> taskQueue;

	// dispatch task to workers
	public void Dispatch()
	{

	}

}
