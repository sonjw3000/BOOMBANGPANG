using UnityEngine;

public class TempRobot : AIWorker
{
	protected override void BuildBlackBoard()
	{

	}

	protected override void BuildBehaviorTree()
	{

	}

	protected override void EnableAction()
	{
		Debug.Log("사람 등장");
	}

	protected override void DisableAction()
	{
		Debug.Log("사람 등장");
	}
}
