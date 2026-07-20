using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

// 아이템 보관함
// 선반, 상자, 기타등등이 이를 사용
public interface IItemContainer
{
	public IReadOnlyList<ItemStack> Stacks { get; }

	public IReadOnlyDictionary<uint, int> ItemTotals { get; }

	public float TotalSize { get; }
	public float MaxSize { get; }
	public float FilledPercent => MaxSize <= 0 ? 0 : (TotalSize / MaxSize) * 100.0f;
	public ItemTag ItemTags { get; }

	public bool CanRegister();

	public int GetQuantity(uint itemId);

	public int GetAcceptableQuantity(uint itemId, int requested);

	public bool CanAcceptStack(ItemStack stack);

	public int AddItem(uint itemId, int quantity);

	public int RemoveItem(uint itemId, int quantity);

	public bool AddStack(ItemStack stack);

	public bool RemoveStack(ItemStack stack);

	public bool TryRemoveFromStack(ItemStack stack, int quantity, out ItemStack removedStack);
}

public interface IItemPickReservable
{
	public IReadOnlyDictionary<uint, int> ItemToBePicked { get; }

	public event System.Action<IItemContainer, uint, int> OnItemReservedPickChanged;

	public int GetPickableQuantity(uint itemId);

	public int ReservePicking(uint itemId, int quantity);

	public int ReleaseReservedPick(uint itemId, int quantity);

	public int ConsumeReservedPick(uint itemId, int quantity);
}

// 실제 item 저장
[System.Serializable]
public class ItemStack
{
	private const byte DefaultFreshnessValue = 100;
	private const byte DefaultDamageValue = 0;
	private static readonly Stack<ItemStack> pool = new();

	private uint itemID;
	private int quantity = 0;
	private byte freshness = DefaultFreshnessValue;
	private byte damage = DefaultDamageValue;
	private ItemStatus status = ItemStatus.None;
	private ItemQuality quality = ItemQuality.None;
	private PackageOutboundStage outboundStage = PackageOutboundStage.None;

	public uint ItemID => itemID;
	public int Quantity => quantity;
	public byte Freshness => freshness;
	public byte Damage => damage;
	public ItemStatus Status => status;
	public ItemQuality Quality => quality;
	public PackageOutboundStage OutboundStage => outboundStage;
	public float Size => GameContext.Instance.ItemDB.GetItemSize(ItemID) * Quantity;
	public bool IsDefaultIdentity =>
		freshness == DefaultFreshnessValue &&
		damage == DefaultDamageValue &&
		status == ItemStatus.None &&
		quality == ItemQuality.None &&
		outboundStage == PackageOutboundStage.None;

	public ItemStack(
		uint itemID,
		byte freshness = 100,
		byte damage = 0,
		ItemStatus status = ItemStatus.None,
		PackageOutboundStage outboundStage = PackageOutboundStage.None,
		ItemQuality quality = ItemQuality.None)
	{
		Initialize(itemID, freshness, damage, status, outboundStage, quality);
	}

	private static byte ClampPercent(byte value) => (byte)Mathf.Clamp((int)value, 0, 100);

	public static ItemStack Rent(
		uint itemID,
		byte freshness = 100,
		byte damage = 0,
		ItemStatus status = ItemStatus.None,
		PackageOutboundStage outboundStage = PackageOutboundStage.None,
		ItemQuality quality = ItemQuality.None)
	{
		if (pool.Count > 0)
		{
			ItemStack stack = pool.Pop();
			stack.Initialize(itemID, freshness, damage, status, outboundStage, quality);
			return stack;
		}

		return new ItemStack(itemID, freshness, damage, status, outboundStage, quality);
	}

	public static ItemStack RentDefault(uint itemID)
	{
		return Rent(itemID, DefaultFreshnessValue, DefaultDamageValue, ItemStatus.None, PackageOutboundStage.None);
	}

	public bool HasItemID(uint itemID) => this.itemID == itemID;
	public bool HasStatus(ItemStatus target) => status == target;
	public bool HasQuality(ItemQuality target) => target != ItemQuality.None && (quality & target) == target;

	public void SetFreshness(byte freshness)
	{
		this.freshness = ClampPercent(freshness);
	}

	public void SetDamage(byte damage)
	{
		this.damage = ClampPercent(damage);
	}

	public void SetStatus(ItemStatus status)
	{
		this.status = status;
	}

	public void AddQuality(ItemQuality quality)
	{
		this.quality |= quality;
	}

	public void SetOutboundStage(PackageOutboundStage outboundStage)
	{
		this.outboundStage = outboundStage;
	}

	public bool HasMatchingIdentity(ItemStack other)
	{
		if (other == null)
			return false;

		return itemID == other.itemID &&
			freshness == other.freshness &&
			damage == other.damage &&
			status == other.status &&
			quality == other.quality &&
			outboundStage == other.outboundStage;
	}

	public bool CanMergeWith(ItemStack other)
	{
		return other != null && HasMatchingIdentity(other);
	}

	// returns actual added amount
	public int AddItem(int amount)
	{
		if (amount <= 0)
			return 0;

		quantity += amount;

		if (quantity < 0)
			Debug.LogError("Why it's under 0");

		return amount;
	}

	// returns actual removed amount
	// picking task가 아이템을 실제로 훑고 지나갔을 때
	public int RemoveItem(int amount)
	{
		amount = math.min(amount, quantity);

		quantity -= amount;

		if (quantity < 0)
			Debug.LogError("Why it's under 0");

		return amount;
	}

	protected virtual ItemStack CreateEmptyLikeThis()
	{
		return Rent(itemID, freshness, damage, status, outboundStage, quality);
	}

	public virtual ItemStack CreateTransferStack(int amount)
	{
		if (amount <= 0)
			return null;

		ItemStack stack = CreateEmptyLikeThis();
		stack.AddItem(amount);
		return stack;
	}

	public ItemStack CloneWithQuantity(int quantity)
	{
		return CreateTransferStack(quantity);
	}

	public ItemStack Split(int amount)
	{
		if (amount <= 0)
			return null;

		int removed = RemoveItem(amount);
		return removed > 0 ? CreateTransferStack(removed) : null;
	}

	public bool TryMergeFrom(ItemStack other)
	{
		if (CanMergeWith(other) == false)
			return false;

		int added = AddItem(other.Quantity);
		if (added != other.Quantity)
			return false;

		other.RemoveItem(added);
		return true;
	}

	public virtual void Recycle()
	{
		ResetState();

		if (GetType() == typeof(ItemStack))
			pool.Push(this);
	}

	private void Initialize(
		uint itemID,
		byte freshness,
		byte damage,
		ItemStatus status,
		PackageOutboundStage outboundStage,
		ItemQuality quality)
	{
		this.itemID = itemID;
		quantity = 0;
		this.freshness = ClampPercent(freshness);
		this.damage = ClampPercent(damage);
		this.status = status;
		this.outboundStage = outboundStage;
		this.quality = quality;
	}

	private void ResetState()
	{
		itemID = 0;
		quantity = 0;
		freshness = DefaultFreshnessValue;
		damage = DefaultDamageValue;
		status = ItemStatus.None;
		quality = ItemQuality.None;
		outboundStage = PackageOutboundStage.None;
	}
}

public enum ItemStatus
{
	NotDefined = -1,
	None = 0,
	Labeled = 1,
	Packed = 2,
}

[System.Flags]
public enum ItemQuality
{
	None = 0,
	Waste = 1 << 0,
}

public enum PackageOutboundStage
{
	None,
	WaitingForShipping,
	Shipping,
	InDelivery,
	Completed,
}

// 특정 item이 위치한 정보를 간편히 표현한 자료구조
// 여기서 건들면 연동되게 해야함
// task는 해당 자료구조를 참조한다
// 사용의 편의성을 위해 item add, remove는 여기에도 두지만 하는 행동은 동일함
//public class ItemLocation
//{
//	private ShelfBase container;
//	private uint itemID;
//	//private int quantity;
//	//private int tobeQuantity;

//	public ItemLocation(ShelfBase shelf, uint itemID)
//	{
//		container = shelf;
//		this.itemID = itemID;
//	}

//	public ShelfBase Container => container;
//	private ItemStack itemStack => container.Stacks[itemID];
//	public int Quantity => itemStack.Quantity;
//	public int TobeQuantity => itemStack.TobeQuantity;
//	public uint ItemID => itemID;

//	// storing task가 아이템을 저장할 때
//	public int AddItem(int amount)
//	{
//		return itemStack.AddItem(amount);
//	}
	
//	// picking task가 아이템을 실제로 훑고 지나갔을 때
//	public int RemoveItem(int amount)
//	{
//		return itemStack.RemoveItem(amount);
//	}

//	// picking task가 아이템을 예약할 때
//	public int ReservePicking(int amount)
//	{
//		return itemStack.ReservePicking(amount);
//	}

//}
