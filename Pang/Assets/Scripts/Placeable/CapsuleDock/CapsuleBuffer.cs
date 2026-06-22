using System;
using Unity.Mathematics;
using UnityEngine;

// box base를 보관하는 타일 단 하나

public enum CapsuleBufferState
{
	IBOnly,
	OBOnly,
	Shared,
}

public partial class CapsuleBuffer : 
	CapsuleDock
{
	[SerializeField] private GameObject boxStackPos;
	[SerializeField] private CapsuleBufferState bufferState = CapsuleBufferState.Shared;

	public event Action<CapsuleBuffer> OnCapsuleDocked;
	public event Action<CapsuleBuffer> OnCapsuleUndocked;
	public event Action<CapsuleBuffer> OnCapsuleContentChanged;
	public event Action<CapsuleBuffer> OnBufferStateChanged;

	public CapsuleBufferState BufferState => bufferState;
	public override WorkerStatusTarget BuildingTarget => WorkerStatusTarget.CapsuleBuffer;

	public bool CanReceiveFromInbound() => bufferState != CapsuleBufferState.OBOnly && CanPutBox();
	public bool CanDispatchToOutbound() => bufferState != CapsuleBufferState.IBOnly && CanGetBox() && IsCapsuleEmpty() == false;

	public void SetBufferState(CapsuleBufferState newState)
	{
		if (bufferState == newState)
			return;

		bufferState = newState;
		OnBufferStateChanged?.Invoke(this);
	}

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

	protected override void OnDockedCapsuleChanged()
	{
		if (HasCapsule)
			OnCapsuleDocked?.Invoke(this);
		else
			OnCapsuleUndocked?.Invoke(this);
	}

	protected override void OnCapsuleQuantityChanged()
	{
		if (DockedCapsule == null)
			return;

		OnCapsuleContentChanged?.Invoke(this);
	}

}
