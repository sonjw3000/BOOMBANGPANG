using Unity.Mathematics;
using UnityEngine;

public class CargoPort : 
	ShelfBase
{
	// ib/ob 구분
	// 런타임에 수정되면 안된다
	[SerializeField] private bool isInbound = true;
	public int3 UnpackPoint => InteractionPoints[0];
	public int3 DockPoint => InteractionPoints[1];

	static private CargoPortService IBCargoPorts => GameContext.Instance.IBWorkflowMgr.CargoPorts;
	static private CargoPortService OBCargoPorts => GameContext.Instance.OBWorkflowMgr.CargoPorts;

	private void OnEnable()
	{
		if (isInbound)
			IBCargoPorts.RegisterPort(this);
		else
			OBCargoPorts.RegisterPort(this);
	}

	private void OnDisable()
	{
		if (isInbound)
			IBCargoPorts.UnregisterPort(this);
		else
			OBCargoPorts.UnregisterPort(this);
	}


	protected override void SetInteractionPoints()
	{
		// have to set unpacking point / dock point
		var forward = transform.forward;

		// unpackPoint
		interactionPoints.Add(new int3(
			GridPosition.x + Mathf.RoundToInt(forward.x),
			GridPosition.y + Mathf.RoundToInt(forward.y),
			GridPosition.z + Mathf.RoundToInt(forward.z)
			));

		forward *= -1;

		// dockPoint
		interactionPoints.Add(new int3(
			GridPosition.x + Mathf.RoundToInt(forward.x),
			GridPosition.y + Mathf.RoundToInt(forward.y),
			GridPosition.z + Mathf.RoundToInt(forward.z)
			));
	}
}
