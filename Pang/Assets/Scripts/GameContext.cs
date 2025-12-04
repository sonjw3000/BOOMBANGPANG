using UnityEngine;

// 이것만은 꼭 지키자
// GameContext는 데이터만 가진다
// 로직을 가져선 안된다

public class GameContext : MonoBehaviour
{
	private static GameContext instance;
	public static GameContext Instance
	{
		get
		{
			if (instance == null)
			{
				Debug.LogError("GameGlobalContext is NOT initialized!");
				return null;
			}
			return instance;
		}
	}

	[SerializeField] private ItemDatabase itemDB;
	[SerializeField] private ItemInventory itemInventoryData;

	public ItemDatabase ItemDB => itemDB;
	public ItemInventory ItemInventoryData => itemInventoryData;

	private void Awake()
	{
		Debug.Log("GameGlobalContext Online!");
		if (instance != null && instance != this)
		{
			Destroy(this);
			Debug.Log("WARNNING!! GameGlobalContext Duplicated");
			return;
		}

		instance = this;
		DontDestroyOnLoad(gameObject);
	}

	public void TestStoreItem()
	{
		if (itemInventoryData.Containers.Count <= 0)
		{
			Debug.Log("No Item Container Found");
			return;
		}

		ItemInventoryData.AddItemLocation(123333, itemInventoryData.Containers[0], 0);
		ItemInventoryData.AddItemLocation(123123, itemInventoryData.Containers[1], 0);
		ItemInventoryData.AddItemLocation(14412, itemInventoryData.Containers[2], 0);

		itemDB.InsertOrderedItems(123333);
		itemDB.InsertOrderedItems(123123);
		itemDB.InsertOrderedItems(14412);

		Debug.Log("Test Store Item");
	}

	public void TestFullStockItems()
	{
		if (itemInventoryData.Containers.Count <= 0)
		{
			Debug.Log("No Item Container Found");
			return;
		}

		ItemInventoryData.AdjustItemQuantity(123333, itemInventoryData.Containers[0], 100);

		Debug.Log("123333 * 100 Stock Items");
	}
}
