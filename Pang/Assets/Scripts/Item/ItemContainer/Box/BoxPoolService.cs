using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

public class BoxPoolService
{
	// 실제 박스들
	private List<BoxBase> boxes = new();

	// 박스 보관소들
	private List<BoxPool> boxPoolZones = new();

	public IReadOnlyList<BoxBase> Boxes => boxes;
	public IReadOnlyList<BoxPool> BoxPoolZones => boxPoolZones;

	public void RegisterPool(BoxPool boxPool)
	{
		boxPoolZones.Add(boxPool); 
	}

	public void UnRegisterPool(BoxPool boxPool)
	{
		boxPoolZones.Remove(boxPool);
	}

	public void RegisterBox(BoxBase box)
	{
		boxes.Add(box);
	}

	public void UnRegisterBox(BoxBase box)
	{
		boxes.Remove(box);
	}

	public BoxPool GetClosestAvailablePool(int3 pos)
	{
		BoxPool pool = null;
		int posPowMin = int.MaxValue;

		for (int i = 0; i < boxPoolZones.Count; ++i)
		{
			// todo 다른층에 대해서는 별도 판정을 해야함
			int3 boxPos = boxPoolZones[i].GridPosition;
			int3 posDelta = new int3((pos.x - boxPos.x), 0, pos.z - boxPos.z);
			posDelta.x *= posDelta.x;
			posDelta.y *= posDelta.y;
			posDelta.z *= posDelta.z;

			int sum = posDelta.x + posDelta.y + posDelta.z;

			if (posPowMin > sum)
			{
				posPowMin = sum;
				pool = boxPoolZones[i];
			}
		}

		return pool;
	}
}
