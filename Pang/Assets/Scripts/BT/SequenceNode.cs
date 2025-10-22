using System.Collections.Generic;

public class SequenceNode : IBaseNode
{
	public List<IBaseNode> Children = new List<IBaseNode>();

	public void Add(IBaseNode node) { Children.Add(node); }

	public IBaseNode.ENodeState Evaluate(BTContext ctx)
	{
		foreach (var node in Children)
		{
			var res = node.Evaluate(ctx);
			if (res != IBaseNode.ENodeState.Success) return res;
		}

		return IBaseNode.ENodeState.Success;
	}
}
