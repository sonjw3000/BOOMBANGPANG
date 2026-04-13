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
	[SerializedDictionary("WorkerType", "Speed")]
	public SerializedDictionary<WorkerType, float> moveSpeed;
	[SerializedDictionary("WorkerType", "Task/WorkerProfile")]
	public SerializedDictionary<WorkerType, SerializedDictionary<WorkerTask.TaskType, WorkProfile>> workerWorkTime;
}
