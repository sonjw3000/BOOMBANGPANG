using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

// 아이템과 선반을 한번에 관리한다
// 아이템ID별로 아이템의 위치를 가진 딕셔너리가 존재함
// 
[System.Serializable]
public class ItemInventory
{
	[SerializeField] private List<IItemContainer> containers = new();
	private readonly Dictionary<uint, List<ItemLocation>> itemLocations = new();

	public IReadOnlyList<IItemContainer> Containers => containers;
	public IReadOnlyDictionary<uint, List<ItemLocation>> ItemLocations => itemLocations;

	// ---------------------------
	// 컨테이너 관련
	// ---------------------------
	// 컨테이너에 저장될 아이템의 종류를 업데이트한다
	public void OnContainerAdded(IItemContainer container)
	{
		containers.Add(container);
	}

	public void OnContainerRemoved(IItemContainer container)
	{
		containers.Remove(container);
	}

	public void AddItemLocation(uint itemID, IItemContainer container, int stackIndex)
	{
		if (itemLocations.ContainsKey(itemID) == false)
		{
			itemLocations[itemID] = new List<ItemLocation>();
		}

		var pos = container.PickingPosition;
		var locationList = itemLocations[itemID];
		var existingLocation = locationList.Find(loc => loc.Container == container);

		if (existingLocation.Container == null)
		{
			locationList.Add(new ItemLocation
			{
				Container = container,
				StackIndex = stackIndex,
				Quantity = 0
			});
		}
		else
		{
			existingLocation.StackIndex = stackIndex;
			existingLocation.Quantity = 0;
		}
	}

	// ---------------------------
	// 아이템 관련
	// ---------------------------
	// 컨테이너 내부 아이템 수량을 조절한다
	public void AdjustItemQuantity(uint itemID, IItemContainer container, int quantityDelta)
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

		if (itemLocations.TryGetValue(itemID, out var locationList))
		{
			int idx = locationList.FindIndex(loc => loc.Container == container);
			if (idx < 0)
				return;

			var location = locationList[idx];
			location.Quantity += quantityDelta;
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
