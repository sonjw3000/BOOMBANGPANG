using UnityEngine;
using System.Collections.Generic;

// database of all item types in the game
[System.Serializable]
public class ItemDatabase
{
	// 실제 모든 아이템과 플레이어가 저거 하는 아이템을 모두 분리해서 관리해야함
	//[SerializeField] private List<ItemData> realItems = new();
	[SerializeField] private List<ItemData> items = new();
	public IReadOnlyList<ItemData> Items => items;

	[SerializeField] private HashSet<uint> orderedItems = new HashSet<uint>();
	public IReadOnlyCollection<uint> OrderedItems => orderedItems;


	public bool GetItemData(uint itemID, out ItemData data)
	{
		data = items.Find(item => item.ItemID == itemID);

		return data != null;
	}

	public uint GetRandomItemID()
	{
		return items[Random.Range(0, items.Count -1)].ItemID;
	}

	public void InsertOrderedItems(uint itemID)
	{
		orderedItems.Add(itemID);
	}
}
