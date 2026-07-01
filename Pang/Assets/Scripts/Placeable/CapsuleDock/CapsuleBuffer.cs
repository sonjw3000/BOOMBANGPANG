using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Serialization;

// box base를 보관하는 타일 단 하나

public partial class CapsuleBuffer :
	CapsuleDock,
	IItemContainer
{
	private static readonly IReadOnlyList<ItemStack> EmptyStacks = Array.Empty<ItemStack>();
	private static readonly IReadOnlyDictionary<uint, int> EmptyItemTotals = new Dictionary<uint, int>();

	[SerializeField] private GameObject boxStackPos;
	[FormerlySerializedAs("bufferState")]
	[SerializeField] private CapsuleDockState dockState = CapsuleDockState.Empty;

	public event Action<CapsuleBuffer> OnCapsuleDocked;
	public event Action<CapsuleBuffer, CargoCapsule> OnCapsuleUndocking;
	public event Action<CapsuleBuffer> OnCapsuleUndocked;
	public event Action<CapsuleBuffer> OnCapsuleContentChanged;
	public event Action<CapsuleBuffer> OnDockStateChanged;

	public override CapsuleDockState DockState => dockState;
	public override WorkerStatusTarget BuildingTarget => WorkerStatusTarget.CapsuleBuffer;
	public IReadOnlyList<ItemStack> Stacks => DockedCapsule != null ? DockedCapsule.Stacks : EmptyStacks;
	public IReadOnlyDictionary<uint, int> ItemTotals => DockedCapsule != null ? DockedCapsule.ItemTotals : EmptyItemTotals;
	public ItemTag ItemTags => DockedCapsule != null ? DockedCapsule.ItemTags : ItemTag.None;

	public bool CanReceiveFromInbound() => dockState == CapsuleDockState.IB && CanPutBox();
	public bool CanProvideInboundItems() =>
		dockState == CapsuleDockState.IB &&
		DockedCapsule?.LogisticsState == CapsuleLogisticsState.IB &&
		IsCapsuleEmpty() == false;
	public bool CanDispatchToOutbound() => CanGetBox() && DockedCapsule != null && DockedCapsule.LogisticsState == CapsuleLogisticsState.OB;
	public bool CanRelocateEmptyCapsuleFrom(CapsuleDockState requiredState) =>
		dockState == requiredState &&
		DockedCapsule?.LogisticsState == CapsuleLogisticsState.Empty &&
		IsCapsuleEmpty();
	public bool CanReceiveOutboundItems() =>
		dockState == CapsuleDockState.OBStandby &&
		DockedCapsule != null &&
		DockedCapsule.LogisticsState == CapsuleLogisticsState.OBStandby;
	public bool CanRegister() => DockedCapsule != null && DockedCapsule.CanRegister();
	public int GetQuantity(uint itemId) => DockedCapsule != null ? DockedCapsule.GetQuantity(itemId) : 0;
	public int GetAcceptableQuantity(uint itemId, int requested) => DockedCapsule != null ? DockedCapsule.GetAcceptableQuantity(itemId, requested) : 0;
	public bool CanAcceptStack(ItemStack stack) => DockedCapsule != null && DockedCapsule.CanAcceptStack(stack);
	public int AddItem(uint itemId, int quantity) => DockedCapsule != null ? DockedCapsule.AddItem(itemId, quantity) : 0;
	public int RemoveItem(uint itemId, int quantity) => DockedCapsule != null ? DockedCapsule.RemoveItem(itemId, quantity) : 0;
	public bool AddStack(ItemStack stack) => DockedCapsule != null && DockedCapsule.AddStack(stack);
	public bool RemoveStack(ItemStack stack) => DockedCapsule != null && DockedCapsule.RemoveStack(stack);
	public bool TryRemoveFromStack(ItemStack stack, int quantity, out ItemStack removedStack)
	{
		removedStack = null;
		return DockedCapsule != null && DockedCapsule.TryRemoveFromStack(stack, quantity, out removedStack);
	}

	public void SetDockState(CapsuleDockState newState)
	{
		if (dockState == newState)
			return;

		dockState = newState;
		OnDockStateChanged?.Invoke(this);
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

	protected override void OnBeforeCapsuleUndocked(CargoCapsule capsule)
	{
		OnCapsuleUndocking?.Invoke(this, capsule);
	}

	protected override void OnCapsuleQuantityChanged()
	{
		if (DockedCapsule == null)
			return;

		OnCapsuleContentChanged?.Invoke(this);
	}

}
