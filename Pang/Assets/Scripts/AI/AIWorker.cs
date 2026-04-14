using Assets.Scripts.AI.BT;
using Unity.Mathematics;
using UnityEngine;

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

public enum WorkActionType
{
	PickItem,
	PutItem,
	PickBox,
	PutBox,
	PackItem,
	MoveBox
}

[System.Serializable]
public abstract partial class AIWorker : MonoBehaviour, IGridPlaceable, IGridPlacementEffect
{
	[SerializeField] private WorkerArchetype workerArchetype;

	[SerializeField] private WorkerTask.TaskType workerMainTaskType = WorkerTask.TaskType.Undefined;
	private FindRoute routeFinder;
	
	[SerializeField] private int tick = 0;
	[SerializeField] private string workerName;
	[SerializeField] private int workerID;
	[SerializeField] private WorkerTask currentTask = null;

	private BehaviorTree behaviorTree;
	private readonly BlackBoard localBlackBoard = new();

	private int3 position;

	public float BaseMoveSpeedMultiplier => workerArchetype.baseMoveSpeedMultiplier;
	public float MinimumMoveSpeedMultiplier => workerArchetype.minimumMoveSpeedMultiplier;
	public float BaseWorkSpeedMultiplier => workerArchetype.baseWorkSpeedMultiplier;
	public float MinimumWorkSpeedMultiplier => workerArchetype.minimumWorkSpeedMultiplier;

	public WorkerType WorkerType => workerArchetype.workerType;

	protected WorkerArchetype Archetype => workerArchetype;

	public int MonthlyCost => workerArchetype.monthlyCost;

	// should build BT here
	private void BuildBehaviorTree()
	{
		SelectorNode root = new();

		ActionNode performTask = new(DoWork);
		WaitNode wait = new(1.0f);

		root.Add(performTask);
		root.Add(wait);

		behaviorTree = new BehaviorTree(root);
	}

	public WorkerTask.TaskType TaskType => workerMainTaskType;
	public string Name => workerName;
	public int WorkerID => workerID;
	public WorkerTask CurrentTask => currentTask;

	public int3 GridPosition => position;


	static private WorkerManager WorkerMgr => GameContext.Instance.WorkerMgr;

	private void Start()
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

	private void OnDestroy()
	{
		// unregister AI
		WorkerMgr.UnregisterWorker(this);
	}

	public bool TryAttachBox(BoxBase box)
	{
		gameObject.TryGetComponent<CarryBoxAbility>(out var component);
		if (component == null)
		{
			Debug.LogError("No CarryBox Ability!!!!!!");
			return false;
		}

		//Debug.Log("Attached!");

		return component.PutBox(box);
	}

	public bool TryDetachBox(out BoxBase box)
	{
		gameObject.TryGetComponent<CarryBoxAbility>(out var component);
		if (component == null)
		{
			Debug.LogError("No CarryBox Ability!!!!!!");
			box = null;
			return false;
		}

		return component.GetBox(out box);
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
		task?.SetAIWorker(this);
		currentTask = task;
	}

	// for findroute only
	public void SetPosition(in int3 position)
	{
		this.position = position;
	}

	public void OnPositionSet(in int3 position)
	{
		enabled = true;
		SetPosition(position);
	}

	public void OnRemoved()
	{
		//int3 previousNode = routeFinder.PreviousNode;
		//int3 nextNode = routeFinder.NextNode;

		//Cell[,,] map = GameContext.Instance.MapResources.mapRef;

		//if (previousNode.x >= 0 && previousNode.y >= 0 && previousNode.z >= 0)
		//{
		//	Cell prevCell = map[previousNode.x, previousNode.y, previousNode.z];
		//	prevCell.type = prevCell.previousType;
		//}
		//if (nextNode.x >= 0 && nextNode.y >= 0 && nextNode.z >= 0)
		//{
		//	Cell nextCell = map[nextNode.x, nextNode.y, nextNode.z];
		//	nextCell.type = nextCell.previousType;
		//}
	}

	public void OnDestroyedBy(in DestroyContext ctx)
	{
		// 당장 생각나는거만 적음
		// 들고 있던 태스크에 대해서 실패했다고 뭔가 해줘야하고
		// 또 뭐냐 산재처리 해줘야하는데 이건 아직 미구현이니까 투두리스트로 남겨야하고
		// 뭐 기타등등 해줘야하는데
		// 폭발도 하는게 좀 간지나긴 하는데 폭발은 로케트쪽에서 해주는게 나으려나
		// 
	}


	public virtual float GetWorkSpeedMultiplier() { return 1.0f; }
	public virtual float GetMoveSpeedMultiplier() { return 1.0f; }
	public virtual void OnTaskCompleted() { }
	public virtual void TickVitals(float deltaTime) { }

	public abstract void AddFatigue(float fatigue);
	public abstract float GetFatigue();

	// decreased chance by researches or some pieces of equipment
	public virtual float GetIncidentMitigationMultiplier() { return 1.0f; }
}
