using Unity.Mathematics;
using UnityEngine;

// 이것만은 꼭 지키자
// GameContext는 데이터만 가진다
// 로직을 가져선 안된다

[DefaultExecutionOrder(-100)]
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

	[SerializeField] private Resources mapResources;
	[SerializeField] private ItemDatabase itemDB;
	private ItemInventory itemInventoryData = new();
	[SerializeField] private int3 rocketLandingZoneCenter;
	[SerializeField] private int rocketLandingZoneRadius = 5;

	public Resources MapResources => mapResources;
	public ItemDatabase ItemDB => itemDB;
	public ItemInventory ItemInventoryData => itemInventoryData;
	public int3 RocketLandingZoneCenter => rocketLandingZoneCenter;
	public int RocketLandingZoneRadius => rocketLandingZoneRadius;

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
		instance.mapResources.Initialize();
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
