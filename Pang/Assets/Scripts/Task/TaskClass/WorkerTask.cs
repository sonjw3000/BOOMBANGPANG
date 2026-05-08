using UnityEngine;

public static class WorkerTaskTypeRequirement
{
	public static WorkerAbility GetRequiredAbilities(WorkerTask.TaskType taskType)
	{
		return taskType switch
		{
			WorkerTask.TaskType.Unloading => WorkerAbility.CargoHandling,
			WorkerTask.TaskType.Storing => WorkerAbility.PickingStoring | WorkerAbility.CarryBox,
			WorkerTask.TaskType.Picking => WorkerAbility.PickingStoring | WorkerAbility.CarryBox,
			WorkerTask.TaskType.Packing => WorkerAbility.Packing,
			WorkerTask.TaskType.Loading => WorkerAbility.CargoHandling,
			WorkerTask.TaskType.Water => WorkerAbility.PickingStoring | WorkerAbility.CarryBox,
			WorkerTask.TaskType.Undefined => WorkerAbility.None,
			WorkerTask.TaskType.HandleMistake => WorkerAbility.None,

			_ => WorkerAbility.None,
		};
	}
}

public abstract class WorkerTask
{
	public enum TaskType
	{
		// IB
		Unloading,
		//Receive,
		//Label,
		Storing,

		// OB
		Picking,
		//Sorting,
		Packing,

		Loading,

		Water,
		// undef
		Undefined,

		HandleMistake
	}

	public enum Status
	{
		Ready,
		Blocked,
		Assigned,
		End
	}

	private IBaseNode baseNode = null;

	protected CarryBoxAbility carryBox = null;

	public AIWorker OccupyWorker { get; private set; }
	public TaskType Type { get; private set; }
	public Status CurrentStatus { get; private set; } = Status.Blocked;
	public float TaskBuiltTime { get; private set; }
	public bool IsEmergency { get; private set; }
	public CarryBoxAbility CarryingAbility => carryBox;

	private TaskManager Manager => GameContext.Instance.TaskMgr;
	//static public TaskManager Manager { get; private set; } = null;

	protected WorkerTask(TaskType type)
	{
		//OccupyWorker = worker;
		Type = type;
		TaskBuiltTime = Time.time;
		SelectorNode root = new SelectorNode();

		// todo
		// work가 실패했을경우도 판단해야함
		root.Add(CheckFulfiledNode());
		root.Add(BuildWorkNode());

		baseNode = root;
	}

	public void SetAIWorker(AIWorker worker)
	{
		OccupyWorker = worker;
		// 작업자가 배치된 상태라면 작업이 진행되고있는 상태임
		CurrentStatus = Status.Assigned;
		OnTaskAssigned();
	}

	public void EndTask()
	{
		CurrentStatus = Status.End;
		OccupyWorker.OnTaskCompleted();
		OccupyWorker.SetTask(null);

		Manager.OnEndTask(this);
	}

	protected virtual void OnTaskAssigned() { }

	protected abstract IBaseNode BuildWorkNode();

	public abstract bool CheckTaskEnd();
#if UNITY_EDITOR
	public abstract string ShowStatus();
#endif
	public IBaseNode.NodeState UpdateTaskNode(in BTContext ctx)
	{
		return baseNode.Evaluate(ctx);
	}


	private IBaseNode CheckFulfiledNode()
	{
		SequenceNode checkingFulfilled = new SequenceNode();
		checkingFulfilled.Add(new ActionNode(AIWorker.CheckFulfilled));
		checkingFulfilled.Add(new ActionNode(AIWorker.TaskCompleted));

		return checkingFulfilled;
	}

	//public static void SetTaskManager(TaskManager taskManager) { Manager = taskManager; }
}
