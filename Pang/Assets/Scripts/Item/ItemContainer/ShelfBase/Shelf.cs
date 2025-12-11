using Unity.Mathematics;
using UnityEngine;


public class Shelf : ShelfBase
{
	void OnEnable()
	{
		// todo
		// y 좌표에 대해선 별도 처리를 해야하기 때문에
		// 나중에 이를 고쳐야 한다
		Debug.Log("Shelf 등장이요");


		GameContext.Instance.ItemInventoryData.OnContainerAdded(this);
	}
	void OnDisable()
	{
		GameContext.Instance.ItemInventoryData.OnContainerRemoved(this);
	}

	protected override void SetPickingPosition()
	{
		pickingPosition = new int3(
			Mathf.RoundToInt(GridPosition.x + transform.forward.x),
			Mathf.RoundToInt(GridPosition.y),
			Mathf.RoundToInt(GridPosition.z + transform.forward.z)
		);
	}
}
