using Unity.Mathematics;
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

public abstract partial class WorkerTask
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
		Assigned,
		Returned,
		Completed,
		Invalidated,
	}

	private IBaseNode baseNode = null;

	protected CarryBoxAbility WorkerCarryBox => OccupyWorker != null ? OccupyWorker.CarryingAbility : null;

	public AIWorker OccupyWorker { get; private set; }
	public TaskType Type { get; private set; }
	public Status CurrentStatus { get; private set; } = Status.Ready;
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
		RebuildTaskTree();
	}

	public bool SetAIWorker(AIWorker worker)
	{
		if (worker == null ||
			(CurrentStatus != Status.Ready && CurrentStatus != Status.Returned))
		{
			return false;
		}

		if (IsValidForDispatch == false)
			return false;

		bool isReassignment = CurrentStatus == Status.Returned;
		if (isReassignment)
			RebuildTaskTree();

		OccupyWorker = worker;
		CurrentStatus = Status.Assigned;
		if (isReassignment)
			OnTaskReassigned();
		else
			OnTaskAssigned();

		return true;
	}

	public void EndTask()
	{
		Manager.CompleteTask(this);
	}

	protected virtual void OnTaskAssigned() { }
	protected virtual void OnTaskReassigned() => OnTaskAssigned();
	protected virtual void OnTaskReturned(AIWorker worker) { }
	protected virtual void OnTaskInvalidated() { }

	internal bool MarkReturned(AIWorker worker, BoxBase recoveryBox, in int3 recoveryPosition)
	{
		if (CurrentStatus != Status.Assigned || worker == null || OccupyWorker != worker)
			return false;

		OnTaskReturned(worker);
		if (recoveryBox != null)
			PreparePayloadRecovery(recoveryBox, recoveryPosition);
		else
			ClearPayloadBox();

		OccupyWorker = null;
		CurrentStatus = Status.Returned;
		return true;
	}

	internal bool MarkCompleted(out AIWorker worker)
	{
		worker = OccupyWorker;
		if (CurrentStatus != Status.Assigned || worker == null)
			return false;

		ClearPayloadBox();
		CurrentStatus = Status.Completed;
		return true;
	}

	internal bool MarkInvalidated(out AIWorker worker)
	{
		worker = OccupyWorker;
		if (CurrentStatus == Status.Completed || CurrentStatus == Status.Invalidated)
			return false;

		OnTaskInvalidated();
		ClearPayloadBox();
		OccupyWorker = null;
		CurrentStatus = Status.Invalidated;
		return true;
	}

	internal void RestoreReturnedState()
	{
		if (CurrentStatus == Status.Ready)
			CurrentStatus = Status.Returned;
	}
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

	private void RebuildTaskTree()
	{
		SelectorNode root = new();
		root.Add(CheckFulfiledNode());

		SequenceNode work = new();
		work.Add(BuildPayloadRecoveryNode());
		work.Add(BuildWorkNode());
		root.Add(work);
		baseNode = root;
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

		if (endpoint == null)
			return true;

		if (endpoint is not IFacility facility)
			return true;

		return FacilityFilter.ForWorker(worker).MatchesCurrentRules(facility);
	}

	//public static void SetTaskManager(TaskManager taskManager) { Manager = taskManager; }
}
