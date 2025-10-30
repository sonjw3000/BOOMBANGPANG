
public interface IBaseNode
{
	public enum NodeState
	{
		Running,
		Success,
		Failure
	}
	public NodeState Evaluate(BTContext ctx);
}