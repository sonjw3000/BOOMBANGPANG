using UnityEngine;

public class MoveToTargetState : BaseState
{
	Vector3Int TargetPosition;
	public MoveToTargetState(AIWorker worker, Vector3Int targetPos) : base(worker)
	{
		// 왜 C#엔 이니셜라이저 리스트가 없는것이지?
		TargetPosition = targetPos;
	}

	public override void OnEnter() { }
	public override bool OnUpdate()
	{
		Debug.Log(Worker.WorkerID + " MoveToPosition");

		return true;
	}
	public override void OnExit() { }

}
