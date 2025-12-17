
// storing 전략에 따라 sorting 목적지를 다르게 설정해야 할 수도 있음
// 쿠팡 입고 했던 애들 말 들어보면 이제
// 실제 물건 저장은 지들이 알아서 한다는데
// 내가 구상한 방식은 아이템을 직접 선반에 지정하는 방식이라
// 이거에 맞게 구현해야 할 듯


public class StoringTask : WorkerTask
{
	private WorkJob storeJob;

	public WorkLine CurrentLine => storeJob.Lines[storeJob.CurrentLineIndex];

	public StoringTask(WorkJob job) : base(TaskType.Storing)
	{
		storeJob = job;
	}

	protected override void OnTaskAssigned()
	{
		carryBox = OccupyWorker.GetComponent<CarryBoxAbility>();
	}

	protected override void BuildTaskNode()
	{
		SelectorNode root = new SelectorNode();

		SequenceNode checkingFulfilled = new SequenceNode();
		//checkingFulfilled.Add(new ActionNode(CheckFulfilled));
		checkingFulfilled.Add(new ActionNode(AIWorker.TaskCompleted));

		// work node
		// pick box -> pick items with storeJob -> put items by 
		SequenceNode work = new SequenceNode();
		work.Add(AIWorker.GetBox(BoxType.Personal));
	}

#if UNITY_EDITOR
	public override string ShowStatus()
	{
		return $"[StoringTask] CurrentIndex: {storeJob.CurrentLineIndex}";
	}
#endif

	public override IBaseNode.NodeState UpdateTaskNode(in BTContext ctx)
	{
		// picking task와 비슷한 로직으로 구현하면 될 듯

		return IBaseNode.NodeState.Success;
	}
}
