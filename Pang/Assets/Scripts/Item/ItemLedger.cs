using System;
using System.Collections.Generic;
using UnityEngine;


// item의 출입고 내역을 기록하는 장부
// 추후에 통계자료로 활용 가능

public partial class ItemLedger : MonoBehaviour
{
	private readonly Dictionary<uint, int> itemTotals = new();
	private readonly Dictionary<uint, int> reservedItems = new();

	private readonly List<uint> orderableItems = new();
	
	public IReadOnlyDictionary<uint, int> ItemTotals => itemTotals;
	public IReadOnlyDictionary<uint, int> ReservedItems => reservedItems;
	public IReadOnlyList<uint> OrderableItems => orderableItems;
	public event Action OnInventoryChanged;

	public int GetTotal(uint itemId) => itemTotals.GetValueOrDefault(itemId);
	public int GetReserved(uint itemId) => reservedItems.GetValueOrDefault(itemId);
	public int GetAvailable(uint itemId) => GetTotal(itemId) - GetReserved(itemId);

	// 주문 취소/실패 등으로 예약 롤백
	public void ReleaseReserve(uint itemId, int quantity)
	{
		reservedItems[itemId] = reservedItems.GetValueOrDefault(itemId) - quantity;
		OnInventoryChanged?.Invoke();
	}

	private void ItemAdded(uint itemId, int quantity)
	{
		// item이 orderable이 되었다면
		if (GetAvailable(itemId) == quantity)
		{
			orderableItems.Add(itemId);
		}

	}

	private void ItemRemoved(uint itemId, int quantity)
	{
		// 음수처리
		if (itemTotals[itemId] < 0)
		{
			Debug.LogError($"ItemLedger: Item ID {itemId} has negative total quantity {itemTotals[itemId]}");
			itemTotals[itemId] = 0;
		}

		// reserved 조절
		reservedItems[itemId] = reservedItems.GetValueOrDefault(itemId) + quantity;

		// reserved 음수처리
		if (reservedItems[itemId] < 0)
		{
			Debug.LogError($"ItemLedger: Item ID {itemId} has negative reserved quantity {reservedItems[itemId]}");
			reservedItems[itemId] = 0;
		}

		// item이 사라졌다면
		if (itemTotals[itemId] == 0)
		{
			itemTotals.Remove(itemId);
		}
	}

	public void OnItemQuantityChanged(uint itemId, int quantityDelta)
	{
		// adjust total quantity
		itemTotals[itemId] = itemTotals.GetValueOrDefault(itemId) + quantityDelta;

		if (quantityDelta < 0)
			ItemRemoved(itemId, quantityDelta);
		else if (quantityDelta == 0)
		{
			Debug.LogError("Why this is Zero?? lets track");
		}
		else
			ItemAdded(itemId, quantityDelta);

		OnInventoryChanged?.Invoke();
	}

	// 필수
	// 사용 전에 GetAvailable로 수량을 제한하여야 함
	public void OnItemReserved(uint itemId, int quantity)
	{
		reservedItems[itemId] = reservedItems.GetValueOrDefault(itemId) + quantity;

		// orderable이 아니라면 제거
		if (GetAvailable(itemId) <= 0)
		{
			orderableItems.Remove(itemId);
		}

		OnInventoryChanged?.Invoke();
	}

}
