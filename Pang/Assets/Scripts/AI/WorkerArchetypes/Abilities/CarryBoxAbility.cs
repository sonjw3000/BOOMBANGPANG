using UnityEngine;

public class CarryBoxAbility : AbilityBase, IBoxHandleable
{
	private BoxBase carryingBox = null;

	[SerializeField] private Transform boxSlot;

	public BoxBase CarryingBox => carryingBox;

	public bool CanGetBox() => carryingBox != null;
	public bool CanPutBox() => carryingBox == null;

	protected override void OnInit()
	{
		boxSlot = Worker != null ? Worker.CarrySlot : transform.Find("SlotRoot");

		if (boxSlot == null)
		{
			Debug.Log("No slot for box!!!");
		}
	}

	public bool PutBox(BoxBase box)
	{
		if (box == null) return false;

		if (EnsureInitialized() == false)
			return false;

		// todo
		// 뭐 있는지 확인 해야함
		if (boxSlot == null)
		{
			boxSlot = Worker != null ? Worker.CarrySlot : null;

			if (boxSlot == null)
				boxSlot = transform.Find("SlotRoot");
		}

		if (boxSlot == null)
		{
			Debug.Log("No box slot!");
			return false;
		}

		box.transform.SetParent(boxSlot);
		box.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);

		carryingBox = box;
		carryingBox.OnInvalidated += HandleCarryingBoxInvalidated;
		Worker?.CurrentTask?.TrackPayloadBox(box);

		return true;
	}

	public bool GetBox(out BoxBase box)
	{
		box = null;
		if (carryingBox == null)
			return false;

		box = carryingBox;

		carryingBox.OnInvalidated -= HandleCarryingBoxInvalidated;
		carryingBox.transform.SetParent(null);
		carryingBox = null;
		Worker?.CurrentTask?.ReleasePayloadBox(box);

		return true;
	}

	public bool DropBoxForTaskRecovery(out BoxBase box)
	{
		box = carryingBox;
		if (box == null)
			return false;

		box.OnInvalidated -= HandleCarryingBoxInvalidated;
		box.transform.SetParent(null);
		box.transform.position = Worker != null ? Worker.transform.position : transform.position;
		carryingBox = null;
		return true;
	}

	private void HandleCarryingBoxInvalidated(BoxBase box)
	{
		if (box == null || carryingBox != box)
			return;

		box.OnInvalidated -= HandleCarryingBoxInvalidated;
		box.transform.SetParent(null, true);
		carryingBox = null;
	}
}
