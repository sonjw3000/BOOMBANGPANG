using UnityEngine;

public static class WorkerTaskTypeRequirement
{
	public static WorkerAbility GetRequiredAbilities(WorkerTask.TaskType taskType)
	{
		return taskType switch
		{
			WorkerTask.TaskType.Unloading => WorkerAbility.CargoHandling,
			WorkerTask.TaskType.IB => WorkerAbility.PickingStoring | WorkerAbility.CarryBox,
			WorkerTask.TaskType.CapsuleClear => WorkerAbility.PickingStoring | WorkerAbility.CarryBox,
			WorkerTask.TaskType.CapsuleSupply => WorkerAbility.PickingStoring | WorkerAbility.CarryBox,
			WorkerTask.TaskType.OB => WorkerAbility.PickingStoring | WorkerAbility.CarryBox,
			WorkerTask.TaskType.CargoTransfer => WorkerAbility.CargoHandling,
			WorkerTask.TaskType.Storing => WorkerAbility.PickingStoring | WorkerAbility.CarryBox,
			WorkerTask.TaskType.Picking => WorkerAbility.PickingStoring | WorkerAbility.CarryBox,
			WorkerTask.TaskType.Packing => WorkerAbility.Packing,
			WorkerTask.TaskType.Loading => WorkerAbility.CargoHandling,
			WorkerTask.TaskType.PackingInput => WorkerAbility.PickingStoring | WorkerAbility.CarryBox,
			WorkerTask.TaskType.PackingOutput => WorkerAbility.PickingStoring | WorkerAbility.CarryBox,
			WorkerTask.TaskType.Labeling => WorkerAbility.Labeling,
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
		Unloading = 0,
		IB,
		CapsuleClear,
		CapsuleSupply,
		Storing,
		OB,
		Picking,
		Packing,
		Loading,
		CargoTransfer,
		PackingInput,
		PackingOutput,

		
		Undefined = 999,

		HandleMistake,

		// cross-building
		Labeling,
	}

	public enum Status
	{
		Ready,
		Blocked,
		Assigned,
		End
	}

	private IBaseNode baseNode = null;
	private static ZoneManager ZoneManager => GameContext.HasInstance ? GameContext.Instance.ZoneMgr : null;

	protected CarryBoxAbility WorkerCarryBox => OccupyWorker != null ? OccupyWorker.CarryingAbility : null;

	public AIWorker OccupyWorker { get; private set; }
	public TaskType Type { get; private set; }
	public Status CurrentStatus { get; private set; } = Status.Blocked;
	public float TaskBuiltTime { get; private set; }
	public bool IsEmergency { get; private set; }
	public CarryBoxAbility CarryingAbility => WorkerCarryBox;

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
	public virtual bool TryGetPreferredWorker(out AIWorker worker)
	{
		worker = null;
		return false;
	}

	public virtual bool CanDispatchTo(AIWorker worker)
	{
		return worker != null;
	}

	public abstract string GetStatusSummary();

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

	protected static bool CanDispatchToWorkerZones(AIWorker worker, params IGridPlaceable[] endpoints)
	{
		if (worker == null)
			return false;

		if (endpoints == null || endpoints.Length == 0)
			return true;

		for (int i = 0; i < endpoints.Length; ++i)
		{
			if (CanDispatchToWorkerZone(worker, endpoints[i]) == false)
				return false;
		}

		return true;
	}

	protected static bool CanDispatchToWorkerZone(AIWorker worker, IGridPlaceable endpoint)
	{
		if (worker == null)
			return false;

		if (endpoint == null || ZoneManager == null)
			return true;

		if (ZoneManager.TryGetZoneAt(endpoint.GridPosition, out ZoneArea zone) == false || zone == null)
			return true;

		ZoneWorkerRule workerRule = zone.Rule?.WorkerRule;
		if (workerRule == null)
			return true;

		return workerRule.IsWorkerCapable(new ZoneWorkerFilter(worker));
	}

	//public static void SetTaskManager(TaskManager taskManager) { Manager = taskManager; }
}
