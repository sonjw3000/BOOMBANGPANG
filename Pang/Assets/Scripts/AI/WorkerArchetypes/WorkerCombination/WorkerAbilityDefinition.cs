using UnityEngine;

public enum WorkerType
{
	FullTime,
	PartTime,
	Illegal,
	Robot,
}

[System.Serializable]
public struct WorkerAbilityDefinition
{
	public WorkerType workerType;
	public WorkerAbility abilities;
	public int monthlyCost;
	public int installCost;
}

