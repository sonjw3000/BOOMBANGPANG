using System.Collections.Generic;
using System.Net.Sockets;
using Unity.Mathematics;
using UnityEngine;
using static UnityEditor.Progress;

public class RocketManager : MonoBehaviour
{
	[SerializeField] private int initialPoolSize = 5;

	[SerializeField] private int3 landingZoneCenter = new int3(10, 0, 10);
	[SerializeField] private int landingZoneRadius = 5;

	[SerializeField] private Vector2 fallingTimeRange = new Vector2(3.0f, 7.0f);
	[SerializeField] private Vector2 fallingSpeedRange = new Vector2(3.0f, 7.0f);

	[SerializeField] private float timeSinceLastSpawn = 0.0f;
	[SerializeField] private float spawnInterval = 10.0f;

	[SerializeField] private List<Rocket> activeRockets = new();
	private Queue<Rocket> rocketPool = new();

	public int3 ZoneCenter => landingZoneCenter;
	public int ZoneRadius => landingZoneRadius;
	public IReadOnlyList<ShelfBase> Rockets => activeRockets;

	private ItemDatabase ItemDB => GameContext.Instance.ItemDB;
	private InboundWorkflowManager IBWorkflowMgr => GameContext.Instance.IBWorkflowMgr;
	private Resources ResourceMgr => GameContext.Instance.MapResources;

	private GameObject rocketPoolParent = null;

	private int RocketPrefabIndex => GameContext.Instance.MapResources.FindPrefabIndexByName("TestRocket");

	private void Awake()
	{
		if (rocketPoolParent == null)
		{
			rocketPoolParent = new GameObject("RocketPool");
			rocketPoolParent.transform.parent = transform;
		}

		for (int i = 0; i < initialPoolSize; ++i)
		{
			InstantiateNewRocket();
		}
	}

	private void Update()
	{
		timeSinceLastSpawn += Time.deltaTime;

		// spawn rocket every 10 seconds
		if (timeSinceLastSpawn >= spawnInterval)
		{
			timeSinceLastSpawn = 0.0f;
			SpawnRocketOnRandom();
		}
	}

	public void SpawnRocketOnRandom()
	{
		if (rocketPool.Count <= 0) 
			InstantiateNewRocket();

		rocketPool.TryDequeue(out Rocket rocket);

		if (rocket == null)
		{
			Debug.LogError("RocketManager: Failed to dequeue rocket from pool.");
			return;
		}

		// set random landing point near gamecontext's rocket landing zone
		// todo
		// 모든 타일이 균등하게 선택될 수 있도록 수정해야 한다
		int randomX = UnityEngine.Random.Range(-ZoneRadius, ZoneRadius + 1);
		int absX = Mathf.Abs(randomX);
		int randomZ = UnityEngine.Random.Range(-ZoneRadius + absX, ZoneRadius + 1 - absX);

		int3 landingPoint = ZoneCenter + new int3(randomX, 0, randomZ);

		float randomYRotation = UnityEngine.Random.Range(0.0f, 360.0f);
		Quaternion rot = Quaternion.Euler(20.0f, randomYRotation, 180.0f);
		rocket.transform.rotation = rot;

		// set position and direction
		Vector3 fowardVector = rot * new Vector3(0, 1, 0);

		float randSpeed = UnityEngine.Random.Range(fallingSpeedRange.x, fallingSpeedRange.y);
		float randTime = UnityEngine.Random.Range(fallingTimeRange.x, fallingTimeRange.y);
		rocket.transform.position = new Vector3(landingPoint.x, landingPoint.y, landingPoint.z) - fowardVector * randTime;

		// initialize rocket position
		rocket.InitializePosition(landingPoint, fowardVector, randSpeed);

		// set rocket's payload
		rocket.SetupPayload(BuildRandomPayload());

		// render on & rocket move on
		rocket.enabled = true;
		rocket.gameObject.SetActive(true);
	}

	public void DisableRocket(Rocket rocket)
	{
		rocket.gameObject.SetActive(false);
		activeRockets.Remove(rocket);
		rocketPool.Enqueue(rocket);
	}

	private void InstantiateNewRocket()
	{
		var rocketObj = Instantiate(GameContext.Instance.MapResources.Prefabs[RocketPrefabIndex], rocketPoolParent.transform);
		var rocketComp = rocketObj.GetComponent<Rocket>();
		rocketObj.SetActive(false);
		rocketPool.Enqueue(rocketComp);
	}

	private Dictionary<uint, ItemStack> BuildRandomPayload()
	{
		// todo
		// testing with random item
		// 
		// Todo
		// 나중에는 유저의 주문에 따라서 (재고 물건 부족에 따라서)
		// 아이템을 세팅해주어야 한다
		// itemstack을 만들어서 넘겨주자!(payload라 명명)

		Dictionary<uint, ItemStack> payload = new();
		
		uint randomItemID = ItemDB.GetRandomItemID();
		payload[randomItemID] = new ItemStack(randomItemID, 0);// { ItemID = randomItemID, Quantity = 10 };
		payload[randomItemID].AddItem(10);

		return payload;
	}

	public void OnRocketLanding(Rocket rocket)
	{
		// 해당 rocket을 activeRockets에 추가
		activeRockets.Add(rocket);

		// rocket move off
		rocket.enabled = false;

		// todo
		// 로켓을 Map Resource에 등록
		// 로켓이 떨어진 위치에 있는 객체 파괴 << Resource에서 노티만 해주기
		int3 pos = rocket.LandingPos;
		ResourceMgr.mapRef[pos.x, pos.y, pos.z].Set(RocketPrefabIndex, ResourceMgr.mapRef, rocket.gameObject, pos);

		// todo
		// IB Manager에게 로켓의 payload를 넘겨주고 이를 처리하도록 하자
		IBWorkflowMgr.BuildTaskByPayload(rocket);
	}

}
