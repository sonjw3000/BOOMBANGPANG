using Assets.Scripts.AI.BT;
using System.Collections.Generic;
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
	private readonly Dictionary<TaskType, HashSet<AIWorker>> idleWorkersSet = new();
	private uint nextWorkerID = 0;
	private int monthlyCost = 0;

	// todo
	// storing, picking 등 작업의 경우 작업자들을 zone별 queue로도 나눠야 한다
	// 왜 queue로 나누냐? 쉴놈들 다 쉬었으면 일 해야지

	public IReadOnlyList<AIWorker> Workers => workers;
	public int CostPerMonth => monthlyCost;
	public uint NextWorkerId => nextWorkerID;
	// todo
	// 전역 블랙보드의 관리는 다른곳에 넘겨야함
	private BlackBoard globalBlackboard;

	private void Awake()
	{
		foreach (TaskType type in System.Enum.GetValues(typeof(TaskType)))
		{
			workersPerTaskType[type] = new();
			idleWorkersQueue[type] = new();
			idleWorkersSet[type] = new();
		}
	}

	public void RegisterWorker(AIWorker worker, bool preserveWorkerId = false)
	{
		workers.Add(worker);
		workersPerTaskType[worker.TaskType].Add(worker);

		if (preserveWorkerId)
		{
			if (nextWorkerID <= worker.WorkerID)
				nextWorkerID = worker.WorkerID + 1;
		}
		else
		{
			worker.SetWorkerID(nextWorkerID++);
		}

		monthlyCost += worker.MonthlyCost;
	}

	public void UnregisterWorker(AIWorker worker)
	{
		workers.Remove(worker);
		workersPerTaskType[worker.TaskType].Remove(worker);
		RemoveIdleWorker(worker);

		monthlyCost -= worker.MonthlyCost;
	}

	static public bool CanChangeType(AIWorker worker, TaskType type) => 
		worker.HasAbility(WorkerTaskTypeRequirement.GetRequiredAbilities(type));

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
		RemoveIdleWorker(worker);


		// todo
		// picking / storing에 경우에는 별도의 자료구조가 또 있을수도 있다
		// 추가되면 여기에도 추가하자
	}

	public AIWorker GetAvailableWorkers(WorkerTask taskData)
	{
		if (taskData.TryGetPreferredWorker(out var preferredWorker))
		{
			if (preferredWorker != null && preferredWorker.CanAcceptPreferredTask(taskData))
			{
				RemoveIdleWorker(preferredWorker);
				return preferredWorker;
			}

			return null;
		}

		var queue = idleWorkersQueue[taskData.Type];
		while (queue.Count > 0)
		{
			var worker = queue.First.Value;
			queue.RemoveFirst();
			idleWorkersSet[taskData.Type].Remove(worker);

			if (worker == null || worker.CanAcceptGeneralTask(taskData) == false)
				continue;

			return worker;
		}

		return null;
	}

	public void AddIdleWorker(AIWorker worker)
	{
		if (worker == null)
			return;

		if (idleWorkersSet[worker.TaskType].Add(worker) == false)
			return;

		idleWorkersQueue[worker.TaskType].AddLast(worker);
	}

	public void RemoveIdleWorker(AIWorker worker)
	{
		if (worker == null)
			return;

		if (idleWorkersSet[worker.TaskType].Remove(worker) == false)
			return;

		idleWorkersQueue[worker.TaskType].Remove(worker);
	}

	private void SyncWorkerAvailability(AIWorker worker)
	{
		worker.UpdatePackingRecoveryState();

		if (worker.CanAcceptGeneralTask(worker.TaskType))
			AddIdleWorker(worker);
		else
			RemoveIdleWorker(worker);
	}

	private void Update()
	{
		// todo
		// 타이밍별로 정리해두고 관리해야 함
		// 목적지 이동중엔 비활성화
		// 
		foreach (var worker in workers)
		{
			SyncWorkerAvailability(worker);

			if (worker.enabled)
				worker.RunBT(globalBlackboard);
		}
	}

	public void ResetRuntimeState()
	{
		workers.Clear();
		monthlyCost = 0;
		nextWorkerID = 0;

		foreach (TaskType type in System.Enum.GetValues(typeof(TaskType)))
		{
			workersPerTaskType[type].Clear();
			idleWorkersQueue[type].Clear();
			idleWorkersSet[type].Clear();
		}
	}

	public void SetNextWorkerId(uint nextWorkerId)
	{
		nextWorkerID = nextWorkerId > nextWorkerID ? nextWorkerId : nextWorkerID;
	}
}
