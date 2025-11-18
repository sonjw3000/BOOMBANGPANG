using UnityEngine;

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

		Debug.Log("Test Store Item");
	}

	public void TestFullStockItems()
	{
		if (itemInventoryData.Containers.Count <= 0)
		{
			Debug.Log("No Item Container Found");
			return;
		}



		Debug.Log("Full Stock Items");
	}
}
