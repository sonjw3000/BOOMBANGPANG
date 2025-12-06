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
	
	// datas
	[SerializeField] private Resources mapResources;
	[SerializeField] private ItemDatabase itemDB;

	// domain managers
	[SerializeField] private WorkerManager workerManager;
	[SerializeField] private ItemInventory itemInventory;
	[SerializeField] private RocketManager rocketManager;

	// workflow managers
	[SerializeField] private WorkFlowManager WorkFlowManager;

	public Resources MapResources => mapResources;
	public ItemDatabase ItemDB => itemDB;

	public WorkerManager WorkerMgr => workerManager;
	public ItemInventory ItemInventoryData => itemInventory;
	public RocketManager RocketMgr => rocketManager;

	public WorkFlowManager WorkflowMgr => WorkFlowManager;

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
}
