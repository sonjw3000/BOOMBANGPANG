using BlackBoardSystem;
using System;
using UnityEngine;
using static ActionNode;

class Human : AIWorker
{
	protected override void BuildBlackBoard()
	{
		LocalBlackBoard.Set<float>("testTime", 0.0f);
	}
	protected override void BuildBehaviorTree()
	{
		SelectorNode root = new SelectorNode();
		SequenceNode sequence = new SequenceNode();
		ActionNode action = new ActionNode(WaitFor);
		sequence.Add(action);
		root.Add(sequence);
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

