using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

public class CargoPort : 
	ShelfBase
{
	// ib/ob 구분
	// 런타임에 수정되면 안된다
	[SerializeField] private bool isInbound = true;
	//public int3 UnpackPoint => InteractionPointMap[isInbound ? InteractionKind.Put : InteractionKind.Pick][0];
	//public int3 DockPoint => InteractionPointMap[isInbound ? InteractionKind.Pick : InteractionKind.Put][0];

	private bool inputReady = true;

	public bool InputReady => inputReady;

	static private CargoPortService IBCargoPorts => GameContext.Instance.IBWorkflowMgr.CargoPorts;
	static private CargoPortService OBCargoPorts => GameContext.Instance.OBWorkflowMgr.CargoPorts;

	public void SetInputReady(bool ready)
	{
		inputReady = ready;
	}



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

}
