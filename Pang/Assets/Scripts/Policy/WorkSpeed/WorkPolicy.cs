using AYellowpaper.SerializedCollections;
using UnityEngine;

[System.Serializable]
public class WorkProfile
{
	[SerializeField] private float duration;
	[SerializeField] private float actualFatiguePerTask;

	public float WorkDuration => duration;
	public float FatiguePerTask => actualFatiguePerTask;
}

[CreateAssetMenu(menuName = "Worker/WorkPolicy")]
public class WorkPolicy : ScriptableObject
{
	[SerializedDictionary("WorkerPolicyType", "Speed")]
	public SerializedDictionary<WorkerPolicyType, float> moveSpeed;
	[SerializedDictionary("WorkerPolicyType", "WorkActionType/WorkerProfile")]
	public SerializedDictionary<WorkerPolicyType, SerializedDictionary<WorkActionType, WorkProfile>> workerWorkTime;
}
