using System.Collections.Generic;

public class SequenceNode : IBaseNode
{
	public List<IBaseNode> Children = new List<IBaseNode>();
	private int CurrentIndex = 0;

	public void Add(IBaseNode node) { Children.Add(node); }

	public IBaseNode.ENodeState Evaluate(BTContext ctx)
	{
		for (int i = CurrentIndex; i < Children.Count; ++i)
		{
			var res = Children[i].Evaluate(ctx);
			if (res == IBaseNode.ENodeState.Failure)
			{
				CurrentIndex = 0;
				return res;
			}
			else if (res == IBaseNode.ENodeState.Running)
			{
				CurrentIndex = i;
				return res;
			}
		}

		CurrentIndex = 0;
		return IBaseNode.ENodeState.Success;
	}
}
