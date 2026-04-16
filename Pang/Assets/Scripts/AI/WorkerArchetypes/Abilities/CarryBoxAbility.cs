using UnityEngine;

[System.Serializable]
public class CarryBoxConfig : AbilityConfigBase
{
	public float carriableSize;

	public override void Setup(AIWorker worker)
	{
		var ability = worker.gameObject.AddComponent<CarryBoxAbility>();
		ability.Initailize(worker, this);
	}
}

public class CarryBoxAbility : Ability<CarryBoxConfig>, IBoxHandleable
{
	private float carriableSize;
	private BoxBase carringBox = null;

	[SerializeField] private Transform boxSlot;

	public BoxBase CarringBox => carringBox;

	public bool CanGetBox() => carringBox != null;
	public bool CanPutBox() => carringBox == null;

	protected override void OnInit()
	{
		carriableSize = Config.carriableSize;

		boxSlot = Worker.transform.Find("SlotRoot");

		if (boxSlot == null)
		{
			Debug.Log("No slot for box!!!");
		}
	}

	public bool PutBox(BoxBase box)
	{
		if (box == null) return false;
		// todo
		// 뭐 있는지 확인 해야함
		if (boxSlot == null)
		{
			Debug.Log("No box slot!");
			return false;
		}

		box.transform.SetParent(boxSlot);
		box.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);

		carringBox = box;

		return true;
	}

	public bool GetBox(out BoxBase box)
	{
		box = null;
		if (carringBox == null)
			return false;

		box = carringBox;

		carringBox.transform.SetParent(null);
		carringBox = null;

		return true;
	}
}
