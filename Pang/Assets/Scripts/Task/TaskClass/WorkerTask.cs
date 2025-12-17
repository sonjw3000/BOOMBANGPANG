using UnityEngine;

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
		//Packaging,
		Loading,

		// undef
		Undefined
	}

	public enum Status
	{
		Ready,
		Blocked,
		Assigned,
		End
	}

	protected IBaseNode baseNode = null;

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
		BuildTaskNode();
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
		OccupyWorker.SetTask(null);

		Manager.OnEndTask(this);
	}

	protected virtual void OnTaskAssigned() { }

	protected abstract void BuildTaskNode();
#if UNITY_EDITOR
	public abstract string ShowStatus();
#endif
	public abstract IBaseNode.NodeState UpdateTaskNode(in BTContext ctx);

	//public static void SetTaskManager(TaskManager taskManager) { Manager = taskManager; }
}
