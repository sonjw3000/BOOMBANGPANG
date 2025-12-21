using System.Collections.Generic;
using Unity.Mathematics;

// 아이템 보관함
// 선반, 상자, 기타등등이 이를 사용
public interface IItemContainer
{
	public IReadOnlyList<ItemStack> Stacks { get; }

	public IReadOnlyDictionary<uint, int> ItemTotals { get; }

	public bool CanRegister();

	public int AddItem(uint itemId, int quantity);

	public int RemoveItem(uint itemId, int quantity);
}

// 실제 item 저장
[System.Serializable]
public class ItemStack
{
	private uint itemID;
	private int quantity;
	private int tobeQuantity;

	// if <= 0 >>>>>> max
	private float maxStackSize;

	private ItemDatabase itemDB => GameContext.Instance.ItemDB;
	private float itemSize => itemDB.GetItemSize(itemID);
	public float AvailableSpace => (maxStackSize - (itemSize * quantity));

	public int AvailableAmount => (int)(AvailableSpace / itemSize);

	public uint ItemID => itemID;
	public int Quantity => quantity;
	public int TobeQuantity => tobeQuantity;

	public ItemStack(uint itemID, float maxStackSize)
	{
		this.itemID = itemID;
		this.maxStackSize = maxStackSize;
	}

	public bool CanAddItem(int quantity) => maxStackSize <= 0 || maxStackSize - (itemSize * (this.quantity + quantity)) >= 0;

	// returns actual added amount
	public int AddItem(int amount)
	{
		int maxAddMount = AvailableAmount;
		amount = math.min(amount, maxAddMount);

		quantity += amount;
		tobeQuantity += amount;

		return amount;
	}

	// returns actual removed amount
	// picking task가 아이템을 실제로 훑고 지나갔을 때
	public int RemoveItem(int amount)
	{
		amount = math.min(amount, quantity);

		quantity -= amount;

		return amount;
	}

	// picking task가 아이템을 예약할 때
	public int ReservePicking(int amount)
	{
		amount = math.min(amount, tobeQuantity);

		tobeQuantity -= amount;

		return amount;
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
