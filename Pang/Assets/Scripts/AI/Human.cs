using BlackBoardSystem;
using System;
using UnityEngine;
using static ActionNode;

class Human : AIWorker
{
	protected override void EnableAction()
	{
		Debug.Log("사람 등장");
		// build BT

		// test bt building
		SelectorNode root = new SelectorNode();
		SequenceNode sequence = new SequenceNode();

		ActionFunc abc = (in BTContext bb) => {
			bool temp;
			bb.LayeredBB.TryGet<bool>(new BlackBoardKey<bool>("test"), out temp);

			if (temp)
			{
				Debug.Log("SUCCESS");
				return IBaseNode.ENodeState.Success;
			}
			else return IBaseNode.ENodeState.Failure;
		};

		ActionNode action = new ActionNode(abc);
		sequence.Add(action);
		root.Add(sequence);
		BTMain = new BehaviorTree(root);

		// set black board data 
		blackBoard.Set<bool>(new BlackBoardKey<bool>("test"), false);
	}

	protected override void DisableAction()
	{
		Debug.Log("사람 퇴장");
	}



}

