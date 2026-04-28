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
	public override WorkerStatusTarget BuildingTarget => WorkerStatusTarget.CargoPort;
	static private CargoPortService IBCargoPorts => GameContext.Instance.IBWorkflowMgr.CargoPorts;
	static private CargoPortService OBCargoPorts => GameContext.Instance.OBWorkflowMgr.CargoPorts;

	public bool IsInbound => isInbound;

	public void SetInputReady(bool ready)
	{
		inputReady = ready;
	}



	private void OnEnable()
	{
		if (isInbound)
			IBCargoPorts.Register(this);
		else
			OBCargoPorts.Register(this);
	}

	private void OnDisable()
	{
		if (isInbound)
			IBCargoPorts.Unregister(this);
		else
			OBCargoPorts.Unregister(this);
	}

}
