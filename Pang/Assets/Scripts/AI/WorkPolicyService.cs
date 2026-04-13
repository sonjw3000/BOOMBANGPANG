using System;
using System.Collections.Generic;
using UnityEngine;


// 작업자들의 속도 등을 관리
// 속도를 높일수록 fatigue / battery usage 증가량이 늘어남

[Serializable]
public class WorkProfile
{
	[SerializeField] private float baseValue;
	[SerializeField] private float actualFatiguePerTask;
	[SerializeField] private float boost = 1.0f;

	public float MoveSpeed => baseValue * boost;
	public float WorkDuration => baseValue / Mathf.Max(0.01f, boost);
	public float FatiguePerTask => actualFatiguePerTask * boost;

	public float Boost => boost;

	public void SetBoost(float value) => boost = Mathf.Max(value, 0.01f);
}

public class WorkPolicyService : MonoBehaviour
{
	private readonly Dictionary<WorkerType, WorkProfile> moveSpeed = new();
	private readonly Dictionary<WorkerType, Dictionary<WorkerTask.TaskType, WorkProfile>> workerWorkTime = new();


}
