using BlackBoardSystem;
using System;
using Unity.Mathematics;
using UnityEngine;
using static ActionNode;

public class Human : AIWorker
{
	protected override void BuildBlackBoard()
	{

	}
	protected override void BuildBehaviorTree()
	{
		SelectorNode root = new SelectorNode();
		SequenceNode waitAndMove = new SequenceNode();

		WaitNode wait = new WaitNode(0.2f);
		ActionNode setDestination = new ActionNode(SetDestination);
		ActionNode realMove = new ActionNode(MoveTo);

		waitAndMove.Add(wait);
		waitAndMove.Add(setDestination);
		waitAndMove.Add(realMove);

		root.Add(waitAndMove);

		behaviorTree = new BehaviorTree(root);
	}

	protected override void EnableAction()
	{
		//Debug.Log("사람 등장");
	}

	protected override void DisableAction()
	{
		Debug.Log("사람 퇴장");
	}



}

