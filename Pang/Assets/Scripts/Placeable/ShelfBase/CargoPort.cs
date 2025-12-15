using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

public class CargoPort : 
	ShelfBase
{
	public int3 DockPoint => InteractionPoints[0];
	public int3 UnpackPoint => InteractionPoints[1];

	static private CargoPortService CargoPorts => GameContext.Instance.WMSys.CargoPorts;

	private void OnEnable()
	{
		CargoPorts.RegisterPort(this);
	}

	private void OnDisable()
	{
		CargoPorts.UnregisterPort(this);
	}


	protected override void SetInteractionPoints()
	{
		// have to set unpacking point / dock point
		var forward = transform.forward;

		// dockPoint
		interactionPoints.Add(new int3(
			GridPosition.x + Mathf.RoundToInt(forward.x),
			GridPosition.y + Mathf.RoundToInt(forward.y),
			GridPosition.z + Mathf.RoundToInt(forward.z)
			));

		forward *= -1;

		// unpackPoint
		interactionPoints.Add(new int3(
			GridPosition.x + Mathf.RoundToInt(forward.x),
			GridPosition.y + Mathf.RoundToInt(forward.y),
			GridPosition.z + Mathf.RoundToInt(forward.z)
			));
	}
}
