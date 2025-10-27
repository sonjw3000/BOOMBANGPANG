using BlackBoardSystem;
using Unity.Mathematics;
using UnityEngine;

public abstract class AIWorker : MonoBehaviour
{
	public string Name { get; private set; }
	public int WorkerID { get; private set; }
	protected BehaviorTree BTMain;
	[SerializeField] protected BlackBoard LocalBlackBoard = new();

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

	public void SetMoveOn()
	{
		LocalBlackBoard.Set<bool>("testMoveOn", true);
		LocalBlackBoard.Set<bool>("testMoveOn", true);
	}

	// AI's basic actions
	public static IBaseNode.ENodeState WaitFor(in BTContext context)
	{
		context.LocalBlackBoard.TryGet<float>("testTime", out float time);
		if (time < 5.0f)
			return IBaseNode.ENodeState.Running;
		else
		{
			context.LocalBlackBoard.Set<float>("testTime", 0.0f);
			Debug.Log("WaitEnd");
			return IBaseNode.ENodeState.Success;
		}
	}

	public static IBaseNode.ENodeState MoveTo(in BTContext context)
	{
		if (context.Worker.RouteFinder.IsGoal)
		{
			context.Worker.RouteFinder.enabled = false;
			return IBaseNode.ENodeState.Success;
		}

		if (context.Worker.RouteFinder.enabled == false)
		{
			context.LocalBlackBoard.TryGet<int3>("goalPos", out int3 goalPos);
			context.Worker.RouteFinder.enabled = true;
			context.Worker.RouteFinder.SetGoalPosition(goalPos);
			// todo 
			// bt를 잠시 비활성화 해야함
		}

		return IBaseNode.ENodeState.Running;
	}

	public static IBaseNode.ENodeState TestMoveConfirm(in BTContext context)
	{
		context.LocalBlackBoard.TryGet<bool>("testMoveOn", out bool test);

		if (test)
		{
			int3 goalPos = context.Worker.RouteFinder.GetRandomPos();
			context.LocalBlackBoard.Set<int3>("goalPos", goalPos);
			context.LocalBlackBoard.Set<bool>("testMoveOn", false);
			context.Worker.RouteFinder.SetGoalPosition(goalPos);
			return IBaseNode.ENodeState.Success;
		}
		else return IBaseNode.ENodeState.Failure;
	}

}
