using UnityEngine;
using AYellowpaper.SerializedCollections;


// 작업자들의 속도 등을 관리
// 속도를 높일수록 fatigue / battery usage 증가량이 늘어남

public class WorkPolicyService : MonoBehaviour
{
	[SerializeField] private WorkPolicy workPolicy;

	[SerializedDictionary("WorkerType", "Boost")]
	[SerializeField] private SerializedDictionary<WorkerType, float> moveBoost;
	[SerializedDictionary("WorkerType", "Task/Boost")]
	[SerializeField] private SerializedDictionary<WorkerType, SerializedDictionary<WorkerTask.TaskType, float>> workTimeBoost;

	
	public float GetMoveSpeed(WorkerType workerType)
	{
		//WorkProfile move = workPolicy.moveSpeed[workerType];
		return 0;

	}

}
