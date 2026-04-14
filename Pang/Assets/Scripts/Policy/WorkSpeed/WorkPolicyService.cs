using UnityEngine;
using AYellowpaper.SerializedCollections;


// 작업자들의 속도 등을 관리
// 속도를 높일수록 fatigue / battery usage 증가량이 늘어남

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
}
