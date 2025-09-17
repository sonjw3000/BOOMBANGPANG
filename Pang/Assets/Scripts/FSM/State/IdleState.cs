using UnityEngine;

public class IdleState : BaseState
{
	public IdleState(AIWorker worker) : base(worker)
	{
	}

	public override void OnEnter() { }
	public override bool OnUpdate()
	{
		Debug.Log(Worker.WorkerID + " Idle");

		return true;
	}
	public override void OnExit() { }

}
