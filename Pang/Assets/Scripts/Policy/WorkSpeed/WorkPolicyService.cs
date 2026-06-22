using AYellowpaper.SerializedCollections;
using UnityEngine;
using System.Collections.Generic;
using static WorkerTask;

public partial class WorkPolicyService : MonoBehaviour
{
	private const float DefaultSpeedMultiplier = 1.0f;
	private const float MinimumSpeedMultiplier = 0.5f;
	private const float MaximumSpeedMultiplier = 2.0f;

	// base policy
	[SerializeField] private WorkPolicy workPolicy;

	// worker boost
	[SerializedDictionary("WorkerType", "Boost")]
	[SerializeField] private SerializedDictionary<WorkerType, float> moveBoost;
	[SerializedDictionary("WorkerType", "WorkActionType/Boost")]
	[SerializeField] private SerializedDictionary<WorkerType, SerializedDictionary<WorkActionType, float>> workTimeBoost;
	[SerializedDictionary("WorkerType", "Move Speed Multiplier")]
	[SerializeField] private SerializedDictionary<WorkerType, float> moveSpeedMultipliers = new();
	[SerializedDictionary("WorkerType", "Work Speed Multiplier")]
	[SerializeField] private SerializedDictionary<WorkerType, float> workSpeedMultipliers = new();

	// worker rest/charge
	[SerializeField] private float workerRestFatigue = 70.0f;
	[SerializeField] private float robotChargeBattery = 30.0f;
	[SerializeField] private float workerRestTargetFatigue = 20.0f;
	[SerializeField] private float robotChargeTargetBattery = 80.0f;
	[SerializeField] private float workerRestRecoveryPerSecond = 15.0f;
	[SerializeField] private float robotChargeRecoveryPerSecond = 20.0f;

	private void Awake()
	{
		EnsureRuntimeMultipliers();
	}

	private void OnValidate()
	{
		EnsureRuntimeMultipliers();
	}

	public float GetMoveSpeed(AIWorker targetWorker) 
		=> workPolicy.moveSpeed[targetWorker.WorkerType]
		* GetMoveBoost(targetWorker.WorkerType)
		* GetMoveSpeedMultiplier(targetWorker.WorkerType)
		* targetWorker.GetMoveSpeedMultiplier();

	public float GetWorkTime(AIWorker targetWorker, WorkActionType actionType)
		=> workPolicy.workerWorkTime[targetWorker.WorkerType][actionType].WorkDuration
		/ GetWorkBoost(targetWorker.WorkerType, actionType)
		/ GetWorkSpeedMultiplier(targetWorker.WorkerType)
		/ Mathf.Max(targetWorker.GetWorkSpeedMultiplier(), 0.01f);

	public float GetWorkFatigue(AIWorker targetWorker, WorkActionType actionType)
		=> workPolicy.workerWorkTime[targetWorker.WorkerType][actionType].FatiguePerTask
		* GetWorkBoost(targetWorker.WorkerType, actionType)
		* GetWorkSpeedMultiplier(targetWorker.WorkerType);

	public float GetBoost(AIWorker targetWorker, WorkActionType actionType)
		=> GetWorkBoost(targetWorker.WorkerType, actionType) * GetWorkSpeedMultiplier(targetWorker.WorkerType);

	public float GetMoveSpeedMultiplier(WorkerType workerType)
	{
		EnsureRuntimeMultipliers();
		return moveSpeedMultipliers.TryGetValue(workerType, out float value) ? value : DefaultSpeedMultiplier;
	}

	public float GetWorkSpeedMultiplier(WorkerType workerType)
	{
		EnsureRuntimeMultipliers();
		return workSpeedMultipliers.TryGetValue(workerType, out float value) ? value : DefaultSpeedMultiplier;
	}

	public void SetMoveSpeedMultiplier(WorkerType workerType, float value)
	{
		EnsureRuntimeMultipliers();
		moveSpeedMultipliers[workerType] = Mathf.Clamp(value, MinimumSpeedMultiplier, MaximumSpeedMultiplier);
	}

	public void SetWorkSpeedMultiplier(WorkerType workerType, float value)
	{
		EnsureRuntimeMultipliers();
		workSpeedMultipliers[workerType] = Mathf.Clamp(value, MinimumSpeedMultiplier, MaximumSpeedMultiplier);
	}

	public float WorkerRestFatigueThreshold => workerRestFatigue;
	public float RobotChargeBatteryThreshold => robotChargeBattery;
	public float WorkerRestTargetFatigue => workerRestTargetFatigue;
	public float RobotChargeTargetBattery => robotChargeTargetBattery;
	public float WorkerRestRecoveryPerSecond => workerRestRecoveryPerSecond;
	public float RobotChargeRecoveryPerSecond => robotChargeRecoveryPerSecond;

	public bool IsTargetHigherPriority(AIWorker targetWorker, AIWorker other)
	{
		// 1. 긴급도 (Emergency/HandleMistake)
		bool targetEmergency = IsEmergency(targetWorker);
		bool otherEmergency = IsEmergency(other);
		if (targetEmergency != otherEmergency) return targetEmergency;

		// 2. Express Contract 우선순위
		bool targetExpress = IsExpress(targetWorker);
		bool otherExpress = IsExpress(other);
		if (targetExpress != otherExpress) return targetExpress;

		// 3. 남은 거리 (적은 쪽 우선 - 빨리 비켜주기)
		int targetDist = GetRemainingDistance(targetWorker);
		int otherDist = GetRemainingDistance(other);

		if (targetDist != otherDist) return targetDist < otherDist;

		// 4. ID 기반 결정론적 선택
		return targetWorker.gameObject.GetInstanceID() < other.gameObject.GetInstanceID();
	}

	private bool IsEmergency(AIWorker worker)
	{
		if (worker.CurrentTask != null && worker.CurrentTask.IsEmergency) return true;
		if (worker.WorkerState.Action == WorkerStatusAction.HandlingMistake) return true;
		if (worker.TaskType == TaskType.HandleMistake) return true;
		return false;
	}

	private bool IsExpress(AIWorker worker)
	{
		var task = worker.CurrentTask;
		if (task == null) return false;

		if (task is PickingTask picking)
		{
			return HasExpressLine(picking.PickingData.Lines);
		}
		if (task is PackingTask packing)
		{
			if (worker.CurrentWorkingBuilding is PackingStation station)
			{
				if (station.CurrentPackingBox?.Job != null)
					return HasExpressLine(station.CurrentPackingBox.Job.Lines);
			}
		}
		return false;
	}

	private bool HasExpressLine(List<WorkLine> lines)
	{
		if (lines == null) return false;
		foreach (var line in lines)
		{
			if (line.RelatedOrderLine?.SourceContract?.Type == Assets.Scripts.Contract.ContractType.Express)
				return true;
		}
		return false;
	}

	private int GetRemainingDistance(AIWorker worker)
	{
		var findRoute = worker.GetComponent<FindRoute>();
		return findRoute != null ? findRoute.RemainingDistance : int.MaxValue;
	}

	private void EnsureRuntimeMultipliers()
	{
		moveSpeedMultipliers ??= new SerializedDictionary<WorkerType, float>();
		workSpeedMultipliers ??= new SerializedDictionary<WorkerType, float>();

		foreach (WorkerType workerType in System.Enum.GetValues(typeof(WorkerType)))
		{
			if (moveSpeedMultipliers.ContainsKey(workerType) == false)
				moveSpeedMultipliers[workerType] = DefaultSpeedMultiplier;
			if (workSpeedMultipliers.ContainsKey(workerType) == false)
				workSpeedMultipliers[workerType] = DefaultSpeedMultiplier;
		}
	}

	private float GetMoveBoost(WorkerType workerType)
	{
		return moveBoost != null && moveBoost.TryGetValue(workerType, out float value)
			? value
			: 1.0f;
	}

	private float GetWorkBoost(WorkerType workerType, WorkActionType actionType)
	{
		if (workTimeBoost != null &&
			workTimeBoost.TryGetValue(workerType, out SerializedDictionary<WorkActionType, float> actionBoosts) &&
			actionBoosts != null &&
			actionBoosts.TryGetValue(actionType, out float value))
		{
			return value;
		}

		return 1.0f;
	}

}
