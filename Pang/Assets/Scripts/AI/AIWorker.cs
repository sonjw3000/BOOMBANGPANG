using BlackBoardSystem;
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

	public void Start()
	{
		// register AI's BT to AI Manager
		WorkerManager.Instance.RegisterWorker(this);

		Debug.Log("AI 등장");

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
		// 그뭐냐 목적지에 도달 했냐를 찾아야함
		context.LocalBlackBoard.TryGet<Vector3>("goalPos", out Vector3 goalPos);
		if (false)
		{
			return IBaseNode.ENodeState.Success;
		}

		return IBaseNode.ENodeState.Running;
	}

}
