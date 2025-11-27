using Unity.Mathematics;
using UnityEditor.VersionControl;
using UnityEngine;

using static WorkerTask;

public class WorkFlowManager : MonoBehaviour 
{
	private TaskManager taskManager = new();
	private OrderManager orderManager = new();
	//private InventoryService;
	//private StationService
	private int nextJobID = 0;

	public void OnTaskCompleted(WorkerTask task)
	{
		switch (task.Type)
		{
			// IB
			case TaskType.Unloading:
				break;
			case TaskType.Receive:
				break;
			case TaskType.Label:
				break;
			case TaskType.Putaway:
				break;

			// OB
			case TaskType.Picking:
				break;
			case TaskType.Sorting:
				break;
			case TaskType.Packaging:
				break;
			case TaskType.Loading:
				break;
		}
	}

	private void BuildPickingTaskJob()
	{
		// todo
		// OrderLineQueue가 빌 때 까지 반복해야한다
		var task = orderManager.BuildPickingTasks();
		if (task == null)
		{
			Debug.Log("No Picking Task Created");
			return;
		}

		taskManager.TaskQueue[TaskType.Picking].Enqueue(task, 1);
	}

	public void MakeOrder()
	{
		orderManager.CreateRandomOrder();
	}

	public void MakeTestPickingWork()
	{
		BuildPickingTaskJob();
	}

	void Start()
	{
		WorkerTask.SetTaskManager(taskManager);
	}

	void Update()
	{
		// worker manager에서 작업이 끝난 워커를 찾아야할듯?

		taskManager.Dispatch();
	}

}