using System;
using System.Collections.Generic;
using UnityEngine;

public class ActionNode : IBaseNode
{
	public delegate IBaseNode.ENodeState ActionFunc(in BTContext context);
	private ActionFunc _ActionFunction;

	public ActionNode(ActionFunc actionFunc)
	{
		_ActionFunction = actionFunc;
	}

	public IBaseNode.ENodeState Evaluate(BTContext ctx)
	{
		// todo
		// running 상태일 시
		// 조건에 따라 AI를 일정 기간(목표지점에 도달하기, 애니메이션 재생까지 대기하기)동안
		// AI 비활성화 큐에 넣어두기

		return _ActionFunction?.Invoke(ctx) ?? IBaseNode.ENodeState.Failure;
	}
}

public class WaitNode : IBaseNode
{
	private float _WaitTime;
	private float _StartTime;
	private bool _IsRunning = false;

	public WaitNode(float timeToWait)
	{
		_WaitTime = timeToWait;
	}

	public IBaseNode.ENodeState Evaluate(BTContext ctx)
	{
		if (_IsRunning == false)
		{
			_StartTime = Time.time;
			_IsRunning = true;
		}

		if (Time.time - _StartTime > _WaitTime)
		{
			_IsRunning = false;
			return IBaseNode.ENodeState.Success;
		}

		return IBaseNode.ENodeState.Running;
	}
} 
