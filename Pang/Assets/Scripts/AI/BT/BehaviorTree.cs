using static IBaseNode;

public class BehaviorTree
{

	private IBaseNode rootNode;

	public BehaviorTree(IBaseNode node)
	{
		rootNode = node;
	}
	public NodeState RunBT(in BTContext ctx)
	{
		return rootNode.Evaluate(ctx);
	}

}