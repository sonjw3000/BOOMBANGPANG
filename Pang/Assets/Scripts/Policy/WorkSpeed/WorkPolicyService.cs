using AYellowpaper.SerializedCollections;
using UnityEngine;
using System.Collections.Generic;
using static WorkerTask;

public class WorkPolicyService : MonoBehaviour
{
	// base policy
	[SerializeField] private WorkPolicy workPolicy;

	// worker boost
	[SerializedDictionary("WorkerType", "Boost")]
	[SerializeField] private SerializedDictionary<WorkerType, float> moveBoost;
	[SerializedDictionary("WorkerType", "WorkActionType/Boost")]
	[SerializeField] private SerializedDictionary<WorkerType, SerializedDictionary<WorkActionType, float>> workTimeBoost;

	// worker rest/charge
	[SerializeField] private float workerRestFatigue = 70.0f;
	[SerializeField] private float robotChargeBattery = 30.0f;

	public float GetMoveSpeed(AIWorker targetWorker) 
		=> workPolicy.moveSpeed[targetWorker.WorkerType]
		* moveBoost[targetWorker.WorkerType]
		* targetWorker.GetMoveSpeedMultiplier();

	public float GetWorkTime(AIWorker targetWorker, WorkActionType actionType)
		=> workPolicy.workerWorkTime[targetWorker.WorkerType][actionType].WorkDuration
		/ workTimeBoost[targetWorker.WorkerType][actionType]
		/ Mathf.Max(targetWorker.GetWorkSpeedMultiplier(), 0.01f);

	public float GetWorkFatigue(AIWorker targetWorker, WorkActionType actionType)
		=> workPolicy.workerWorkTime[targetWorker.WorkerType][actionType].FatiguePerTask
		* workTimeBoost[targetWorker.WorkerType][actionType];

	public float GetBoost(AIWorker targetWorker, WorkActionType actionType)
		=> workTimeBoost[targetWorker.WorkerType][actionType];

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
}
