using UnityEngine;


public class LoadingTask : WorkerTask
{
	private bool isLoadEnd = false;

	public LoadingTask() : base(TaskType.Loading)
	{
	}

	protected override void OnTaskAssigned()
	{
		carryBox = OccupyWorker.GetComponent<CarryBoxAbility>();

		if (carryBox == null)
		{
			Debug.LogError("No carryBox ability but assigned to ccc!!");
		}
	}


	protected override IBaseNode BuildWorkNode()
	{


		return null;
	}

	public override bool CheckTaskEnd()
	{
		return isLoadEnd;
	}

#if UNITY_EDITOR
	public override string ShowStatus()
	{
		return $"[LoadingTask] : ";
	}
#endif

}
