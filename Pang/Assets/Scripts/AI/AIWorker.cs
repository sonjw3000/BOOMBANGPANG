using BlackBoardSystem;
using Unity.Mathematics;
using UnityEngine;

public abstract class AIWorker : MonoBehaviour
{
	public string Name { get; private set; }
	public int WorkerID { get; private set; }
	protected BehaviorTree BTMain;
	protected BlackBoard LocalBlackBoard = new();

	protected abstract void BuildBlackBoard();
	protected abstract void BuildBehaviorTree();

	// should build BT here
	protected abstract void EnableAction();
	protected abstract void DisableAction();

	FindRoute RouteFinder;

	public void Start()
	{
		// register AI's BT to AI Manager
		WorkerManager.Instance.RegisterWorker(this);

		Debug.Log("AI 등장");

		RouteFinder = transform.GetComponent<FindRoute>();
		RouteFinder.SetAIMaster(this);
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

	public bool RunBT()
	{
		BTContext btx;
		btx.deltaTime = 0.016f;
		btx.LocalBlackBoard = LocalBlackBoard;
		btx.GlobalBlackBoard = LocalBlackBoard;
		btx.Worker = this;

		BTMain?.RunBT(btx);

		return true;
	}

	//public void SetMoveOn()
	//{
	//	LocalBlackBoard.Set<bool>("testMoveOn", true);
	//}

	// AI's basic actions
	public static IBaseNode.ENodeState SetDestination(in BTContext context)
	{
		// test code
		int3 pos = context.Worker.RouteFinder.GetRandomPos();
		context.LocalBlackBoard.Set<int3>("goalPos", pos);
		//context.LocalBlackBoard.Set<int3>(BlackBoardKey<int3>.GoalPos, pos);

		// for real
		//context.LocalBlackBoard.TryGet<int3>(BlackBoardKey<int3>.GoalPos, out int3 goalPos);
		context.LocalBlackBoard.TryGet<int3>("goalPos", out int3 goalPos);
		context.Worker.RouteFinder.enabled = true;
		context.Worker.RouteFinder.SetGoalPosition(goalPos);

		return IBaseNode.ENodeState.Success;
	}

	public static IBaseNode.ENodeState MoveTo(in BTContext context)
	{
		if (context.Worker.RouteFinder.IsGoal)
		{
			context.Worker.RouteFinder.enabled = false;
			return IBaseNode.ENodeState.Success;
		}
		context.Worker.enabled = false;

		return IBaseNode.ENodeState.Running;
	}
}
