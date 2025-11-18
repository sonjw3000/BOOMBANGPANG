using System.Collections.Generic;
using UnityEngine;

// 아이템과 선반을 한번에 관리한다
// 아이템ID별로 아이템의 위치를 가진 딕셔너리가 존재함
// 

public class ItemInventory
{
	private List<IItemContainer> containers = new();
	private readonly Dictionary<int, List<ItemLocation>> itemLocations = new();

	public IReadOnlyList<IItemContainer> Containers => containers;
	public IReadOnlyDictionary<int, List<ItemLocation>> ItemLocations => itemLocations;

	public void OnContainerAdded(IItemContainer container)
	{
		containers.Add(container);
	}

	public void OnContainerRemoved(IItemContainer container)
	{
		containers.Remove(container);
	}

	// ---------------------------
	// 컨테이너 관련
	// ---------------------------
	// 컨테이너에 저장될 아이템의 종류를 업데이트한다
	public void AddItemLocation(int itemID, IItemContainer container, int stackIndex)
	{
		if (itemLocations.ContainsKey(itemID) == false)
		{
			itemLocations[itemID] = new List<ItemLocation>();
		}

		var pos = container.Position;
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
	public void AdjustItemQuantity(int itemID, IItemContainer container, int quantityDelta)
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
}
