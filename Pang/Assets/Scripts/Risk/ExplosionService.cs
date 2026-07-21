using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

public sealed class ExplosionService
{
	private const int DebugEdgeDamagePercent = 25;
	private const int DebugIgnitionDelayTicks = 2;

	private readonly Queue<ExplosionRequest> pendingRequests = new();
	private readonly Queue<PendingItemImpact> pendingItemImpacts = new();
	private readonly Dictionary<GameObject, int3> affectedObjects = new();
	private readonly HashSet<IItemContainer> affectedContainers = new();

	private GameTime gameTime;
	private bool isProcessing;

	public int PendingTriggerCount => pendingRequests.Count;
	public int PendingItemImpactCount => pendingItemImpacts.Count;

	public void Bind(GameTime targetGameTime)
	{
		if (gameTime == targetGameTime)
			return;

		Unbind();
		gameTime = targetGameTime;
		if (gameTime != null)
			gameTime.OnSimulationTick += HandleSimulationTick;
	}

	public void Unbind()
	{
		if (gameTime != null)
			gameTime.OnSimulationTick -= HandleSimulationTick;

		gameTime = null;
	}

	public void ResetRuntimeState()
	{
		pendingRequests.Clear();
		pendingItemImpacts.Clear();
		affectedObjects.Clear();
		affectedContainers.Clear();
		isProcessing = false;
	}

	public void Enqueue(in ItemDamageIncidentTrigger trigger)
	{
		ulong currentTick = CurrentTick;
		pendingRequests.Enqueue(new ExplosionRequest(
			currentTick + (ulong)trigger.TriggerDelayTicks,
			trigger.OriginCell,
			trigger.Radius,
			trigger.Severity,
			trigger.EdgeDamagePercent,
			trigger.DamageChange.ItemId,
			isDebugRequest: false));

		if (trigger.TriggerDelayTicks > 0)
			PublishIgnition(in trigger.OriginCell, trigger.Container, trigger.TriggerDelayTicks);

		TryProcess(currentTick, applyPendingItemImpacts: false);
	}

	public bool TryEnqueueDebugExplosion(in int3 originCell, int radius, int severity)
	{
		if (radius < 0 || severity <= 0)
			return false;

		ulong currentTick = CurrentTick;
		pendingRequests.Enqueue(new ExplosionRequest(
			currentTick + DebugIgnitionDelayTicks,
			originCell,
			radius,
			Mathf.Clamp(severity, 1, 100),
			DebugEdgeDamagePercent,
			itemId: 0,
			isDebugRequest: true));
		PublishIgnition(in originCell, container: null, DebugIgnitionDelayTicks);
		TryProcess(currentTick, applyPendingItemImpacts: false);
		return true;
	}

	private ulong CurrentTick => gameTime?.SimulationTicksPassed ?? 0;

	private void HandleSimulationTick(SimulationTickContext context)
	{
		TryProcess(context.Tick, applyPendingItemImpacts: true);
	}

	private void TryProcess(ulong currentTick, bool applyPendingItemImpacts)
	{
		if (isProcessing || (pendingRequests.Count == 0 &&
			(applyPendingItemImpacts == false || pendingItemImpacts.Count == 0)))
			return;

		isProcessing = true;
		try
		{
			if (applyPendingItemImpacts)
				ApplyPendingItemImpacts(currentTick);

			ProcessDueExplosionRequests(currentTick);
		}
		finally
		{
			isProcessing = false;
		}
	}

	private void ProcessDueExplosionRequests(ulong currentTick)
	{
		int requestCount = pendingRequests.Count;
		for (int i = 0; i < requestCount; ++i)
		{
			ExplosionRequest request = pendingRequests.Dequeue();
			if (request.ActivationTick > currentTick)
			{
				pendingRequests.Enqueue(request);
				continue;
			}

			ProcessRequest(in request, currentTick);
		}
	}

	private void ProcessRequest(in ExplosionRequest request, ulong currentTick)
	{
		if (GameContext.HasInstance == false)
			return;

		GridService gridService = GameContext.Instance.GridService;
		if (gridService == null || gridService.IsReady == false)
			return;

		CollectAffectedObjects(gridService, in request);
		affectedContainers.Clear();

		int damagedHealthTargets = 0;
		int pendingContainers = 0;
		foreach (var affectedObject in affectedObjects)
		{
			GameObject targetObject = affectedObject.Key;
			if (targetObject == null)
				continue;

			int3 affectedCell = affectedObject.Value;
			int damage = CalculateExplosionDamage(in request, in affectedCell);
			if (damage <= 0)
				continue;

			if (targetObject.TryGetComponent<AIWorker>(out var worker))
			{
				if (worker.ApplyDamage(damage) > 0.0f)
					++damagedHealthTargets;

				BoxBase carryingBox = worker.CarryingAbility?.CarryingBox;
				if (EnqueueContainerImpactOnce(
					carryingBox,
					worker.GridPosition,
					damage,
					currentTick))
					++pendingContainers;

				continue;
			}

			if (targetObject.TryGetComponent<IFacility>(out var facility) == false)
			{
				if (targetObject.TryGetComponent<IItemContainer>(out var exposedContainer) &&
					EnqueueContainerImpactOnce(
						exposedContainer,
						affectedCell,
						damage,
						currentTick))
				{
					++pendingContainers;
				}

				continue;
			}

			if (facility.ApplyDamage(damage) > 0.0f)
				++damagedHealthTargets;

			if (facility.Health <= 0.0f && GameContext.Instance.FacilityMgr?.IsDestroyed(facility) == false)
			{
				DestroyContext destroyContext = new(DestroyContext.Destroycause.Explosion);
				GameContext.Instance.FacilityMgr?.DestroyFacility(facility, in destroyContext);
			}

			if (facility is IItemContainer facilityContainer && EnqueueContainerImpactOnce(
					facilityContainer,
					facility.GridPosition,
					damage,
					currentTick))
			{
				++pendingContainers;
			}

			if (facility is CapsuleDock capsuleDock && facility is not IItemContainer &&
				EnqueueContainerImpactOnce(
					capsuleDock.DockedCapsule,
					facility.GridPosition,
					damage,
					currentTick))
			{
				++pendingContainers;
			}

			if (facility is BoxPool boxPool)
			{
				foreach (BoxBase box in boxPool.Boxes)
				{
					if (EnqueueContainerImpactOnce(
						box,
						facility.GridPosition,
						damage,
						currentTick))
					{
						++pendingContainers;
					}
				}
			}

			if (facility is PackingStation packingStation)
			{
				if (EnqueueContainerImpactOnce(packingStation.WaitingBox?.Box, packingStation.GridPosition, damage, currentTick))
					++pendingContainers;
				if (EnqueueContainerImpactOnce(packingStation.CurrentPackingBox?.Box, packingStation.GridPosition, damage, currentTick))
					++pendingContainers;
				if (EnqueueContainerImpactOnce(packingStation.EndPackingBox?.Box, packingStation.GridPosition, damage, currentTick))
					++pendingContainers;
			}

			if (facility is LaunchStation launchStation)
			{
				if (launchStation.TryGetAddon<LaunchPadAddon>(out var launchPad) &&
					EnqueueContainerImpactOnce(launchPad.CargoToLaunch, launchStation.GridPosition, damage, currentTick))
				{
					++pendingContainers;
				}

				if (launchStation.TryGetAddon<CargoStorageAddon>(out var cargoStorage))
				{
					foreach (BoxBase cargo in cargoStorage.CargosToLaunch)
					{
						if (EnqueueContainerImpactOnce(cargo, launchStation.GridPosition, damage, currentTick))
							++pendingContainers;
					}
				}
			}
		}

		if (request.IsDebugRequest)
		{
			Debug.Log(
				$"[DebugControl] Explosion processed at ({request.OriginCell.x},{request.OriginCell.y},{request.OriginCell.z}) " +
				$"radius={request.Radius}, severity={request.Severity}, " +
				$"healthTargets={damagedHealthTargets}, pendingContainers={pendingContainers}");
		}
		else
		{
			PublishHudEvent(in request, damagedHealthTargets, pendingContainers);
		}
	}

	private void CollectAffectedObjects(GridService gridService, in ExplosionRequest request)
	{
		affectedObjects.Clear();
		int3 origin = request.OriginCell;
		int radius = Mathf.Max(0, request.Radius);
		long radiusSquared = (long)radius * radius;

		for (int z = origin.z - radius; z <= origin.z + radius; ++z)
		{
			for (int x = origin.x - radius; x <= origin.x + radius; ++x)
			{
				long offsetX = x - origin.x;
				long offsetZ = z - origin.z;
				if (offsetX * offsetX + offsetZ * offsetZ > radiusSquared)
					continue;

				GridCell cell = gridService.GetCell(x, origin.y, z);
				if (cell == null)
					continue;

				if (request.IsDebugRequest)
					Debug.Log($"[DebugExplosion] Cell affected: ({x},{origin.y},{z})");

				int3 affectedCell = new(x, origin.y, z);
				PublishExplosionCue(in affectedCell, CalculateExplosionDamage(in request, in affectedCell));
				if (cell.ObjectOnGrid != null)
					TrackAffectedObject(cell.ObjectOnGrid, in affectedCell, in origin);
				if (cell.OccupancyObjectOnGrid != null)
					TrackAffectedObject(cell.OccupancyObjectOnGrid, in affectedCell, in origin);
			}
		}
	}

	private void TrackAffectedObject(GameObject target, in int3 affectedCell, in int3 origin)
	{
		if (target == null)
			return;

		if (affectedObjects.TryGetValue(target, out int3 previousCell))
		{
			long previousDistance = DistanceSquared(in previousCell, in origin);
			long nextDistance = DistanceSquared(in affectedCell, in origin);
			if (previousDistance <= nextDistance)
				return;
		}

		affectedObjects[target] = affectedCell;
	}

	private static int CalculateExplosionDamage(in ExplosionRequest request, in int3 affectedCell)
	{
		if (request.Severity <= 0)
			return 0;

		if (request.Radius <= 0)
			return request.Severity;

		float offsetX = affectedCell.x - request.OriginCell.x;
		float offsetZ = affectedCell.z - request.OriginCell.z;
		float distance = Mathf.Sqrt(offsetX * offsetX + offsetZ * offsetZ);
		float normalizedDistance = Mathf.Clamp01(distance / request.Radius);
		float edgeRatio = request.EdgeDamagePercent / 100.0f;
		float multiplier = Mathf.Lerp(1.0f, edgeRatio, normalizedDistance);
		return Mathf.Clamp(Mathf.RoundToInt(request.Severity * multiplier), 0, 100);
	}

	private static long DistanceSquared(in int3 a, in int3 b)
	{
		long offsetX = a.x - b.x;
		long offsetZ = a.z - b.z;
		return offsetX * offsetX + offsetZ * offsetZ;
	}

	private void ApplyPendingItemImpacts(ulong currentTick)
	{
		if (GameContext.HasInstance == false)
			return;

		ItemDamageService itemDamageService = GameContext.Instance.ItemDamage;
		if (itemDamageService == null)
			return;

		while (pendingItemImpacts.Count > 0 && pendingItemImpacts.Peek().ApplyTick <= currentTick)
		{
			PendingItemImpact impact = pendingItemImpacts.Dequeue();
			ApplyContainerDamage(
				itemDamageService,
				impact.Container,
				in impact.OriginCell,
				impact.BaseDamage);
		}
	}

	private bool EnqueueContainerImpactOnce(
		IItemContainer container,
		in int3 originCell,
		int baseDamage,
		ulong currentTick)
	{
		if (IsContainerAvailable(container) == false ||
			baseDamage <= 0 ||
			affectedContainers.Add(container) == false)
			return false;

		bool hasDamageTarget = false;
		IReadOnlyList<ItemStack> stacks = container.Stacks;
		for (int i = 0; i < stacks.Count; ++i)
		{
			ItemStack stack = stacks[i];
			if (stack == null || stack.Quantity <= 0 || stack.Damage >= 100)
				continue;

			hasDamageTarget = true;
		}

		if (hasDamageTarget == false)
			return false;

		pendingItemImpacts.Enqueue(new PendingItemImpact(
			currentTick + 1,
			container,
			in originCell,
			baseDamage));

		return true;
	}

	private static bool ApplyContainerDamage(
		ItemDamageService itemDamageService,
		IItemContainer container,
		in int3 originCell,
		int damage)
	{
		if (IsContainerAvailable(container) == false || damage <= 0)
			return false;

		bool applied = false;
		IReadOnlyList<ItemStack> stacks = container.Stacks;
		for (int i = stacks.Count - 1; i >= 0; --i)
		{
			ItemStack stack = stacks[i];
			if (itemDamageService.TryApplyDamage(
					stack,
					damage,
					in originCell,
					container,
					ItemDamageCause.Explosion,
					out _))
			{
				applied = true;
			}
		}

		return applied;
	}

	private static bool IsContainerAvailable(IItemContainer container)
	{
		if (container == null)
			return false;

		return container is not Object unityObject || unityObject != null;
	}

	private static void PublishIgnition(in int3 originCell, IItemContainer container, int delayTicks)
	{
		if (GameContext.HasInstance == false)
			return;

		Object source = container as Object;
		GameContext.Instance.HudEventManager?.Publish(
			HudEventType.Warning,
			$"IGNITION @({originCell.x},{originCell.y},{originCell.z}) - detonation in {delayTicks} ticks",
			source);

		GameContext.Instance.FloatingTextManager?.ShowWorld(
			FloatingTextPreset.Error,
			$"IGNITION\n{delayTicks} TICKS",
			new Vector3(originCell.x, originCell.y + 1.0f, originCell.z));
	}

	private static void PublishExplosionCue(in int3 affectedCell, int damage)
	{
		if (GameContext.HasInstance == false)
			return;

		GameContext.Instance.FloatingTextManager?.ShowWorld(
			FloatingTextPreset.Error,
			$"EXPLOSION\nDMG {damage}",
			new Vector3(affectedCell.x, affectedCell.y + 1.0f, affectedCell.z));
	}

	private static void PublishHudEvent(
		in ExplosionRequest request,
		int damagedHealthTargets,
		int pendingContainers)
	{
		GameContext.Instance.HudEventManager?.Publish(
			HudEventType.Error,
			$"EXPLOSION item:{request.ItemId} @({request.OriginCell.x},{request.OriginCell.y},{request.OriginCell.z}) " +
			$"R:{request.Radius} S:{request.Severity} H:{damagedHealthTargets} P:{pendingContainers}");
	}

	private readonly struct ExplosionRequest
	{
		public readonly ulong ActivationTick;
		public readonly int3 OriginCell;
		public readonly int Radius;
		public readonly int Severity;
		public readonly int EdgeDamagePercent;
		public readonly uint ItemId;
		public readonly bool IsDebugRequest;

		public ExplosionRequest(
			ulong activationTick,
			in int3 originCell,
			int radius,
			int severity,
			int edgeDamagePercent,
			uint itemId,
			bool isDebugRequest)
		{
			ActivationTick = activationTick;
			OriginCell = originCell;
			Radius = Mathf.Max(0, radius);
			Severity = Mathf.Clamp(severity, 0, 100);
			EdgeDamagePercent = Mathf.Clamp(edgeDamagePercent, 0, 100);
			ItemId = itemId;
			IsDebugRequest = isDebugRequest;
		}
	}

	private readonly struct PendingItemImpact
	{
		public readonly ulong ApplyTick;
		public readonly IItemContainer Container;
		public readonly int3 OriginCell;
		public readonly int BaseDamage;

		public PendingItemImpact(
			ulong applyTick,
			IItemContainer container,
			in int3 originCell,
			int baseDamage)
		{
			ApplyTick = applyTick;
			Container = container;
			OriginCell = originCell;
			BaseDamage = Mathf.Clamp(baseDamage, 0, 100);
		}
	}
}
