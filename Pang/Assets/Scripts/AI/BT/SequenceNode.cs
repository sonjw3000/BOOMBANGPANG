using System.Collections.Generic;
using static IBaseNode;

public class SequenceNode : IBaseNode
{
	public List<IBaseNode> Children = new List<IBaseNode>();
	private int currentIndex = 0;
	private int lastTick = -1;

	public void Add(IBaseNode node) { Children.Add(node); }

	public NodeState Evaluate(in BTContext ctx)
	{
		if (lastTick + 1 != ctx.Tick)
		{
			currentIndex = 0;
			//UnityEngine.Debug.Log($"Index Changed!, cur tick: {ctx.Tick}, LastTick: {lastTick}");
		}

		lastTick = ctx.Tick;

		for (int i = currentIndex; i < Children.Count; ++i)
		{
			var res = Children[i].Evaluate(ctx);
			if (res == NodeState.Failure)
			{
				currentIndex = 0;
				return res;
			}
			else if (res == NodeState.Running)
			{
				//UnityEngine.Debug.Log("Suspended by Running!");
				currentIndex = i;
				return res;
			}
		}

		currentIndex = 0;
		return NodeState.Success;
	}

}
