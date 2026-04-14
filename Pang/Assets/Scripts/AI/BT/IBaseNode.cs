
public interface IBaseNode
{
	public enum NodeState
	{
		Running,
		Success,
		Failure,
		Abort,
	}
	public NodeState Evaluate(in BTContext ctx);
}