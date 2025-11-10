using System;
using System.Collections.Generic;
using UnityEngine;

using static IBaseNode;

public class ActionNode : IBaseNode
{
	private ActionFunc actionFunction;
	
	public delegate NodeState ActionFunc(in BTContext context);

	public ActionNode(ActionFunc actionFunc)
	{
		actionFunction = actionFunc;
	}

	public NodeState Evaluate(in BTContext ctx)
	{
		// todo
		// running 상태일 시
		// 조건에 따라 AI를 일정 기간(목표지점에 도달하기, 애니메이션 재생까지 대기하기)동안
		// AI 비활성화 큐에 넣어두기

		return actionFunction?.Invoke(ctx) ?? IBaseNode.NodeState.Failure;
	}
}

public class WaitNode : IBaseNode
{
	private float waitTime;
	private float startTime;
	private bool isRunning = false;

	public WaitNode(float timeToWait)
	{
		waitTime = timeToWait;
	}

	public NodeState Evaluate(in BTContext ctx)
	{
		if (isRunning == false)
		{
			startTime = Time.time;
			isRunning = true;
		}

		if (Time.time - startTime > waitTime)
		{
			isRunning = false;
			return NodeState.Success;
		}

		return NodeState.Running;
	}
} 
