

// storing 전략에 따라 sorting 목적지를 다르게 설정해야 할 수도 있음
// 쿠팡 입고 했던 애들 말 들어보면 이제
// 실제 물건 저장은 지들이 알아서 한다는데
// 내가 구상한 방식은 아이템을 직접 선반에 지정하는 방식이라
// 이거에 맞게 구현해야 할 듯


public class StoringTask : WorkerTask
{
	private ToteBox currentBox;

	public StoringTask(ToteBox currentBox) : base(TaskType.Storing)
	{
		this.currentBox = currentBox;
	}

	protected override void BuildTaskNode()
	{

	}

#if UNITY_EDITOR
	public override string ShowStatus()
	{
		return $"[StoringTask] BoxStatus: {currentBox.Stacks.Count}";
	}
#endif

	public override IBaseNode.NodeState UpdateTaskNode(in BTContext ctx)
	{
		// picking task와 비슷한 로직으로 구현하면 될 듯

		return IBaseNode.NodeState.Success;
	}
}
