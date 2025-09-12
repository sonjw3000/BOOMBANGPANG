using UnityEngine;

public abstract class BaseState
{
	protected AIWorker _Worker;

	protected BaseState (AIWorker worker)
	{
		_Worker = worker;
	}

	public abstract void OnEnter();
	public abstract void OnUpdate();
	public abstract void OnExit();
}
