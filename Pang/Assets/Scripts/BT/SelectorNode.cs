using System.Collections.Generic;

using static IBaseNode;

public class SelectorNode : IBaseNode
{
	public List<IBaseNode> Children = new List<IBaseNode>();

	public void Add(IBaseNode node) { Children.Add(node); }


	public ENodeState Evaluate() 
	{
		foreach (IBaseNode node in Children)
		{
			var res = node.Evaluate();
			if (res != ENodeState.Failure) return res;
		}

		return ENodeState.Failure;
	}
}
