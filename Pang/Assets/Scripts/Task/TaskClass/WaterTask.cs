using UnityEngine;

public class WaterTask : WorkerTask
{
	private IGridPlaceable from;
	private IGridPlaceable to;


	public WaterTask(IGridPlaceable from, IGridPlaceable to) : base(TaskType.Water)
	{
		this.from = from;
		this.to = to;
	}

	protected override void OnTaskAssigned()
	{
		// todo
		// check human like ability
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
		return false;
	}

#if UNITY_EDITOR
	public override string ShowStatus()
	{

		return $"[WaterTask] Working: {0}";
	}
#endif

}
