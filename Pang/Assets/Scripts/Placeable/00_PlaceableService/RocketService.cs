using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

public partial class RocketService : FacilityService<Rocket>
{
	[SerializeField] private int initialPoolSize = 5;
	[SerializeField] private FacingDirection landingFacingDirection = FacingDirection.North;
	[SerializeField] [Range(0.0f, 1.0f)] private float hardLandingChance = 0.35f;
	[SerializeField] private Vector2 fallingTimeRange = new(3.0f, 7.0f);
	[SerializeField] private Vector2 fallingSpeedRange = new(3.0f, 7.0f);

	[SerializeField] private float rocketPayloadSize = 1000.0f;

	[SerializeField] private List<Rocket> activeRockets = new();
	private readonly Queue<Rocket> rocketPool = new();
	private readonly List<GameObject> overridePreviewTargets = new();

	public IReadOnlyList<Rocket> Rockets => activeRockets;
	public event System.Action<Rocket> InboundRocketLanded;

	private ItemDatabase ItemDB => GameContext.Instance.ItemDB;

	private GameObject rocketPoolParent = null;
	private PlaceableDefinition rocketPD;

	protected override void Start()
	{
		base.Start();

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

	public bool CanLandAt(in int3 candidatePoint)
	{
		return CanLand(candidatePoint);
	}

	public bool TrySpawnInboundRocket(in int3 landingPoint)
	{
		if (rocketPool.Count <= 0)
			InstantiateNewRocket();

		rocketPool.TryDequeue(out Rocket rocket);

		if (rocket == null)
		{
			Debug.LogError("RocketService: Failed to dequeue rocket from pool.");
			return false;
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

		return true;
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
		if (rocket != null && rocket.TryUndockCapsule(out CargoCapsule capsule))
			capsule.transform.SetParent(null, true);

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
		if (rocketPD == null || rocketPD.prefab == null)
		{
			Debug.LogError("[RocketService] Cannot instantiate rocket because the placeable definition or prefab is missing.");
			return;
		}

		var rocketObj = Instantiate(rocketPD.prefab, rocketPoolParent.transform);
		var rocketComp = rocketObj.GetComponent<Rocket>();

		if (rocketComp == null)
		{
			Debug.LogError($"[RocketService] Rocket prefab '{rocketPD.prefab.name}' is missing the Rocket component.");
			Destroy(rocketObj);
			return;
		}

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

		RocketLandingOutcome landingOutcome = BuildLandingOutcome(rocket.LandingPos);
		rocket.SetLandingOutcome(in landingOutcome);

		PlacementContext ctx = new(
			rocket.LandingPos,
			landingFacingDirection,
			rocketPD,
			GetPlacementEvent(in landingOutcome),
			rocket.gameObject
		);

		rocket.transform.position = Vector3.zero;
		if (GridService.OnInstall(ctx) == false)
		{
			Debug.LogError($"[RocketService] Failed to install landed rocket at {rocket.LandingPos}.");
			return;
		}

		rocket.enabled = false;
		rocket.ApplyLandingOutcome();
		InboundRocketLanded?.Invoke(rocket);
	}

	private RocketLandingOutcome BuildLandingOutcome(in int3 landingPoint)
	{
		RocketLandingSeverity severity = UnityEngine.Random.value < hardLandingChance
			? RocketLandingSeverity.Hard
			: RocketLandingSeverity.Soft;

		PlacementContext previewContext = new(
			landingPoint,
			landingFacingDirection,
			rocketPD,
			PlacementEvent.RocketCrashLanding
		);

		GridService.GetOverrideTargets(previewContext, overridePreviewTargets);

		int overriddenRocketCount = 0;
		for (int i = 0; i < overridePreviewTargets.Count; ++i)
		{
			GameObject target = overridePreviewTargets[i];
			if (target != null && target.TryGetComponent<Rocket>(out _))
				overriddenRocketCount++;
		}

		return new RocketLandingOutcome(
			severity,
			overridePreviewTargets.Count,
			overriddenRocketCount
		);
	}

	private static PlacementEvent GetPlacementEvent(in RocketLandingOutcome landingOutcome)
	{
		return landingOutcome.Severity == RocketLandingSeverity.Hard || landingOutcome.HasOverride
			? PlacementEvent.RocketCrashLanding
			: PlacementEvent.RocketLanding;
	}

}
