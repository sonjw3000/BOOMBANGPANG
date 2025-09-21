
using static IBaseNode;

public class BehaviorTree
{
	//private BlackBoard BlackBoard;

	private IBaseNode RootNode;

	public BehaviorTree(IBaseNode node)
	{
		RootNode = node;
	}

	public ENodeState RunBT()
	{
		return RootNode.Evaluate();
	}

}