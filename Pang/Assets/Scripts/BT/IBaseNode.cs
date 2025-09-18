
public interface IBaseNode
{
	public enum ENodeState
	{
		Running,
		Success,
		Failure
	}
	public ENodeState Evaluate();
}