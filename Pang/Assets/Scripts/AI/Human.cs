using UnityEngine;

class Human : AIWorker
{
	protected override void EnableAction()
	{
		Debug.Log("사람 등장");
		// build BT

	}

	protected override void DisableAction()
	{
		Debug.Log("사람 퇴장");
	}
}

