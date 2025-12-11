using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using static BoxPool;
using static WorkerTask;

public class BoxPoolManager : MonoBehaviour
{
	private List<BoxPool> boxPoolZones = new();


	public BoxPool GetClosestAvailablePool(int3 pos)
	{
		BoxPool pool = null;
		int posPowMin = int.MaxValue;

		for (int i = 0; i < boxPoolZones.Count; ++i)
		{
			// todo 다른층에 대해서는 별도 판정을 해야함
			int3 boxPos = boxPoolZones[i].Position;
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
