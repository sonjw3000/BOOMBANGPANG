using System;

public class ActionNode : IBaseNode
{
	public Func<IBaseNode.ENodeState> ActionFunc;

	ActionNode(Func<IBaseNode.ENodeState> actionFunc)
	{
		ActionFunc = actionFunc;
	}

	public IBaseNode.ENodeState Evaluate()
	{
		// todo
		// running 상태일 시
		// 조건에 따라 AI를 일정 기간(목표지점에 도달하기, 애니메이션 재생까지 대기하기)동안
		// AI 비활성화 큐에 넣어두기

		return ActionFunc?.Invoke() ?? IBaseNode.ENodeState.Failure;
	}
}