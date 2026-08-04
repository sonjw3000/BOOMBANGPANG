using System;
using Assets.Scripts.AI.BT;
using System.Collections.Generic;
using UnityEngine;
using static WorkerTask;
using static WorkerTask.TaskType;

[DefaultExecutionOrder(-100)]
public partial class WorkerManager : MonoBehaviour
{
	// todo
	// 자료형을 바꿔야 한다
	// 삽입 삭제가 빈번히 일어나기 때문에
	// 빈번한가?
	// 흠 그런가?
	// 일단 기다려봐
	[SerializeField] private List<AIWorker> workers = new();
	private Dictionary<TaskType, List<AIWorker>> workersPerTaskType = new();
	private readonly Dictionary<TaskType, Dictionary<WorkerStatusAction, int>> workerStatusCountsPerTaskType = new();
	private readonly Dictionary<TaskType, int> trafficBlockedCountsPerTaskType = new();
	private readonly Dictionary<WorkerStatusAction, int> workerStatusCounts = new();
	private int trafficBlockedCount = 0;

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
	public int TrafficBlockedCount => trafficBlockedCount;
	public event Action OnWorkersChanged;
	public event Action<AIWorker> OnWorkerChanged;
	public event Action<AIWorker, WorkerOperationalState, WorkerOperationalState> OnWorkerOperationalStateChanged;
	// todo
	// 전역 블랙보드의 관리는 다른곳에 넘겨야함
	private BlackBoard globalBlackboard;

	private void Awake()
	{
		InitializeWorkerStatusCounts();

		foreach (TaskType type in System.Enum.GetValues(typeof(TaskType)))
		{
			workersPerTaskType[type] = new();
			idleWorkersQueue[type] = new();
			idleWorkersSet[type] = new();
		}
	}

	public void RegisterWorker(AIWorker worker, bool preserveWorkerId = false)
	{
		if (worker == null || workers.Contains(worker))
			return;

		workers.Add(worker);
		worker.EnsureAssignedTaskTypesInitialized();
		RegisterWorkerTaskTypes(worker);

		if (preserveWorkerId)
		{
			if (nextWorkerID <= worker.WorkerID)
				nextWorkerID = worker.WorkerID + 1;
		}
		else
		{
			worker.SetWorkerID(nextWorkerID++);
		}

		if (worker is HumanWorker human && GameContext.HasInstance)
		{
			if (preserveWorkerId)
				GameContext.Instance.HumanIncident?.InitializeWorker(human);
			else
				GameContext.Instance.HumanIncident?.InitializeNewWorker(human);
		}

		monthlyCost += worker.MonthlyCost;
		SubscribeWorker(worker);
		RegisterWorkerStatus(worker);
		OnWorkersChanged?.Invoke();
	}

	public void UnregisterWorker(AIWorker worker)
	{
		if (worker == null || workers.Remove(worker) == false)
			return;

		UnregisterWorkerStatus(worker);
		UnsubscribeWorker(worker);
		UnregisterWorkerTaskTypes(worker);
		RemoveIdleWorker(worker);

		monthlyCost -= worker.MonthlyCost;
		worker.MarkUnregistered();
		OnWorkersChanged?.Invoke();
	}

	public void ReportRobotWear(in SimulationTickContext context, WearService wearService)
	{
		if (wearService == null)
			return;

		for (int i = 0; i < workers.Count; ++i)
		{
			if (workers[i] is not RobotWorker robot || robot.IsOperational == false)
				continue;

			WorkerStatusAction action = robot.EffectiveStatusAction;
			if (action != WorkerStatusAction.MovingTo &&
				action != WorkerStatusAction.UsingAirlock &&
				action != WorkerStatusAction.Working &&
				action != WorkerStatusAction.HandlingMistake)
			{
				continue;
			}

			wearService.ReportOperation(robot, context.ElapsedWeeks);
		}
	}

	public bool TryRemoveWorker(AIWorker worker)
	{
		if (worker == null || workers.Contains(worker) == false || worker.IsOperational == false)
			return false;

		GridService gridService = GameContext.HasInstance ? GameContext.Instance.GridService : null;
		if (gridService == null || gridService.IsPlacedObject(worker.gameObject) == false)
			return false;

		if (worker.PrepareForRemoval() == false)
			return false;

		return gridService.OnRemove(worker.gameObject);
	}

	static public bool CanChangeType(AIWorker worker, TaskType type) => WorkerTaskAssignmentPolicy.CanAssign(worker, type);

	public void ChangeWorkerTaskType(AIWorker worker, TaskType type)
	{
		if (worker == null)
			return;

		if (CanChangeType(worker, type) == false)
		{
			Debug.Log($"Worker {worker.name} cannot change to task type {type}.");
			return;
		}

		// have to check ability
		if (HasRequiredComponent(worker, type) == false)
			return;

		UnregisterWorkerTaskTypes(worker);
		RemoveIdleWorker(worker);
		worker.ChangeWorkerType(type);
		RegisterWorkerTaskTypes(worker);
		SyncWorkerAvailability(worker);


		// todo
		// picking / storing에 경우에는 별도의 자료구조가 또 있을수도 있다
		// 추가되면 여기에도 추가하자
	}

	public void SetWorkerAssignedTaskTypes(AIWorker worker, IReadOnlyList<TaskType> taskTypes)
	{
		if (worker == null || worker.CurrentTask != null)
			return;

		TrySetWorkerAssignment(worker, worker.PrimaryBuildingId, taskTypes);
	}

	public bool TrySetWorkerPrimaryBuilding(AIWorker worker, uint buildingId)
	{
		return TrySetWorkerAssignment(worker, buildingId, Array.Empty<TaskType>());
	}

	public bool TryRequestWorkerAssignment(
		AIWorker worker,
		uint buildingId,
		IReadOnlyList<TaskType> taskTypes)
	{
		if (worker == null)
			return false;

		return worker.CurrentTask != null
			? TryScheduleWorkerAssignment(worker, buildingId, taskTypes)
			: TrySetWorkerAssignment(worker, buildingId, taskTypes);
	}

	public bool TrySetWorkerAssignment(
		AIWorker worker,
		uint buildingId,
		IReadOnlyList<TaskType> taskTypes)
	{
		if (worker == null || workers.Contains(worker) == false || worker.CurrentTask != null)
			return false;

		if (TryValidateAssignment(worker, buildingId, taskTypes, out List<TaskType> validTypes) == false)
			return false;

		ApplyWorkerAssignment(worker, buildingId, validTypes, syncAvailability: true);
		worker.ClearPendingAssignment();
		OnWorkerChanged?.Invoke(worker);
		return true;
	}

	public bool TryScheduleWorkerAssignment(
		AIWorker worker,
		uint buildingId,
		IReadOnlyList<TaskType> taskTypes)
	{
		if (worker == null ||
			workers.Contains(worker) == false ||
			(worker.CurrentTask == null && worker.HasPendingAssignment == false))
			return false;

		if (TryValidateAssignment(worker, buildingId, taskTypes, out List<TaskType> validTypes) == false)
			return false;

		worker.SetPendingAssignment(buildingId, validTypes);
		if (worker.CurrentTask == null)
			return TryApplyPendingAssignment(worker);

		OnWorkerChanged?.Invoke(worker);
		return true;
	}

	public bool CancelPendingWorkerAssignment(AIWorker worker)
	{
		if (worker == null || workers.Contains(worker) == false || worker.HasPendingAssignment == false)
			return false;

		worker.ClearPendingAssignment();
		OnWorkerChanged?.Invoke(worker);
		return true;
	}

	public bool TryApplyPendingAssignment(AIWorker worker)
	{
		if (worker == null ||
			workers.Contains(worker) == false ||
			worker.CurrentTask != null ||
			worker.HasPendingAssignment == false)
		{
			return false;
		}

		if (TryValidateAssignment(
				worker,
				worker.PendingPrimaryBuildingId,
				worker.PendingAssignedTaskTypes,
				out List<TaskType> validTypes) == false)
		{
			return false;
		}

		uint buildingId = worker.PendingPrimaryBuildingId;
		ApplyWorkerAssignment(worker, buildingId, validTypes, syncAvailability: false);
		worker.ClearPendingAssignment();
		OnWorkerChanged?.Invoke(worker);
		return true;
	}

	private bool TryValidateAssignment(
		AIWorker worker,
		uint buildingId,
		IReadOnlyList<TaskType> taskTypes,
		out List<TaskType> validTypes)
	{
		validTypes = new List<TaskType>();
		if (worker == null || TryResolveBuildingType(buildingId, out BuildingType? buildingType) == false)
			return false;

		if (taskTypes == null)
			return true;

		for (int i = 0; i < taskTypes.Count; ++i)
		{
			TaskType taskType = taskTypes[i];
			if (taskType == TaskType.Undefined || validTypes.Contains(taskType))
				continue;

			if (WorkerTaskAssignmentPolicy.CanAssign(worker, buildingType, taskType) == false ||
				HasRequiredComponent(worker, taskType) == false)
			{
				return false;
			}

			validTypes.Add(taskType);
		}

		return true;
	}

	private static bool TryResolveBuildingType(uint buildingId, out BuildingType? buildingType)
	{
		buildingType = null;
		if (buildingId == 0)
			return true;

		BuildingManager buildingManager = GameContext.HasInstance ? GameContext.Instance.BuildingMgr : null;
		if (buildingManager == null ||
			buildingManager.TryGetBuilding(buildingId, out Building building) == false ||
			building == null)
		{
			return false;
		}

		buildingType = building.Type;
		return true;
	}

	private void ApplyWorkerAssignment(
		AIWorker worker,
		uint buildingId,
		IReadOnlyList<TaskType> taskTypes,
		bool syncAvailability)
	{
		if (GameContext.HasInstance)
			GameContext.Instance.ItemTransferTaskScheduler?.CancelReadyReservationForAssignmentChange(worker);

		ReleasePackingStationAssignmentIfNeeded(worker, buildingId, taskTypes);
		UnregisterWorkerTaskTypes(worker);
		RemoveIdleWorker(worker);
		worker.SetPrimaryBuildingId(buildingId);
		worker.SetAssignedTaskTypes(taskTypes);
		RegisterWorkerTaskTypes(worker);
		if (syncAvailability)
			SyncWorkerAvailability(worker);
	}

	private static void ReleasePackingStationAssignmentIfNeeded(
		AIWorker worker,
		uint buildingId,
		IReadOnlyList<TaskType> taskTypes)
	{
		if (worker?.CurrentWorkingBuilding is not PackingStation station)
			return;

		bool keepsPackingTask = taskTypes != null;
		if (keepsPackingTask)
		{
			keepsPackingTask = false;
			for (int i = 0; i < taskTypes.Count; ++i)
			{
				if (taskTypes[i] != TaskType.Packing)
					continue;

				keepsPackingTask = true;
				break;
			}
		}

		bool keepsStation = keepsPackingTask &&
			buildingId != 0 &&
			GameContext.HasInstance &&
			GameContext.Instance.FacilityMgr != null &&
			GameContext.Instance.FacilityMgr.TryGetBuildingId(station, out uint stationBuildingId) &&
			stationBuildingId == buildingId;
		if (keepsStation == false)
			station.CurrentPackingWorker = null;
	}

	public AIWorker GetAvailableWorkers(WorkerTask taskData)
	{
		if (taskData.TryGetPreferredWorker(out var preferredWorker))
		{
			if (preferredWorker != null &&
				preferredWorker.CanAcceptPreferredTask(taskData) &&
				taskData.CanDispatchTo(preferredWorker))
			{
				RemoveIdleWorker(preferredWorker);
				return preferredWorker;
			}

			return null;
		}

		var queue = idleWorkersQueue[taskData.Type];
		int candidateCount = queue.Count;
		for (int i = 0; i < candidateCount; ++i)
		{
			var worker = queue.First.Value;
			queue.RemoveFirst();
			idleWorkersSet[taskData.Type].Remove(worker);

			if (worker == null || worker.CanAcceptGeneralTask(taskData) == false)
				continue;

			if (taskData.CanDispatchTo(worker) == false)
			{
				idleWorkersSet[taskData.Type].Add(worker);
				queue.AddLast(worker);
				continue;
			}

			RemoveIdleWorker(worker);
			return worker;
		}

		return null;
	}

	public void AddIdleWorker(AIWorker worker)
	{
		if (worker == null || worker.IsOperational == false || worker.IsPlayerOverride || worker.CurrentTask != null)
			return;

		foreach (TaskType taskType in worker.AssignedTaskTypes)
		{
			if (worker.CanAcceptGeneralTask(taskType) == false)
				continue;

			if (idleWorkersSet[taskType].Add(worker) == false)
				continue;

			idleWorkersQueue[taskType].AddLast(worker);
		}

		if (GameContext.HasInstance)
			GameContext.Instance.ItemTransferTaskScheduler.NotifyIdleWorker(worker);
	}

	public void RemoveIdleWorker(AIWorker worker)
	{
		if (worker == null)
			return;

		foreach (TaskType taskType in worker.AssignedTaskTypes)
		{
			if (idleWorkersSet[taskType].Remove(worker) == false)
				continue;

			idleWorkersQueue[taskType].Remove(worker);
		}

		if (GameContext.HasInstance)
			GameContext.Instance.ItemTransferTaskScheduler.NotifyWorkerUnavailable(worker);
	}

	private void SyncWorkerAvailability(AIWorker worker)
	{
		worker.UpdatePackingRecoveryState();

		if (worker.AssignedTaskTypes.Count > 0 && worker.CanAcceptGeneralTask(worker.AssignedTaskTypes[0]))
			AddIdleWorker(worker);
		else
			RemoveIdleWorker(worker);
	}

	private void RegisterWorkerTaskTypes(AIWorker worker)
	{
		if (worker == null)
			return;

		foreach (TaskType taskType in worker.AssignedTaskTypes)
		{
			if (workersPerTaskType[taskType].Contains(worker) == false)
				workersPerTaskType[taskType].Add(worker);
		}
	}

	private void UnregisterWorkerTaskTypes(AIWorker worker)
	{
		if (worker == null)
			return;

		foreach (TaskType taskType in worker.AssignedTaskTypes)
			workersPerTaskType[taskType].Remove(worker);
	}

	private static bool HasRequiredComponent(AIWorker worker, TaskType type)
	{
		switch (type)
		{
			case TaskType.Unloading:
			case TaskType.CargoTransfer:
				if (worker.GetComponent<CargoHandlingAbility>() == false)
				{
					Debug.Log("No CargoHandlingAbility");
					return false;
				}
				return true;

			case TaskType.IB:
			case TaskType.CapsuleClear:
			case TaskType.CapsuleSupply:
			case TaskType.OB:
			case TaskType.Storing:
			case TaskType.Picking:
			case TaskType.LaunchSort:
			case TaskType.WasteCollection:
				if (worker.GetComponent<CarryBoxAbility>() == false)
				{
					Debug.Log("No CarryBoxAbility");
					return false;
				}
				return true;

			case TaskType.Labeling:
				if (worker.GetComponent<LabelingAbility>() == false)
				{
					Debug.Log("No LabelingAbility");
					return false;
				}
				return true;

			default:
				return true;
		}
	}

	private void Update()
	{
		// todo
		// 타이밍별로 정리해두고 관리해야 함
		// 목적지 이동중엔 비활성화
		// 
		foreach (var worker in workers)
		{
			// SyncWorkerAvailability(worker);
			if (worker == null)
				continue;

			if (worker.IsOperational)
				worker.TickVitals(Time.deltaTime);

			if (worker.enabled)
				worker.RunBT(globalBlackboard);
		}
	}

	public void SetNextWorkerId(uint nextWorkerId)
	{
		nextWorkerID = nextWorkerId > nextWorkerID ? nextWorkerId : nextWorkerID;
	}

	public int GetTaskWorkerStatusCount(TaskType taskType, WorkerStatusAction statusAction)
	{
		if (statusAction == WorkerStatusAction.TrafficBlock)
			return trafficBlockedCountsPerTaskType.TryGetValue(taskType, out int trafficCount) ? trafficCount : 0;

		if (workerStatusCountsPerTaskType.TryGetValue(taskType, out var statusCounts) == false)
			return 0;

		return statusCounts.TryGetValue(statusAction, out int count) ? count : 0;
	}

	public int GetWorkerStatusCount(WorkerStatusAction statusAction)
	{
		if (statusAction == WorkerStatusAction.TrafficBlock)
			return trafficBlockedCount;

		return workerStatusCounts.TryGetValue(statusAction, out int count) ? count : 0;
	}

	public void RebuildWorkerStatusCaches()
	{
		ResetWorkerStatusCounts();

		for (int i = 0; i < workers.Count; ++i)
		{
			RegisterWorkerStatus(workers[i]);
		}
	}

	private void SubscribeWorker(AIWorker worker)
	{
		worker.OnStatusChanged += OnWorkerStatusChanged;
		worker.OnTaskTypeChanged += OnWorkerTaskTypeChanged;
		worker.OnTrafficBlockChanged += OnWorkerTrafficBlockChanged;
		worker.OnOperationalStateChanged += HandleWorkerOperationalStateChanged;
	}

	private void UnsubscribeWorker(AIWorker worker)
	{
		worker.OnStatusChanged -= OnWorkerStatusChanged;
		worker.OnTaskTypeChanged -= OnWorkerTaskTypeChanged;
		worker.OnTrafficBlockChanged -= OnWorkerTrafficBlockChanged;
		worker.OnOperationalStateChanged -= HandleWorkerOperationalStateChanged;
	}

	private void OnWorkerStatusChanged(AIWorker worker, WorkerStatusAction oldStatus, WorkerStatusAction newStatus)
	{
		if (worker == null || oldStatus == newStatus)
			return;

		MoveStatusCount(worker.TaskType, oldStatus, newStatus);
		OnWorkerChanged?.Invoke(worker);
	}

	private void OnWorkerTaskTypeChanged(AIWorker worker, TaskType oldTaskType, TaskType newTaskType)
	{
		if (worker == null || oldTaskType == newTaskType)
			return;

		WorkerStatusAction currentStatus = worker.EffectiveStatusAction;
		AdjustStatusCount(oldTaskType, currentStatus, -1);
		AdjustStatusCount(newTaskType, currentStatus, 1);

		if (worker.IsTrafficBlocked)
		{
			AdjustTrafficBlockedCount(oldTaskType, -1);
			AdjustTrafficBlockedCount(newTaskType, 1);
		}
		OnWorkerChanged?.Invoke(worker);
	}

	private void OnWorkerTrafficBlockChanged(AIWorker worker, bool isBlocked)
	{
		if (worker == null)
			return;

		AdjustTrafficBlockedCount(worker.TaskType, isBlocked ? 1 : -1);
		OnWorkerChanged?.Invoke(worker);
	}

	private void HandleWorkerOperationalStateChanged(
		AIWorker worker,
		WorkerOperationalState previousState,
		WorkerOperationalState nextState)
	{
		if (worker == null || previousState == nextState)
			return;

		OnWorkerChanged?.Invoke(worker);
		OnWorkerOperationalStateChanged?.Invoke(worker, previousState, nextState);
	}

	private void RegisterWorkerStatus(AIWorker worker)
	{
		if (worker == null)
			return;

		AdjustStatusCount(worker.TaskType, worker.EffectiveStatusAction, 1);

		if (worker.IsTrafficBlocked)
			AdjustTrafficBlockedCount(worker.TaskType, 1);
	}

	private void UnregisterWorkerStatus(AIWorker worker)
	{
		if (worker == null)
			return;

		AdjustStatusCount(worker.TaskType, worker.EffectiveStatusAction, -1);

		if (worker.IsTrafficBlocked)
			AdjustTrafficBlockedCount(worker.TaskType, -1);
	}

	private void MoveStatusCount(TaskType taskType, WorkerStatusAction oldStatus, WorkerStatusAction newStatus)
	{
		AdjustStatusCount(taskType, oldStatus, -1);
		AdjustStatusCount(taskType, newStatus, 1);
	}

	private void AdjustStatusCount(TaskType taskType, WorkerStatusAction statusAction, int delta)
	{
		if (statusAction == WorkerStatusAction.TrafficBlock)
			return;

		workerStatusCountsPerTaskType[taskType][statusAction] = Mathf.Max(0, workerStatusCountsPerTaskType[taskType][statusAction] + delta);
		workerStatusCounts[statusAction] = Mathf.Max(0, workerStatusCounts[statusAction] + delta);
	}

	private void AdjustTrafficBlockedCount(TaskType taskType, int delta)
	{
		trafficBlockedCountsPerTaskType[taskType] = Mathf.Max(0, trafficBlockedCountsPerTaskType[taskType] + delta);
		trafficBlockedCount = Mathf.Max(0, trafficBlockedCount + delta);
	}

	private void InitializeWorkerStatusCounts()
	{
		foreach (WorkerStatusAction statusAction in Enum.GetValues(typeof(WorkerStatusAction)))
		{
			workerStatusCounts[statusAction] = 0;
		}

		foreach (TaskType taskType in Enum.GetValues(typeof(TaskType)))
		{
			Dictionary<WorkerStatusAction, int> statusCounts = new();
			foreach (WorkerStatusAction statusAction in Enum.GetValues(typeof(WorkerStatusAction)))
			{
				statusCounts[statusAction] = 0;
			}

			workerStatusCountsPerTaskType[taskType] = statusCounts;
			trafficBlockedCountsPerTaskType[taskType] = 0;
		}
	}

	private void ResetWorkerStatusCounts()
	{
		foreach (WorkerStatusAction statusAction in Enum.GetValues(typeof(WorkerStatusAction)))
		{
			workerStatusCounts[statusAction] = 0;
		}

		foreach (TaskType taskType in Enum.GetValues(typeof(TaskType)))
		{
			foreach (WorkerStatusAction statusAction in Enum.GetValues(typeof(WorkerStatusAction)))
			{
				workerStatusCountsPerTaskType[taskType][statusAction] = 0;
			}

			trafficBlockedCountsPerTaskType[taskType] = 0;
		}
	}
}
