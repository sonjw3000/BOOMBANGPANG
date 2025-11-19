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

	private void BuildPickingTaskJob()
	{
		// PickingTask를 만들어야함
		// 피킹태스크는 주문이 들어왔을 때 생성됨
		// 주문정보를 받아서 피킹태스크를 생성해야하는데

		// 이를 위해서 나중에 오더 매니지먼트같은게 필요하지 않을까?
	}

	public void MakeTestPickingWork()
	{
		// picking의 구조를 어케 해야할까?
		// 이게 맞음?
		// 일단 박아 난 몰라 나중에 알아서 고치겠지 일단 박는게 맞다고 본다 ㅇㅇ
		PickingTask.PickJob testJob = new PickingTask.PickJob();
		testJob.JobID = nextJobID++;
		testJob.Lines = new();

		// 아이템id 123333의 아이템을 줏으러 가라

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