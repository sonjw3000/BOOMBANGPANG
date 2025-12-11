using BlackBoardSystem;
using Unity.Mathematics;
using UnityEngine;
using static IBaseNode;
using static IBaseNode.NodeState;

[System.Flags]
public enum WorkerAbility
{
	None = 0,
	CarryBox = 1 << 0,
	PickOrStore = 1 << 1,
	Package = 1 << 2,
	Labeling = 1 << 3,
	// ...
}

[System.Serializable]
public sealed class AIWorker : MonoBehaviour
{
	[SerializeField] WorkerArchetype workerArchetype;

	[SerializeField] private WorkerTask.TaskType workerMainTaskType = WorkerTask.TaskType.Undefined;
	private FindRoute routeFinder;
	
	[SerializeField] private int tick = 0;
	[SerializeField] private string workerName;
	[SerializeField] private int workerID;
	[SerializeField] private WorkerTask currentTask = null;

	private BehaviorTree behaviorTree;
	private BlackBoard localBlackBoard = new();

	// should build BT here
	private void BuildBehaviorTree()
	{
		SelectorNode root = new SelectorNode();

		ActionNode performTask = new ActionNode(DoWork);
		WaitNode wait = new WaitNode(1.0f);

		root.Add(performTask);
		root.Add(wait);

		behaviorTree = new BehaviorTree(root);
	}

	public WorkerTask.TaskType TaskType => workerMainTaskType;
	public string Name => workerName;
	public int WorkerID => workerID;
	public WorkerTask CurrentTask => currentTask;

	private WorkerManager WorkerMgr => GameContext.Instance.WorkerMgr;

	public void Start()
	{
		routeFinder = transform.GetComponent<FindRoute>();

		if (routeFinder == null)
		{
			Debug.Log($"FindRoute가 null이다 해당 객체가 프리뷰가 아니라면 큰일이다, 이름: {this.name}");

			return;
		}

		workerArchetype.SetupWorker(this);

		// register AI's BT to AI Manager
		WorkerMgr.RegisterWorker(this);

		routeFinder.SetAIMaster(this);
		BuildBehaviorTree();
	}

	public void OnDestroy()
	{
		// unregister AI
		WorkerMgr.UnregisterWorker(this);
	}

	public bool RunBT(BlackBoard GlobalBlackboard)
	{
		BTContext btx;
		btx.DeltaTime = 0.016f;
		btx.LocalBlackBoard = localBlackBoard;
		btx.GlobalBlackBoard = GlobalBlackboard;
		btx.Worker = this;
		btx.Tick = tick++;

		behaviorTree?.RunBT(btx);

		return true;
	}

	public void ChangeWorkerType(WorkerTask.TaskType taskType)
	{
		workerMainTaskType = taskType;
	}

	public void SetTask(WorkerTask task)
	{
		if (task != null)
		{
			task.SetAIWorker(this);
		}
		currentTask = task;
	}

	// AI's basic actions
	public static NodeState SetDestination(in BTContext context)
	{
		// for real
		context.LocalBlackBoard.TryGet<int3>("goalPos", out int3 goalPos);
		context.Worker.routeFinder.enabled = true;
		context.Worker.routeFinder.SetGoalPosition(goalPos);

		return Success;
	}

	public static NodeState MoveTo(in BTContext context)
	{
		if (context.Worker.routeFinder.IsGoal)
		{
			//Debug.Log("Goal Hit!");
			context.Worker.routeFinder.enabled = false;
			return Success;
		}
		context.Worker.enabled = false;

		return Running;
	}

	public static NodeState DoWork(in BTContext context)
	{
		if (context.Worker.CurrentTask == null)
			return Failure;

		return context.Worker.CurrentTask.UpdateTaskNode(context);
	}

	public static NodeState TaskCompleted(in BTContext ctx)
	{
		var task = ctx.Worker.CurrentTask;
		task.EndTask();

		Debug.Log("TaskCompleted!");

		return Success;
	}

	public static NodeState TaskFailed(in BTContext ctx)
	{
		var task = ctx.Worker.CurrentTask;
		task.EndTask();

		Debug.Log("TaskFailed...");

		return Success;
	}
}
