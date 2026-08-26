using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

// box base를 보관하는 타일 단 하나

public partial class CapsuleBuffer :
	CapsuleDock,
	IItemContainer,
	IItemPickReservable
{
	private static readonly IReadOnlyList<ItemStack> EmptyStacks = Array.Empty<ItemStack>();
	private static readonly IReadOnlyDictionary<uint, int> EmptyItemTotals = new Dictionary<uint, int>();
	private readonly Dictionary<uint, int> itemsReservedPick = new();

	[SerializeField] private GameObject boxStackPos;

	public event Action<CapsuleBuffer, CargoCapsule> OnCapsuleUndocking;
	public event Action<CapsuleBuffer> OnCapsuleContentChanged;
	public event Action<IItemContainer, uint, int> OnItemReservedPickChanged;

	public override CapsuleDockState DockState => CapsuleDockState.Buffer;
	public override WorkerStatusTarget BuildingTarget => WorkerStatusTarget.CapsuleBuffer;
	public IReadOnlyList<ItemStack> Stacks => DockedCapsule != null ? DockedCapsule.Stacks : EmptyStacks;
	public IReadOnlyDictionary<uint, int> ItemTotals => DockedCapsule != null ? DockedCapsule.ItemTotals : EmptyItemTotals;
	public IReadOnlyDictionary<uint, int> ItemToBePicked => itemsReservedPick;
	public ItemTag ItemTags => DockedCapsule != null ? DockedCapsule.ItemTags : ItemTag.None;

	public bool CanReceiveCapsule() => CanPutBox();
	public bool CanProvideInboundItems() =>
		DockedCapsule?.LogisticsState == CapsuleLogisticsState.Inside &&
		IsCapsuleEmpty() == false;
	public bool CanDispatchToOutbound() => CanGetBox() && DockedCapsule != null && DockedCapsule.LogisticsState == CapsuleLogisticsState.OB;
	public bool CanRelocateEmptyCapsule() =>
		DockedCapsule?.LogisticsState == CapsuleLogisticsState.Empty &&
		IsCapsuleEmpty();
	public bool CanReceiveOutboundItems() =>
		DockedCapsule != null &&
		(DockedCapsule.LogisticsState == CapsuleLogisticsState.Empty ||
		 DockedCapsule.LogisticsState == CapsuleLogisticsState.Inside);
	public bool CanRegister() => DockedCapsule != null && DockedCapsule.CanRegister();
	public int GetQuantity(uint itemId) => DockedCapsule != null ? DockedCapsule.GetQuantity(itemId) : 0;
	public int GetPickableQuantity(uint itemId) => DockedCapsule != null ? DockedCapsule.GetQuantity(itemId) - itemsReservedPick.GetValueOrDefault(itemId) : 0;
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

	public int ReservePicking(uint itemId, int quantity)
	{
		if (DockedCapsule == null || quantity <= 0)
			return 0;

		int total = DockedCapsule.GetQuantity(itemId);
		int beforeReserved = itemsReservedPick.GetValueOrDefault(itemId);
		int reserved = Mathf.Clamp(quantity, 0, total - beforeReserved);
		if (reserved <= 0)
			return 0;

		itemsReservedPick[itemId] = beforeReserved + reserved;
		OnItemReservedPickChanged?.Invoke(this, itemId, reserved);
		return reserved;
	}

	public int ConsumeReservedPick(uint itemId, int quantity)
	{
		if (quantity <= 0)
			return 0;

		int reserved = itemsReservedPick.GetValueOrDefault(itemId);
		if (reserved <= 0)
		{
			Debug.LogWarning($"[CapsuleBuffer] Tried to consume unreserved pick. buffer={name}, item={itemId}, quantity={quantity}");
			return 0;
		}

		int consumed = Mathf.Min(quantity, reserved);
		int remaining = reserved - consumed;
		if (remaining > 0)
			itemsReservedPick[itemId] = remaining;
		else
			itemsReservedPick.Remove(itemId);

		OnItemReservedPickChanged?.Invoke(this, itemId, -consumed);

		if (consumed != quantity)
			Debug.LogWarning($"[CapsuleBuffer] Reserved pick was smaller than removed quantity. buffer={name}, item={itemId}, requested={quantity}, consumed={consumed}");

		return consumed;
	}

	public int ReleaseReservedPick(uint itemId, int quantity)
	{
		if (quantity <= 0)
			return 0;

		int reserved = itemsReservedPick.GetValueOrDefault(itemId);
		if (reserved <= 0)
			return 0;

		int released = Mathf.Min(quantity, reserved);
		int remaining = reserved - released;
		if (remaining > 0)
			itemsReservedPick[itemId] = remaining;
		else
			itemsReservedPick.Remove(itemId);

		OnItemReservedPickChanged?.Invoke(this, itemId, -released);

		return released;
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
		ClearPickReservations();
		base.OnDestroyedBy(in ctx);
	}

	protected override void OnBeforeCapsuleUndocked(CargoCapsule capsule)
	{
		ClearPickReservations();
		OnCapsuleUndocking?.Invoke(this, capsule);
	}

	protected override void OnDockedCapsuleChanged()
	{
		ClearPickReservations();
	}

	protected override void OnCapsuleQuantityChanged()
	{
		if (DockedCapsule == null)
			return;

		OnCapsuleContentChanged?.Invoke(this);
	}

	private void ClearPickReservations()
	{
		if (itemsReservedPick.Count <= 0)
			return;

		List<uint> itemIds = new(itemsReservedPick.Keys);
		for (int i = 0; i < itemIds.Count; ++i)
		{
			uint itemId = itemIds[i];
			int reserved = itemsReservedPick.GetValueOrDefault(itemId);
			if (reserved > 0)
				OnItemReservedPickChanged?.Invoke(this, itemId, -reserved);
		}

		itemsReservedPick.Clear();
	}

}
