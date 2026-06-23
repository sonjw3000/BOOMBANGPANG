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
	[SerializedDictionary("WorkerPolicyType", "Boost")]
	[SerializeField] private SerializedDictionary<WorkerPolicyType, float> moveBoost;
	[SerializedDictionary("WorkerPolicyType", "WorkActionType/Boost")]
	[SerializeField] private SerializedDictionary<WorkerPolicyType, SerializedDictionary<WorkActionType, float>> workTimeBoost;
	[SerializedDictionary("WorkerPolicyType", "Move Speed Multiplier")]
	[SerializeField] private SerializedDictionary<WorkerPolicyType, float> moveSpeedMultipliers = new();
	[SerializedDictionary("WorkerPolicyType", "Work Speed Multiplier")]
	[SerializeField] private SerializedDictionary<WorkerPolicyType, float> workSpeedMultipliers = new();

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
		=> workPolicy.moveSpeed[targetWorker.WorkerPolicyType]
		* GetMoveBoost(targetWorker.WorkerPolicyType)
		* GetMoveSpeedMultiplier(targetWorker.WorkerPolicyType)
		* targetWorker.GetMoveSpeedMultiplier();

	public float GetWorkTime(AIWorker targetWorker, WorkActionType actionType)
		=> workPolicy.workerWorkTime[targetWorker.WorkerPolicyType][actionType].WorkDuration
		/ GetWorkBoost(targetWorker.WorkerPolicyType, actionType)
		/ GetWorkSpeedMultiplier(targetWorker.WorkerPolicyType)
		/ Mathf.Max(targetWorker.GetWorkSpeedMultiplier(), 0.01f);

	public float GetWorkFatigue(AIWorker targetWorker, WorkActionType actionType)
		=> workPolicy.workerWorkTime[targetWorker.WorkerPolicyType][actionType].FatiguePerTask
		* GetWorkBoost(targetWorker.WorkerPolicyType, actionType)
		* GetWorkSpeedMultiplier(targetWorker.WorkerPolicyType);

	public float GetBoost(AIWorker targetWorker, WorkActionType actionType)
		=> GetWorkBoost(targetWorker.WorkerPolicyType, actionType) * GetWorkSpeedMultiplier(targetWorker.WorkerPolicyType);

	public float GetMoveSpeedMultiplier(WorkerPolicyType workerPolicyType)
	{
		EnsureRuntimeMultipliers();
		return moveSpeedMultipliers.TryGetValue(workerPolicyType, out float value) ? value : DefaultSpeedMultiplier;
	}

	public float GetWorkSpeedMultiplier(WorkerPolicyType workerPolicyType)
	{
		EnsureRuntimeMultipliers();
		return workSpeedMultipliers.TryGetValue(workerPolicyType, out float value) ? value : DefaultSpeedMultiplier;
	}

	public void SetMoveSpeedMultiplier(WorkerPolicyType workerPolicyType, float value)
	{
		EnsureRuntimeMultipliers();
		moveSpeedMultipliers[workerPolicyType] = Mathf.Clamp(value, MinimumSpeedMultiplier, MaximumSpeedMultiplier);
	}

	public void SetWorkSpeedMultiplier(WorkerPolicyType workerPolicyType, float value)
	{
		EnsureRuntimeMultipliers();
		workSpeedMultipliers[workerPolicyType] = Mathf.Clamp(value, MinimumSpeedMultiplier, MaximumSpeedMultiplier);
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
		moveSpeedMultipliers ??= new SerializedDictionary<WorkerPolicyType, float>();
		workSpeedMultipliers ??= new SerializedDictionary<WorkerPolicyType, float>();

		foreach (WorkerPolicyType workerPolicyType in System.Enum.GetValues(typeof(WorkerPolicyType)))
		{
			if (moveSpeedMultipliers.ContainsKey(workerPolicyType) == false)
				moveSpeedMultipliers[workerPolicyType] = DefaultSpeedMultiplier;
			if (workSpeedMultipliers.ContainsKey(workerPolicyType) == false)
				workSpeedMultipliers[workerPolicyType] = DefaultSpeedMultiplier;
		}
	}

	private float GetMoveBoost(WorkerPolicyType workerPolicyType)
	{
		return moveBoost != null && moveBoost.TryGetValue(workerPolicyType, out float value)
			? value
			: 1.0f;
	}

	private float GetWorkBoost(WorkerPolicyType workerPolicyType, WorkActionType actionType)
	{
		if (workTimeBoost != null &&
			workTimeBoost.TryGetValue(workerPolicyType, out SerializedDictionary<WorkActionType, float> actionBoosts) &&
			actionBoosts != null &&
			actionBoosts.TryGetValue(actionType, out float value))
		{
			return value;
		}

		return 1.0f;
	}

}
