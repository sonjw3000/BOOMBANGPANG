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

		ActionNode performTask = new ActionNode(DoWork);
		WaitNode wait = new WaitNode(1.0f);
		
		root.Add(performTask);
		root.Add(wait);

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

