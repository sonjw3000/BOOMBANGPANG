using NUnit.Framework;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

public abstract class WorkerTask
{
	public enum TaskType
	{
		// IB
		Unloading,
		Receive,
		Label,
		Putaway,

		// OB
		Picking,
		Sorting,
		Packaging,
		Loading,

		// undef
		//Undefined
	}

	public enum Status
	{
		Ready,
		Blocked,
		Assigned
	}

	public AIWorker OccupyWorker { get; private set; }
	public TaskType Type { get; private set; }
	public Status CurrentStatus { get; private set; } = Status.Blocked;
	public float TaskBuiltTime { get; private set; }
	public bool IsEmergency { get; private set; }

	protected WorkerTask(TaskType type)
	{
		//OccupyWorker = worker;
		Type = type;
		TaskBuiltTime = Time.time;
		BuildTaskNode();
	}

	public void SetAIWorker(AIWorker worker)
	{
		OccupyWorker = worker;
		// 작업자가 배치된 상태라면 작업이 진행되고있는 상태임
		CurrentStatus = Status.Assigned;
	}

	protected abstract void BuildTaskNode();
	public abstract void UpdateTaskNode(in BTContext ctx);
}
