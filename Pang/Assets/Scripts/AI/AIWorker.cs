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
public sealed partial class AIWorker : MonoBehaviour, IGridPlaceable
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

	private int3 position;


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

	public int3 GridPosition => position;


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

	public void OnPositionSet(Cell[,,] map, int3 position)
	{
		this.position = position;
	}

	public void OnReset(Cell[,,] map)
	{
		int3 previousNode = routeFinder.PreviousNode;
		int3 nextNode = routeFinder.NextNode;


		if (previousNode.x >= 0 && previousNode.y >= 0 && previousNode.z >= 0)
		{
			Cell prevCell = map[previousNode.x, previousNode.y, previousNode.z];
			prevCell.type = prevCell.previousType;
		}
		if (nextNode.x >= 0 && nextNode.y >= 0 && nextNode.z >= 0)
		{
			Cell nextCell = map[nextNode.x, nextNode.y, nextNode.z];
			nextCell.type = nextCell.previousType;
		}
	}


	// findroute만 써라
	public void SetGridPos(int3 pos)
	{
		position = pos;
	}
}
