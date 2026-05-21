using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

public class RocketManager : GridPlaceableManager<Rocket>
{
	[SerializeField] private int initialPoolSize = 5;
	[SerializeField] private ZoneManager zoneManager;
	[SerializeField] private ZoneType landingZoneType = ZoneType.RocketLanding;
	[SerializeField] private int landingZoneFloor = 0;
	[SerializeField] private int randomSearchCountPerZone = 12;
	[SerializeField] private FacingDirection landingFacingDirection = FacingDirection.North;

	[SerializeField] private Vector2 fallingTimeRange = new(3.0f, 7.0f);
	[SerializeField] private Vector2 fallingSpeedRange = new(3.0f, 7.0f);

	[SerializeField] private float timeSinceLastSpawn = 0.0f;
	[SerializeField] private float spawnInterval = 10.0f;

	[SerializeField] private float rocketPayloadSize = 1000.0f;

	[SerializeField] private List<Rocket> activeRockets = new();
	private readonly Queue<Rocket> rocketPool = new();

	public IReadOnlyList<ShelfBase> Rockets => activeRockets;

	private ItemDatabase ItemDB => GameContext.Instance.ItemDB;
	private InboundWorkflowManager IBWorkflowMgr => GameContext.Instance.IBWorkflowMgr;
	private GridService GridService => GameContext.Instance.GridService;
	private DeliveryService DeliveryService => GameContext.Instance.DeliveryService;
	private ZoneManager ZoneManager
	{
		get
		{
			if (zoneManager == null && GameContext.HasInstance)
				zoneManager = GameContext.Instance.ZoneMgr;

			return zoneManager;
		}
	}

	private GameObject rocketPoolParent = null;
	private PlaceableDefinition rocketPD;

	private void Start()
	{
		rocketPD = GameContext.Instance.PlaceableCatalog.FindById("TestRocket");

		if (rocketPD == null)
			Debug.LogError("No rocketPD");

		if (rocketPoolParent == null)
		{
			rocketPoolParent = new GameObject("RocketPool");
			rocketPoolParent.transform.parent = transform;
		}

		for (int i = 0; i < initialPoolSize; ++i)
			InstantiateNewRocket();
	}

	private void Update()
	{
		timeSinceLastSpawn += Time.deltaTime;

		if (timeSinceLastSpawn >= spawnInterval)
		{
			if (DeliveryService.TryPeek(out var _) == false)
				return;

			timeSinceLastSpawn = 0.0f;
			SpawnRocketOnRandom();
		}
	}

	public void SpawnRocketOnRandom()
	{
		if (TryGetLandingPoint(out _, out var landingPoint) == false)
			return;

		if (rocketPool.Count <= 0)
			InstantiateNewRocket();

		rocketPool.TryDequeue(out Rocket rocket);

		if (rocket == null)
		{
			Debug.LogError("RocketManager: Failed to dequeue rocket from pool.");
			return;
		}

		float randomYRotation = UnityEngine.Random.Range(0.0f, 360.0f);
		Quaternion rot = Quaternion.Euler(20.0f, randomYRotation, 180.0f);
		rocket.transform.rotation = rot;

		Vector3 forwardVector = rot * new Vector3(0, 1, 0);

		float randSpeed = UnityEngine.Random.Range(fallingSpeedRange.x, fallingSpeedRange.y);
		float randTime = UnityEngine.Random.Range(fallingTimeRange.x, fallingTimeRange.y);
		rocket.transform.position = new Vector3(landingPoint.x, landingPoint.y, landingPoint.z) - forwardVector * randTime;

		rocket.InitializePosition(landingPoint, forwardVector, randSpeed);
		rocket.SetupPayloadByDelivery();
		rocket.enabled = true;
		rocket.gameObject.SetActive(true);
	}

	private bool TryGetLandingPoint(out ZoneArea landingZone, out int3 landingPoint)
	{
		landingZone = null;
		landingPoint = default;

		if (ZoneManager == null || rocketPD == null)
			return false;

		if (ZoneManager.TryGetZones(out var zones, landingZoneFloor, landingZoneType) == false)
			return false;

		int startIndex = UnityEngine.Random.Range(0, zones.Count);
		for (int i = 0; i < zones.Count; ++i)
		{
			var zone = zones[(startIndex + i) % zones.Count];
			if (TryFindLandingPoint(zone, out landingPoint))
			{
				landingZone = zone;
				return true;
			}
		}

		return false;
	}

	private bool TryFindLandingPoint(ZoneArea zone, out int3 landingPoint)
	{
		for (int i = 0; i < Mathf.Max(1, randomSearchCountPerZone); ++i)
		{
			zone.GetRandomPoint(out var candidatePoint);
			if (CanLand(candidatePoint))
			{
				landingPoint = candidatePoint;
				return true;
			}
		}

		for (int z = zone.Bounds.yMin; z < zone.Bounds.yMax; ++z)
		{
			for (int x = zone.Bounds.xMin; x < zone.Bounds.xMax; ++x)
			{
				var candidatePoint = new int3(x, zone.Floor, z);
				if (CanLand(candidatePoint))
				{
					landingPoint = candidatePoint;
					return true;
				}
			}
		}

		landingPoint = default;
		return false;
	}

	private bool CanLand(in int3 candidatePoint)
	{
		if (rocketPD == null)
			return false;

		List<int3> possible = new();
		List<int3> blocked = new();
		PlacementContext context = new(candidatePoint, landingFacingDirection, rocketPD, PlacementEvent.RocketCrashLanding);
		return GridService.OnCheckInstallable(context, possible, blocked) && blocked.Count == 0;
	}

	public Rocket GetRocketForLaunch(Vector3 position)
	{
		if (rocketPool.Count <= 0)
			InstantiateNewRocket();

		rocketPool.TryDequeue(out Rocket rocket);

		if (rocket != null)
		{
			rocket.transform.position = position;
			rocket.transform.rotation = Quaternion.identity;
			rocket.gameObject.SetActive(true);
			activeRockets.Add(rocket);
		}

		return rocket;
	}

	public void DisableRocket(Rocket rocket)
	{
		if (rocket == null)
			return;

		if (GridService != null && GridService.IsPlacedObject(rocket.gameObject))
			GridService.OnRemove(rocket.gameObject, destroyObject: false);

		DetachCargoChildren(rocket);

		rocket.gameObject.SetActive(false);
		if (rocketPoolParent != null)
			rocket.transform.SetParent(rocketPoolParent.transform, false);

		activeRockets.Remove(rocket);
		if (rocketPool.Contains(rocket) == false)
			rocketPool.Enqueue(rocket);
	}

	private static void DetachCargoChildren(Rocket rocket)
	{
		for (int i = rocket.transform.childCount - 1; i >= 0; --i)
		{
			Transform child = rocket.transform.GetChild(i);
			if (child.TryGetComponent<BoxBase>(out _) == false)
				continue;

			child.SetParent(null);
		}
	}

	private void InstantiateNewRocket()
	{
		var rocketObj = Instantiate(rocketPD.prefab, rocketPoolParent.transform);
		var rocketComp = rocketObj.GetComponent<Rocket>();
		rocketObj.SetActive(false);
		rocketPool.Enqueue(rocketComp);
	}

	private List<ItemStack> BuildRandomPayload()
	{
		List<ItemStack> payload = new();

		uint randomItemID = ItemDB.GetRandomItemID();
		ItemStack newStack = new(randomItemID, rocketPayloadSize);
		newStack.AddItem(10);

		payload.Add(newStack);
		return payload;
	}

	public void OnRocketLanding(Rocket rocket)
	{
		activeRockets.Add(rocket);

		PlacementContext ctx = new(
			rocket.LandingPos,
			landingFacingDirection,
			rocketPD,
			PlacementEvent.RocketCrashLanding,
			rocket.gameObject
		);

		rocket.transform.position = Vector3.zero;
		GridService.OnInstall(ctx);
		rocket.enabled = false;

		IBWorkflowMgr.BuildTaskByPayload(rocket);
	}

	public RocketManagerSaveData CaptureState()
	{
		return new RocketManagerSaveData
		{
			TimeSinceLastSpawn = timeSinceLastSpawn,
		};
	}

	public void RestoreState(RocketManagerSaveData data)
	{
		if (data == null)
			return;

		timeSinceLastSpawn = data.TimeSinceLastSpawn;
	}

	public void ResetRuntimeState()
	{
		timeSinceLastSpawn = 0.0f;
		activeRockets.Clear();
	}
}
