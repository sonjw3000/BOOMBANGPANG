using UnityEngine;

public class CarryBoxAbility : Ability<CarryBoxConfig>, IBoxHandleable
{
	private BoxBase carryingBox = null;

	[SerializeField] private Transform boxSlot;

	public BoxBase CarryingBox => carryingBox;

	public bool CanGetBox() => carryingBox != null;
	public bool CanPutBox() => carryingBox == null;

	protected override void OnInit()
	{
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

		carryingBox = box;

		return true;
	}

	public bool GetBox(out BoxBase box)
	{
		box = null;
		if (carryingBox == null)
			return false;

		box = carryingBox;

		carryingBox.transform.SetParent(null);
		carryingBox = null;

		return true;
	}
}
