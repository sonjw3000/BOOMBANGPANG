using BlackBoardSystem;
using System;
using Unity.Mathematics;
using UnityEngine;
using static ActionNode;

public class Human : AIWorker
{
	protected override void BuildBlackBoard()
	{
		LocalBlackBoard.Set<bool>("testMoveOn", false);
		LocalBlackBoard.Set<float>("testTime", 0.0f);
	}
	protected override void BuildBehaviorTree()
	{
		SelectorNode root = new SelectorNode();
		SequenceNode moveTo = new SequenceNode();
		ActionNode checkMoveOn = new ActionNode(TestMoveConfirm);
		ActionNode realMove = new ActionNode(MoveTo);

		ActionNode action = new ActionNode(WaitFor);
		
		moveTo.Add(checkMoveOn);
		moveTo.Add(realMove);
		root.Add(moveTo);
		root.Add(action);
		BTMain = new BehaviorTree(root);
	}

	protected override void EnableAction()
	{
		Debug.Log("사람 등장");
	}

	protected override void DisableAction()
	{
		Debug.Log("사람 퇴장");
	}



}

