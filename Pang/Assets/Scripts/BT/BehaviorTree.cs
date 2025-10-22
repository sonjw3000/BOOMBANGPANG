
using UnityEngine;
using static IBaseNode;
using BlackBoardSystem;

public class BehaviorTree
{

	private IBaseNode RootNode;

	public BehaviorTree(IBaseNode node)
	{
		RootNode = node;
	}

	public ENodeState RunBT(BTContext ctx)
	{
		return RootNode.Evaluate(ctx);
	}

}