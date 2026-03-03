using UnityEngine;
using System.Collections.Generic;

// database of all item types in the game
[System.Serializable]
public class ItemDatabase : MonoBehaviour
{
	// 실제 모든 아이템과 플레이어가 저거 하는 아이템을 모두 분리해서 관리해야함
	//[SerializeField] private List<ItemDefinition> realItems = new();
	[SerializeField] private List<ItemCatalog> itemCatalogs = new();
	[SerializeField] private List<ItemDefinition> items = new();
	public IReadOnlyList<ItemDefinition> Items => items;

	[SerializeField] private HashSet<uint> orderedItems = new HashSet<uint>();
	public IReadOnlyCollection<uint> OrderedItems => orderedItems;

	private void OnValidate()
	{
		Dictionary<uint, ItemDefinition> itemIDMap = new();

		foreach (var catalog in itemCatalogs)
		{
			foreach (var item in catalog.itemDefinitions)
			{
				if (itemIDMap.ContainsKey(item.ItemID))
				{
					Debug.LogError($"Duplicate ItemID {item.ItemID} found in {catalog.name}. Each ItemID must be unique.");
				}
				else
				{
					itemIDMap[item.ItemID] = item;
				}
			}
		}

	}

	private void Awake()
	{
		foreach (var catalog in itemCatalogs)
		{
			items.AddRange(catalog.itemDefinitions);
		}
	}

	public bool GetItemData(uint itemID, out ItemDefinition data)
	{
		data = items.Find(item => item.ItemID == itemID);

		return data != null;
	}

	public float GetItemSize(uint itemID)
	{
		ItemDefinition data;
		if (GetItemData(itemID, out data))
		{
			return data.Size;
		}
		else
		{
			Debug.LogError($"ItemID {itemID} does not exist in ItemDB.");
			return 0;
		}
	}

	public uint GetRandomItemID()
	{
		return items[Random.Range(0, items.Count)].ItemID;
	}

	public void InsertOrderedItems(uint itemID)
	{
		orderedItems.Add(itemID);
	}
}
