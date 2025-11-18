using BlackBoardSystem;
using Unity.Mathematics;
using UnityEngine;

[System.Serializable]
public abstract class AIWorker : MonoBehaviour
{
	private FindRoute routeFinder;
	[SerializeField] private int tick = 0;
	
	protected BehaviorTree behaviorTree;
	protected BlackBoard localBlackBoard = new();

	protected abstract void BuildBlackBoard();
	protected abstract void BuildBehaviorTree();

	// should build BT here
	protected abstract void EnableAction();
	protected abstract void DisableAction();

	[SerializeField] public string Name { get; private set; }
	[SerializeField] public int WorkerID { get; private set; }
	[SerializeField] public WorkerTask CurrentTask { get; private set; } = null;


	public void Start()
	{
		// register AI's BT to AI Manager
		WorkerManager.Instance.RegisterWorker(this);

		//Debug.Log("AI 등장");

		routeFinder = transform.GetComponent<FindRoute>();
		routeFinder.SetAIMaster(this);
		BuildBlackBoard();
		BuildBehaviorTree();
		EnableAction();
	}

	//public void OnEnable()
	//{
	//	Debug.Log("AI Worker Enabled!");
	//}

	//public void OnDisable()
	//{
	//	Debug.Log("AI Worker Disabled!");
	//}

	public void OnDestroy()
	{
		// unregister AI
		WorkerManager.Instance.UnregisterWorker(this);

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
		if (task != null) 
			task.SetAIWorker(this);
		else
		{
			// release action
			//CurrentTask.On
		}
		CurrentTask = task;
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
