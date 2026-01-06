
public abstract class BaseState
{
	protected AIWorker Worker;

	protected BaseState (AIWorker worker)
	{
		Worker = worker;
	}

	public abstract void OnEnter();

	// if job is completed, return true
	public abstract bool OnUpdate();
	public abstract void OnExit();
}
 