using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

public class LaunchPadService : MonoBehaviour
{
	private List<LaunchStation> launchPads = new();

	public void RegisterLaunchPad(LaunchStation pad)
	{
		launchPads.Add(pad);
	}
	public void UnregisterLaunchPad(LaunchStation pad)
	{
		launchPads.Remove(pad);
	}

	public LaunchStation GetClosestAvailableTarget(int3 pos)
	{
		LaunchStation target = null;
		int posPowMin = int.MaxValue;

		for (int i = 0; i < launchPads.Count; ++i)
		{
			// todo 다른층에 대해서는 별도 판정을 해야함
			int3 boxPos = launchPads[i].GridPosition;
			int3 posDelta = new int3((pos.x - boxPos.x), 0, pos.z - boxPos.z);
			posDelta.x *= posDelta.x;
			posDelta.y *= posDelta.y;
			posDelta.z *= posDelta.z;

			int sum = posDelta.x + posDelta.y + posDelta.z;

			if (posPowMin > sum)
			{
				posPowMin = sum;
				target = launchPads[i];
			}
		}

		return target;
	}

}
