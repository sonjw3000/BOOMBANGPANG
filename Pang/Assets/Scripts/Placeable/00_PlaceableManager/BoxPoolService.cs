using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

public class BoxPoolService : GridPlaceableManager<BoxPool>
{
	[SerializeField] private BoxBase palletPrefab;
	[SerializeField] private BoxBase boxPrefab;

	[SerializeField] private float toteCapacity = 150.0f;

	// 실제 박스들
	private List<BoxBase> boxes = new();

	// 박스 보관소들
	//private List<BoxPool> boxPoolZones = new();

	// todo
	// boxtype에 따른 뭔가를 만들어줘야하는데 뭐라해야하지 이걸 어쨋든 그래

	public IReadOnlyList<BoxBase> Boxes => boxes;
	//public IReadOnlyList<BoxPool> BoxPoolZones => boxPoolZones;

	public float ToteCapacity => toteCapacity;

	public void RegisterBox(BoxBase box)
	{
		boxes.Add(box);
	}

	public void UnRegisterBox(BoxBase box)
	{
		boxes.Remove(box);
	}

	public void GiveNewBox(BoxPool boxPool, BoxType type)
	{
		var box = Instantiate(type == BoxType.Cargo ? palletPrefab : boxPrefab, boxPool.transform).GetComponent<BoxBase>();

		if (box is ToteBox tote)
			tote.UpdateToteCapacity(toteCapacity);

		boxPool.PutBox(box);
	}
}
