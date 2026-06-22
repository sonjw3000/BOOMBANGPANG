using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

// box base를 보관하는 타일 단 하나

public class CapsuleBuffer : 
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

	public CapsuleBufferSaveData CaptureState()
	{
		CapsuleBufferSaveData data = new();
		if (DockedCapsule != null)
			data.BoxId = DockedCapsule.BoxId;

		return data;
	}

	public void RestoreState(CapsuleBufferSaveData data, IReadOnlyDictionary<uint, BoxBase> restoredBoxes)
	{
		if (data == null || restoredBoxes == null)
			return;

		if (data.BoxId != 0)
		{
			if (restoredBoxes.TryGetValue(data.BoxId, out var box))
				PutBox(box);
		}
	}
}
