using System;

public class ActionNode : IBaseNode
{
	public Func<BTContext, IBaseNode.ENodeState> ActionFunc;

	public ActionNode(Func<BTContext, IBaseNode.ENodeState> actionFunc)
	{
		ActionFunc = actionFunc;
	}

	public IBaseNode.ENodeState Evaluate(BTContext ctx)
	{
		// todo
		// running 상태일 시
		// 조건에 따라 AI를 일정 기간(목표지점에 도달하기, 애니메이션 재생까지 대기하기)동안
		// AI 비활성화 큐에 넣어두기

		return ActionFunc?.Invoke(ctx) ?? IBaseNode.ENodeState.Failure;
	}
}