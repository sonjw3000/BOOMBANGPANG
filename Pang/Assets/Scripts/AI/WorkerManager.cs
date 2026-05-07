using Assets.Scripts.AI.BT;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static WorkerTask;
//using static WorkerTask.TaskType;

[DefaultExecutionOrder(-100)]
public class WorkerManager : MonoBehaviour
{
	// todo
	// 자료형을 바꿔야 한다
	// 삽입 삭제가 빈번히 일어나기 때문에
	// 빈번한가?
	// 흠 그런가?
	// 일단 기다려봐
	[SerializeField] private List<AIWorker> workers = new();
	private Dictionary<TaskType, List<AIWorker>> workersPerTaskType = new();

	// 중간지점 삭제를 할 경우도 있다
	private readonly Dictionary<TaskType, LinkedList<AIWorker>> idleWorkersQueue = new();
	private uint nextWorkerID = 0;
	private int monthlyCost = 0;

	// todo
	// storing, picking 등 작업의 경우 작업자들을 zone별 queue로도 나눠야 한다
	// 왜 queue로 나누냐? 쉴놈들 다 쉬었으면 일 해야지

	public IReadOnlyList<AIWorker> Workers => workers;
	public int CostPerMonth => monthlyCost;
	// todo
	// 전역 블랙보드의 관리는 다른곳에 넘겨야함
	private BlackBoard globalBlackboard;

	private void Awake()
	{
		foreach (TaskType type in System.Enum.GetValues(typeof(TaskType)))
		{
			workersPerTaskType[type] = new();
			idleWorkersQueue[type] = new();
		}
	}

	public void RegisterWorker(AIWorker worker)
	{
		workers.Add(worker);
		workersPerTaskType[TaskType.Undefined].Add(worker);
		worker.SetWorkerID(nextWorkerID++);

		if (worker.CurrentTask == null)
			idleWorkersQueue[worker.TaskType].AddLast(worker);

		monthlyCost += worker.MonthlyCost;
	}

	public void UnregisterWorker(AIWorker worker)
	{
		workers.Remove(worker);
		workersPerTaskType[worker.TaskType].Remove(worker);

		if (worker.CurrentTask == null)
			idleWorkersQueue[worker.TaskType].Remove(worker);

		monthlyCost -= worker.MonthlyCost;
	}

	public void ChangeWorkerTaskType(AIWorker worker, TaskType type)
	{
		// have to check ability
		switch (type)
		{
			case TaskType.Unloading:
				if (worker.GetComponent<CargoHandlingAbility>() == false)
				{
					Debug.Log("No Unloading Ability");
					return;
				}
				break;

			case TaskType.Storing:
				if (worker.GetComponent<CarryBoxAbility>() == false)
				{
					Debug.Log("No CarryboxAbility Ability");
					return;
				}
				break;

			case TaskType.Picking:
				if (worker.GetComponent<CarryBoxAbility>() == false)
				{
					Debug.Log("No CarryboxAbility Ability");
					return;
				}
				break;

			case TaskType.Packing:

				break;

			case TaskType.Loading:
				break;
		}

		workersPerTaskType[worker.TaskType].Remove(worker);
		workersPerTaskType[type].Add(worker);

		worker.ChangeWorkerType(type);


		// 임시코드
		// 나중에 모두 위와 같은 방식으로 task 부여해야함
		if (type == TaskType.Packing)
		{
			PackingTask task = new PackingTask();
			worker.SetTask(task);
			return;
		}

		if (worker.CurrentTask == null)
		{
			idleWorkersQueue[type].AddLast(worker);
		}


		// todo
		// picking / storing에 경우에는 별도의 자료구조가 또 있을수도 있다
		// 추가되면 여기에도 추가하자
	}

	public AIWorker GetAvailableWorkers(WorkerTask taskData)
	{
		// is available worker there?
		// todo
		// 태스크의 조건과 알맞은 작업자를 돌려줌

		AIWorker worker = null;

		if (idleWorkersQueue[taskData.Type].Count > 0)
		{
			// todo if picking storing이라면 일단 해당 zone에 있는 작업자를 찾아야한다

			worker = idleWorkersQueue[taskData.Type].First();
			idleWorkersQueue[taskData.Type].RemoveFirst();
		}

		return worker;
	}

	public void AddIdleWorker(AIWorker worker)
	{
		idleWorkersQueue[worker.TaskType].AddLast(worker);
	}

	private void Update()
	{
		// todo
		// 타이밍별로 정리해두고 관리해야 함
		// 목적지 이동중엔 비활성화
		// 
		foreach (var worker in workers)
		{
			if (worker.enabled)
				worker.RunBT(globalBlackboard);
		}
	}
}
