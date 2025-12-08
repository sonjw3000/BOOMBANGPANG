using BlackBoardSystem;
using Unity.Mathematics;
using UnityEngine;

[System.Serializable]
public abstract class AIWorker : MonoBehaviour
{
	private WorkerTask.TaskType beforeWorkerTask = WorkerTask.TaskType.Undefined;
	private FindRoute routeFinder;
	
	[SerializeField] private int tick = 0;
	[SerializeField] private string workerName;
	[SerializeField] private int workerID;
	[SerializeField] private WorkerTask currentTask = null;

	protected BehaviorTree behaviorTree;
	protected BlackBoard localBlackBoard = new();

	protected abstract void BuildBlackBoard();
	protected abstract void BuildBehaviorTree();

	// should build BT here
	protected abstract void EnableAction();
	protected abstract void DisableAction();

	public WorkerTask.TaskType BeforeWorkerTask => beforeWorkerTask;
	public string Name => workerName;
	public int WorkerID => workerID;
	public WorkerTask CurrentTask => currentTask;

	protected WorkerManager WorkerMgr => GameContext.Instance.WorkerMgr;

	public void Start()
	{
		// register AI's BT to AI Manager
		WorkerMgr.RegisterWorker(this);

		routeFinder = transform.GetComponent<FindRoute>();

		if (routeFinder == null)
		{
			Debug.Log($"FindRoute가 null이다 해당 객체가 프리뷰가 아니라면 큰일이다, 이름: {this.name}");

			return;
		}

		routeFinder.SetAIMaster(this);
		BuildBlackBoard();
		BuildBehaviorTree();
		EnableAction();
	}

	public void OnDestroy()
	{
		// unregister AI
		WorkerMgr.UnregisterWorker(this);

		DisableAction();
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

	public void SetTask(WorkerTask task)
	{
		// 두가지 경우
		// 1. null -> st
		//		beftask는 유지
		// 2. st -> null
		//		beforetask를 st로 set
		
		if (task != null)
			task.SetAIWorker(this);
		else
			beforeWorkerTask = currentTask.Type;
	
		// release action
		//CurrentTask.On
		currentTask = task;
	}

	// AI's basic actions
	public static IBaseNode.NodeState SetDestination(in BTContext context)
	{
		// for real
		context.LocalBlackBoard.TryGet<int3>("goalPos", out int3 goalPos);
		context.Worker.routeFinder.enabled = true;
		context.Worker.routeFinder.SetGoalPosition(goalPos);

		return IBaseNode.NodeState.Success;
	}

	public static IBaseNode.NodeState MoveTo(in BTContext context)
	{
		if (context.Worker.routeFinder.IsGoal)
		{
			//Debug.Log("Goal Hit!");
			context.Worker.routeFinder.enabled = false;
			return IBaseNode.NodeState.Success;
		}
		context.Worker.enabled = false;

		return IBaseNode.NodeState.Running;
	}

	public static IBaseNode.NodeState DoWork(in BTContext context)
	{
		if (context.Worker.CurrentTask == null)
			return IBaseNode.NodeState.Failure;

		return context.Worker.CurrentTask.UpdateTaskNode(context);
	}

	public static IBaseNode.NodeState TaskCompleted(in BTContext ctx)
	{
		var task = ctx.Worker.CurrentTask;
		task.EndTask();

		Debug.Log("TaskCompleted!");

		return IBaseNode.NodeState.Success;
	}
}
