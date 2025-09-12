using UnityEngine;

public class IdleState : BaseState
{
	public IdleState(AIWorker worker) : base(worker)
	{
	}

	public override void OnEnter() { }
	public override void OnUpdate()
	{
		Debug.Log(_Worker._WorkerID + " Idle");
	}
	public override void OnExit() { }

}
