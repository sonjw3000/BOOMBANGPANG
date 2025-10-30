
using UnityEngine;
using static IBaseNode;
using BlackBoardSystem;

public class BehaviorTree
{

	private IBaseNode rootNode;

	public BehaviorTree(IBaseNode node)
	{
		rootNode = node;
	}

	public NodeState RunBT(BTContext ctx)
	{
		return rootNode.Evaluate(ctx);
	}

}