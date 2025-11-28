using System.Collections.Generic;
using Unity.Mathematics;
using Unity.VisualScripting;
using UnityEngine;

// 아이템과 선반을 한번에 관리한다
// 아이템ID별로 아이템의 위치를 가진 딕셔너리가 존재함
// 
[System.Serializable]
public class ItemInventory
{
	// shelf, bin 등 아이템 컨테이너 리스트
	[SerializeField] private List<ShelfBase> containers = new();

	// 아이템 ID별 아이템의 위치 리스트
	private readonly Dictionary<uint, List<ItemLocation>> itemLocations = new();

	public IReadOnlyList<ShelfBase> Containers => containers;
	public IReadOnlyDictionary<uint, List<ItemLocation>> ItemLocations => itemLocations;

	// ---------------------------
	// 컨테이너 관련
	// ---------------------------
	// 컨테이너에 저장될 아이템의 종류를 업데이트한다
	public void OnContainerAdded(ShelfBase container)
	{
		containers.Add(container);
	}

	public void OnContainerRemoved(ShelfBase container)
	{
		containers.Remove(container);
	}

	public void AddItemLocation(uint itemID, ShelfBase container, int stackIndex)
	{
		// add itemID to container's item stack
		container.RegisterItem(itemID);

		if (itemLocations.ContainsKey(itemID) == false)
		{
			itemLocations[itemID] = new List<ItemLocation>();
		}

		itemLocations[itemID].Add(new ItemLocation
		{
			Container = container,
			StackIndex = stackIndex,
			Quantity = 0
		});
	}

	// ---------------------------
	// 아이템 관련
	// ---------------------------
	// 컨테이너 내부 아이템 수량을 조절한다
	public void AdjustItemQuantity(uint itemID, ShelfBase container, int quantityDelta)
	{
#if UNITY_EDITOR
		// check itemID existence
		ItemData data;
		var res = GameContext.Instance.ItemDB.GetItemData(itemID, out data);

		if (res == false)
		{
			Debug.LogError($"ItemID {itemID} does not exist in ItemDB.");
			return;
		}
#endif

		// adjust quantity

		if (container.Items.ContainsKey(itemID))
		{
			container.Items[itemID].Quantity += quantityDelta;
		}
		else
		{
			// 아이템ID가 존재하지 않음
			Debug.Log($"ItemID {itemID} not found in inventory.");
			Debug.Log("Register First! (AddItemLocation)");
		}
	}

	public bool GetItemLocations(uint itemID, out List<ItemLocation> locations)
	{
		return itemLocations.TryGetValue(itemID, out locations);
	}

	public bool GetClosestItemLocation(uint itemID, int3 from, out int3 location)
	{
		location = default;
		if (itemLocations.ContainsKey(itemID) == false) return false;

		var locations = itemLocations[itemID];

		//float minDist = float.MaxValue;
		//float3 floatFrom = (float3)from;
		//foreach (var loc in locations)
		//{
		//	var containerPos = loc.Container.PickingPosition;
		//	float distPow = 
		//	if (dist < minDist)
		//	{
		//		minDist = dist;
		//		location = loc;
		//	}
		//}

		// 임시로 그냥 첫번째 위치 반환
		location = locations[0].Container.PickingPosition;

		return true;
	}
}
