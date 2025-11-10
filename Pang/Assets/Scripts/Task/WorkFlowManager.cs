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

	public void TruckArrival(TruckManifest manifest)
	{
		// assignment dock


		// unload task assign
		//taskManager.Enqueue();
	}

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
			GoalPosition = new int3(1, 1, 3),
			ContainerID = 1,
			ItemID = 1,
			RequestedMount = 2
		});

		testJob.Lines.Add(new PickingTask.PickingLine
		{
			GoalPosition = new int3(10, 1, 3),
			ContainerID = 1,
			ItemID = 1,
			RequestedMount = 2
		});

		PickingTask testPick = new PickingTask(testJob);

		// 일단 넣어봐
		taskManager.TaskQueue[TaskType.Picking].Enqueue(testPick, 0);
			
	}

	private void Update()
	{
		taskManager.Dispatch();
	}

}