using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

// box base를 보관하는 타일 단 하나

public enum CapsuleBufferState
{
	IBOnly,
	OBOnly,
	Empty,
}

public partial class CapsuleBuffer :
	CapsuleDock,
	IItemContainer
{
	private static readonly IReadOnlyList<ItemStack> EmptyStacks = Array.Empty<ItemStack>();
	private static readonly IReadOnlyDictionary<uint, int> EmptyItemTotals = new Dictionary<uint, int>();

	[SerializeField] private GameObject boxStackPos;
	[SerializeField] private CapsuleBufferState bufferState = CapsuleBufferState.Empty;

	public event Action<CapsuleBuffer> OnCapsuleDocked;
	public event Action<CapsuleBuffer> OnCapsuleUndocked;
	public event Action<CapsuleBuffer> OnCapsuleContentChanged;
	public event Action<CapsuleBuffer> OnBufferStateChanged;

	public CapsuleBufferState BufferState => bufferState;
	public override WorkerStatusTarget BuildingTarget => WorkerStatusTarget.CapsuleBuffer;
	public IReadOnlyList<ItemStack> Stacks => DockedCapsule != null ? DockedCapsule.Stacks : EmptyStacks;
	public IReadOnlyDictionary<uint, int> ItemTotals => DockedCapsule != null ? DockedCapsule.ItemTotals : EmptyItemTotals;
	public ItemTag ItemTags => DockedCapsule != null ? DockedCapsule.ItemTags : ItemTag.None;

	public bool CanReceiveFromInbound() => bufferState == CapsuleBufferState.IBOnly && CanPutBox();
	public bool CanDispatchToOutbound() => bufferState == CapsuleBufferState.OBOnly && CanGetBox() && IsCapsuleEmpty() == false;
	public bool CanRegister() => DockedCapsule != null && DockedCapsule.CanRegister();
	public int GetQuantity(uint itemId) => DockedCapsule != null ? DockedCapsule.GetQuantity(itemId) : 0;
	public int GetAcceptableQuantity(uint itemId, int requested) => DockedCapsule != null ? DockedCapsule.GetAcceptableQuantity(itemId, requested) : 0;
	public bool CanAcceptStack(ItemStack stack) => DockedCapsule != null && DockedCapsule.CanAcceptStack(stack);
	public int AddItem(uint itemId, int quantity) => DockedCapsule != null ? DockedCapsule.AddItem(itemId, quantity) : 0;
	public int RemoveItem(uint itemId, int quantity) => DockedCapsule != null ? DockedCapsule.RemoveItem(itemId, quantity) : 0;
	public bool AddStack(ItemStack stack) => DockedCapsule != null && DockedCapsule.AddStack(stack);
	public bool RemoveStack(ItemStack stack) => DockedCapsule != null && DockedCapsule.RemoveStack(stack);

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
