using Unity.Mathematics;
using UnityEngine;


public class Shelf : ShelfBase
{
	void OnEnable()
	{
		Debug.Log("Shelf 등장이요");
		pickingPosition = new int3(
			Mathf.RoundToInt(transform.position.x + transform.forward.x),
			Mathf.RoundToInt(transform.position.y),
			Mathf.RoundToInt(transform.position.z + transform.forward.z)
		);

		GameContext.Instance.ItemInventoryData.OnContainerAdded(this);
	}
	void OnDisable()
	{
		GameContext.Instance.ItemInventoryData.OnContainerRemoved(this);
	}
}
