using Unity.Mathematics;
using UnityEditor.VersionControl;
using UnityEngine;

using static WorkerTask;

public class WorkFlowManager : MonoBehaviour 
{
	private TaskManager taskManager = new();
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

	public void MakeTestPickingWork()
	{
		// picking의 구조를 어케 해야할까?
		// 이게 맞음?
		// 일단 박아 난 몰라 나중에 알아서 고치겠지 일단 박는게 맞다고 본다 ㅇㅇ
		PickingTask.PickJob testJob = new PickingTask.PickJob();
		testJob.JobID = nextJobID++;
		testJob.Lines = new();

		testJob.Lines.Add(new PickingTask.PickingLine 
		{ 
			GoalPosition = new int3(1, 0, 3),
			ItemID = 0,
			Quantity = 2
		});

		testJob.Lines.Add(new PickingTask.PickingLine
		{
			GoalPosition = new int3(3, 0, 6),
			ItemID = 1,
			Quantity = 2
		});

		PickingTask testPick = new PickingTask(testJob);

		// 일단 넣어봐
		if (taskManager.TaskQueue.ContainsKey(TaskType.Picking) == false)
			taskManager.TaskQueue[TaskType.Picking] = new();

		taskManager.TaskQueue[TaskType.Picking].Enqueue(testPick, 1);
			
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