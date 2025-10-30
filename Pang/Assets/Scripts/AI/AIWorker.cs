using BlackBoardSystem;
using Unity.Mathematics;
using UnityEngine;

public abstract class AIWorker : MonoBehaviour
{
	private FindRoute routeFinder;
	private int tick = 0;
	
	protected BehaviorTree behaviorTree;
	protected BlackBoard localBlackBoard = new();

	protected abstract void BuildBlackBoard();
	protected abstract void BuildBehaviorTree();

	// should build BT here
	protected abstract void EnableAction();
	protected abstract void DisableAction();

	public string Name { get; private set; }
	public int WorkerID { get; private set; }


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

	//public void SetMoveOn()
	//{
	//	LocalBlackBoard.Set<bool>("testMoveOn", true);
	//}

	// AI's basic actions
	public static IBaseNode.NodeState SetDestination(in BTContext context)
	{
		// test code
		int3 pos = context.Worker.routeFinder.GetRandomPos();
		context.LocalBlackBoard.Set<int3>("goalPos", pos);
		//context.LocalBlackBoard.Set<int3>(BlackBoardKey<int3>.GoalPos, pos);

		// for real
		//context.LocalBlackBoard.TryGet<int3>(BlackBoardKey<int3>.GoalPos, out int3 goalPos);
		context.LocalBlackBoard.TryGet<int3>("goalPos", out int3 goalPos);
		context.Worker.routeFinder.enabled = true;
		context.Worker.routeFinder.SetGoalPosition(goalPos);

		return IBaseNode.NodeState.Success;
	}

	public static IBaseNode.NodeState MoveTo(in BTContext context)
	{
		if (context.Worker.routeFinder.IsGoal)
		{
			context.Worker.routeFinder.enabled = false;
			return IBaseNode.NodeState.Success;
		}
		context.Worker.enabled = false;

		return IBaseNode.NodeState.Running;
	}
}
