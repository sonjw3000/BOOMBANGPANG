using System.Collections.Generic;
using UnityEngine;
using static UnityEditor.Progress;


// item의 출입고 내역을 기록하는 장부
// 추후에 통계자료로 활용 가능

public class ItemLedger : MonoBehaviour
{
	private Dictionary<uint, int> itemTotals = new();
	private Dictionary<uint, int> itemReserveds = new();

	private List<uint> orderableItems = new();
	
	public IReadOnlyDictionary<uint, int> ItemTotals => itemTotals;
	public IReadOnlyDictionary<uint, int> ItemReserveds => itemReserveds;
	public IReadOnlyList<uint> OrderableItems => orderableItems;

	public int GetTotal(uint itemId) => itemTotals.GetValueOrDefault(itemId);
	public int GetReserved(uint itemId) => itemReserveds.GetValueOrDefault(itemId);
	public int GetAvailable(uint itemId) => GetTotal(itemId) - GetReserved(itemId);

	// 주문 취소/실패 등으로 예약 롤백
	public void ReleaseReserve(uint itemId, int quantity) => itemReserveds[itemId] = itemReserveds.GetValueOrDefault(itemId) - quantity;

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
		itemReserveds[itemId] = itemReserveds.GetValueOrDefault(itemId) + quantity;

		// reserved 음수처리
		if (itemReserveds[itemId] < 0)
		{
			Debug.LogError($"ItemLedger: Item ID {itemId} has negative reserved quantity {itemReserveds[itemId]}");
			itemReserveds[itemId] = 0;
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
		else
			ItemAdded(itemId, quantityDelta);
	}

	// 필수
	// 사용 전에 GetAvailable로 수량을 제한하여야 함
	public void OnItemReserved(uint itemId, int quantity)
	{
		itemReserveds[itemId] = itemReserveds.GetValueOrDefault(itemId) + quantity;

		// orderable이 아니라면 제거
		if (GetAvailable(itemId) <= 0)
		{
			orderableItems.Remove(itemId);
		}
	}
}

