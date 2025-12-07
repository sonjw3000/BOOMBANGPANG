using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

public interface IItemContainer
{
	public int StackCount { get; }
	public float StackCapacity { get; }
	public int3 PickingPosition { get; }
	public IReadOnlyDictionary<uint, ItemStack> Items { get; }

	public bool HasSpace();

	public void RegisterItem(uint itemId);

	public void RemoveItem(uint itemId);
}

// 실제 item 저장
[System.Serializable]
public class ItemStack
{
	private uint itemID;
	private int quantity;
	private int tobeQuantity;

	public uint ItemID => itemID;
	public int Quantity => quantity;
	public int TobeQuantity => tobeQuantity;

	public ItemStack(uint itemID)
	{
		this.itemID = itemID;
	}

	public void AddItem(int amount)
	{
		//itemStack.
		quantity += amount;
		tobeQuantity += amount;
	}

	// picking task가 아이템을 실제로 훑고 지나갔을 때
	public void RemoveItem(int amount)
	{
		Debug.Log($"Items Removed!\nID: {itemID}, quantity: {quantity}, amount: {amount}");
		quantity -= amount;
	}

	// picking task가 아이템을 예약할 때
	public void ReservePicking(int amount)
	{
		tobeQuantity -= amount;
	}
}

// 특정 item이 위치한 정보를 간편히 표현한 자료구조
// 여기서 건들면 연동되게 해야함
public class ItemLocation
{
	private ShelfBase container;
	private uint itemID;
	private int stackIndex;
	//private int quantity;
	//private int tobeQuantity;

	public ItemLocation(ShelfBase shelf, uint itemID, int stackIndex)
	{
		container = shelf;
		this.itemID = itemID;
		this.stackIndex = stackIndex;
	}

	public ShelfBase Container => container;
	private ItemStack itemStack => container.Items[itemID];
	public int Quantity => itemStack.Quantity;
	public int TobeQuantity => itemStack.TobeQuantity;


	// storing task가 아이템을 저장할 때
	public void AddItem(int amount)
	{
		itemStack.AddItem(amount);
	}
	
	// picking task가 아이템을 실제로 훑고 지나갔을 때
	public void RemoveItem(int amount)
	{
		itemStack.RemoveItem(amount);
	}

	// picking task가 아이템을 예약할 때
	public void ReservePicking(int amount)
	{
		itemStack.ReservePicking(amount);
	}

}
