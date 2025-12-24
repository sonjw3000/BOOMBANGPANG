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
	
	public IReadOnlyList<uint> OrderableItems => orderableItems;

	public int GetTotal(uint itemID) => itemTotals.GetValueOrDefault(itemID);
	public int GetReserved(uint itemID) => itemReserveds.GetValueOrDefault(itemID);
	public int GetAvailable(uint itemID) => GetTotal(itemID) - GetReserved(itemID);

	// 주문 취소/실패 등으로 예약 롤백
	public void ReleaseReserve(uint itemID, int quantity) => itemReserveds[itemID] = itemReserveds.GetValueOrDefault(itemID) - quantity;


	// 출고 확정 배송 시작
	public void OnCommitShip(uint itemID, int quantity)
	{
		itemTotals[itemID] = itemTotals.GetValueOrDefault(itemID) - quantity;
		itemReserveds[itemID] = itemReserveds.GetValueOrDefault(itemID) - quantity;
	} 

	public void OnItemQuantityChanged(uint itemId, int quantityDelta)
	{
		int befOrderable = GetAvailable(itemId);

		itemTotals[itemId] = itemTotals.GetValueOrDefault(itemId) + quantityDelta;

		// 음수 방지
		if (itemTotals[itemId] < 0)
		{
			Debug.LogError($"ItemLedger: Item ID {itemId} has negative total quantity {itemTotals[itemId]}");
			itemTotals[itemId] = 0;
		}

		// 더이상 orderable이 아니라면
		if (GetAvailable(itemId) <= 0)
		{
			orderableItems.Remove(itemId);
		}
		else if (befOrderable <= 0)
		//else if (orderableItems.Contains(itemId) == false)
		{
			// 방금의 행동으로 orderable이 되었다면
			orderableItems.Add(itemId);
		}

		// item이 사라졌다면
		if (itemTotals[itemId] == 0)
		{
			itemTotals.Remove(itemId);
		}
	}

	// 필수
	// 사용 전에 GetAvailable로 수량을 제한하여야 함
	public void OnItemReserved(uint itemId, int quantity)
	{
		itemReserveds[itemId] = itemReserveds.GetValueOrDefault(itemId) + quantity;

		if (GetAvailable(itemId) <= 0)
		{
			orderableItems.Remove(itemId);
		}
	}
}

