using UnityEngine;

public enum WorkerType
{
	FullTime,
	PartTime,
	Illegal,
	Robot,
}

[CreateAssetMenu(menuName = "Worker/Ability")]
public class WorkerAbilityDefinition : ScriptableObject
{
	public WorkerType workerType;
	public WorkerAbility abilities;
	public int monthlyCost;
}

