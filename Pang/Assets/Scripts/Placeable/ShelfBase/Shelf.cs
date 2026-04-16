using UnityEngine;


public class Shelf : ShelfBase
{
	public override WorkerStatusTarget BuildingTarget => WorkerStatusTarget.Shelf;
	void OnEnable()
	{
		// todo
		// y 좌표에 대해선 별도 처리를 해야하기 때문에
		// 나중에 이를 고쳐야 한다
		//Debug.Log("Shelf 등장이요");


		GameContext.Instance.StorageIndex.OnContainerAdded(this);
	}
	void OnDisable()
	{
		GameContext.Instance.StorageIndex.OnContainerRemoved(this);
	}

}
