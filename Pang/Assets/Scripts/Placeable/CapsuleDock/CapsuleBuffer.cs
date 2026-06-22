using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

// box base를 보관하는 타일 단 하나

public partial class CapsuleBuffer : 
	CapsuleDock
{
	[SerializeField] private GameObject boxStackPos;

	public override WorkerStatusTarget BuildingTarget => WorkerStatusTarget.CapsuleBuffer;

	public override void OnPositionSet(in int3 position, FacingDirection direction)
	{
		enabled = true;
		this.position = position;
		this.facingDirection = direction;
	}

	public override void OnRemoved()
	{
	}

	public override void OnDestroyedBy(in DestroyContext ctx)
	{

	}

}
