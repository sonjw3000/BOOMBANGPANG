using UnityEngine;

using static WorkerTask;

// outbound 작업 흐름 관리
// 주문을 까서 picking -> packaging -> loading 작업을 관리

public class OutboundWorkflowManager : MonoBehaviour 
{
	[SerializeField] private float timeSinceLastOrder = 0.0f;
	[SerializeField] private float orderInterval = 10.0f;

	private OrderManager OrderMgr => GameContext.Instance.OrderMgr;
	private TaskManager TaskMgr => GameContext.Instance.TaskMgr;
	//private TaskManager taskManager = new();
	//private OrderManager orderManager = new();
	//private InventoryService;
	//private StationService
	private int nextJobID = 0;


	// 주문을 묶는 역할
	private PickingTaskAllocator pickingTaskAllocator = new TestingPickingTaskAllocator();

	public void OnTaskCompleted(WorkerTask task)
	{
		switch (task.Type)
		{
			case TaskType.Picking:
				break;
			//case TaskType.Sorting:
			//	break;
			//case TaskType.Packaging:
			//	break;
			case TaskType.Loading:
				break;
		}
	}

	private void BuildPickingTaskJob()
	{
		// todo
		// OrderLineQueue가 빌 때 까지 반복해야한다
		var task = pickingTaskAllocator.BuildPickingTask();
		if (task == null)
		{
			Debug.Log("No Picking Task Created");
			return;
		}

		TaskMgr.TaskQueue[TaskType.Picking].AddLast(task);
	}

	public void MakeOrder()
	{
		OrderMgr.CreateRandomOrder();
	}

	public void MakeTestPickingWork()
	{
		BuildPickingTaskJob();
	}

	private void Start()
	{
	}

	void Update()
	{
		timeSinceLastOrder += Time.deltaTime;

		if (timeSinceLastOrder >= orderInterval)
		{
			timeSinceLastOrder = 0.0f;
			MakeOrder();
			BuildPickingTaskJob();
		}

	}
}
