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

	public bool CanRegister();

	public int GetQuantity(uint itemId);

	public int GetAcceptableQuantity(uint itemId, int requested);

	public bool CanAcceptStack(ItemStack stack);

	public int AddItem(uint itemId, int quantity);

	public int RemoveItem(uint itemId, int quantity);

	public bool AddStack(ItemStack stack);

	public bool RemoveStack(ItemStack stack);
}

// 실제 item 저장
[System.Serializable]
public class ItemStack
{
	private uint itemID;
	private int quantity = 0;
	private byte freshness = 100;
	private byte damage = 0;
	private ItemStatus status = ItemStatus.None;
	private OrderLine relatedOrderLine = null;
	private PackageOutboundStage outboundStage = PackageOutboundStage.None;

	// if <= 0 >>>>>> max
	private float maxStackSize;

	private ItemDatabase ItemDB => GameContext.Instance.ItemDB;
	private float ItemSize => ItemDB.GetItemSize(itemID);
	public float AvailableSpace => (maxStackSize - (ItemSize * quantity));

	public int AvailableAmount => (int)(AvailableSpace / ItemSize);

	public uint ItemID => itemID;
	public int Quantity => quantity;
	public byte Freshness => freshness;
	public byte Damage => damage;
	public ItemStatus Status => status;
	public OrderLine RelatedOrderLine => relatedOrderLine;
	public int? RelatedOrderId => relatedOrderLine?.ParentOrder?.OrderID;
	public PackageOutboundStage OutboundStage => outboundStage;
	public float Size => Quantity * ItemDB.GetItemSize(ItemID);
	public float StackSize => maxStackSize;

	public ItemStack(
		uint itemID,
		float maxStackSize,
		byte freshness = 100,
		byte damage = 0,
		ItemStatus status = ItemStatus.None,
		OrderLine relatedOrderLine = null,
		PackageOutboundStage outboundStage = PackageOutboundStage.None)
	{
		this.itemID = itemID;
		this.maxStackSize = maxStackSize;
		this.freshness = ClampPercent(freshness);
		this.damage = ClampPercent(damage);
		this.status = status;
		this.relatedOrderLine = relatedOrderLine;
		this.outboundStage = outboundStage;
	}

	private static byte ClampPercent(byte value) => (byte)Mathf.Clamp((int)value, 0, 100);

	public bool CanAddItem(int quantity) => maxStackSize <= 0 || maxStackSize - (ItemSize * (this.quantity + quantity)) >= 0;
	public bool HasStatus(ItemStatus target) => (status & target) == target;

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

	public void AddStatus(ItemStatus status)
	{
		this.status |= status;
	}

	public void RemoveStatus(ItemStatus status)
	{
		this.status &= ~status;
	}

	public void AssignRelatedOrderLine(OrderLine relatedOrderLine)
	{
		this.relatedOrderLine = relatedOrderLine;
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
			ReferenceEquals(relatedOrderLine, other.relatedOrderLine) &&
			outboundStage == other.outboundStage;
	}

	public bool CanMergeWith(ItemStack other)
	{
		return other != null &&
			HasMatchingIdentity(other) &&
			CanAddItem(other.Quantity);
	}

	// returns actual added amount
	public int AddItem(int amount)
	{
		int maxAddMount = AvailableAmount;
		amount = math.min(amount, maxAddMount);

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
		return new ItemStack(itemID, maxStackSize, freshness, damage, status, relatedOrderLine, outboundStage);
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

	public void ReportOutboundProgress(OrderManager orderManager, PackageOutboundStage targetStage)
	{
		if (orderManager == null || relatedOrderLine == null)
			return;

		if (targetStage <= outboundStage)
			return;

		for (PackageOutboundStage nextStage = outboundStage + 1; nextStage <= targetStage; ++nextStage)
		{
			switch (nextStage)
			{
				case PackageOutboundStage.WaitingForShipping:
					orderManager.ReportWaitingForShipping(relatedOrderLine, Quantity);
					break;

				case PackageOutboundStage.Shipping:
					orderManager.ReportShipping(relatedOrderLine, Quantity);
					break;

				case PackageOutboundStage.InDelivery:
					orderManager.ReportInDelivery(relatedOrderLine, Quantity);
					break;

				case PackageOutboundStage.Completed:
					orderManager.ReportCompleted(relatedOrderLine, Quantity);
					break;
			}
		}

		outboundStage = targetStage;
	}
}

[System.Flags]
public enum ItemStatus
{
	None = 0,
	Labeled = 1 << 0,
	Packed = 1 << 1,
}

public enum PackingType
{
	Box,
	PlasticBag,
}

public enum PackageOutboundStage
{
	None,
	WaitingForShipping,
	Shipping,
	InDelivery,
	Completed,
}

public static class PackingTypeExt
{
	public static float GetPackageSize(this PackingType type)
	{
		switch (type)
		{
			case PackingType.Box:
				return 50;
			case PackingType.PlasticBag:
				return 50;
			default:
				return 50;
		}
	}
}

public class ItemPackage : ItemStack
{
	private PackingType packingType;
	public PackingType PackingType => packingType;

	public ItemPackage(
		PackingType type,
		OrderLine order,
		uint itemID,
		int quantity,
		PackageOutboundStage outboundStage = PackageOutboundStage.None,
		byte freshness = 100,
		byte damage = 0,
		ItemStatus status = ItemStatus.None) : base(
			itemID,
			type.GetPackageSize(),
			freshness,
			damage,
			status | ItemStatus.Packed,
			order,
			outboundStage)
	{
		packingType = type;
		AddItem(quantity);
	}

	protected override ItemStack CreateEmptyLikeThis()
	{
		return new ItemPackage(packingType, RelatedOrderLine, ItemID, 0, OutboundStage, Freshness, Damage, Status);
	}
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
