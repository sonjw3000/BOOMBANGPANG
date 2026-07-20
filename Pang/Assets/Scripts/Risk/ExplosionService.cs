using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

public sealed class ExplosionService
{
	private readonly Queue<ExplosionRequest> pendingRequests = new();
	private readonly Dictionary<GameObject, int3> affectedObjects = new();
	private readonly HashSet<IItemContainer> affectedContainers = new();

	private GameTime gameTime;
	private bool isProcessing;

	public int PendingTriggerCount => pendingRequests.Count;

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
		affectedObjects.Clear();
		affectedContainers.Clear();
		isProcessing = false;
	}

	public void Enqueue(in ItemDamageIncidentTrigger trigger)
	{
		pendingRequests.Enqueue(new ExplosionRequest(
			trigger.OriginCell,
			trigger.Radius,
			trigger.Severity,
			trigger.DamageChange.ItemId,
			isDebugRequest: false));
	}

	public bool TryEnqueueDebugExplosion(in int3 originCell, int radius, int severity)
	{
		if (radius < 0 || severity <= 0)
			return false;

		pendingRequests.Enqueue(new ExplosionRequest(
			originCell,
			radius,
			Mathf.Clamp(severity, 1, 100),
			itemId: 0,
			isDebugRequest: true));
		return true;
	}

	private void HandleSimulationTick(SimulationTickContext context)
	{
		if (isProcessing || pendingRequests.Count == 0)
			return;

		isProcessing = true;
		try
		{
			while (pendingRequests.Count > 0)
			{
				ExplosionRequest request = pendingRequests.Dequeue();
				ProcessRequest(in request);
			}
		}
		finally
		{
			isProcessing = false;
		}
	}

	private void ProcessRequest(in ExplosionRequest request)
	{
		if (GameContext.HasInstance == false)
			return;

		GridService gridService = GameContext.Instance.GridService;
		ItemDamageService itemDamageService = GameContext.Instance.ItemDamage;
		if (gridService == null || gridService.IsReady == false || itemDamageService == null)
			return;

		CollectAffectedObjects(gridService, in request.OriginCell, request.Radius, request.IsDebugRequest);
		affectedContainers.Clear();

		int damagedHealthTargets = 0;
		int damagedContainers = 0;
		foreach (var affectedObject in affectedObjects)
		{
			GameObject targetObject = affectedObject.Key;
			if (targetObject == null)
				continue;

			if (targetObject.TryGetComponent<AIWorker>(out var worker))
			{
				if (worker.ApplyDamage(request.Severity) > 0.0f)
					++damagedHealthTargets;

				BoxBase carryingBox = worker.CarryingAbility?.CarryingBox;
				if (ApplyContainerDamageOnce(
					itemDamageService,
					carryingBox,
					worker.GridPosition,
					request.Severity))
					++damagedContainers;

				continue;
			}

			if (targetObject.TryGetComponent<IFacility>(out var facility) == false)
			{
				if (targetObject.TryGetComponent<IItemContainer>(out var exposedContainer) &&
					ApplyContainerDamageOnce(
						itemDamageService,
						exposedContainer,
						affectedObject.Value,
						request.Severity))
				{
					++damagedContainers;
				}

				continue;
			}

			if (facility.ApplyDamage(request.Severity) > 0.0f)
				++damagedHealthTargets;

			if (facility.Health <= 0.0f)
			{
				DestroyContext destroyContext = new(DestroyContext.Destroycause.Explosion);
				GameContext.Instance.FacilityMgr?.DestroyFacility(facility, in destroyContext);
				continue;
			}

			if (facility is IItemContainer facilityContainer && ApplyContainerDamageOnce(
					itemDamageService,
					facilityContainer,
					facility.GridPosition,
					request.Severity))
			{
				++damagedContainers;
			}

			if (facility is PackingStation packingStation)
			{
				if (ApplyContainerDamageOnce(itemDamageService, packingStation.WaitingBox?.Box, packingStation.GridPosition, request.Severity))
					++damagedContainers;
				if (ApplyContainerDamageOnce(itemDamageService, packingStation.CurrentPackingBox?.Box, packingStation.GridPosition, request.Severity))
					++damagedContainers;
				if (ApplyContainerDamageOnce(itemDamageService, packingStation.EndPackingBox?.Box, packingStation.GridPosition, request.Severity))
					++damagedContainers;
			}

			if (facility is LaunchStation launchStation)
			{
				if (launchStation.TryGetAddon<LaunchPadAddon>(out var launchPad) &&
					ApplyContainerDamageOnce(itemDamageService, launchPad.CargoToLaunch, launchStation.GridPosition, request.Severity))
				{
					++damagedContainers;
				}

				if (launchStation.TryGetAddon<CargoStorageAddon>(out var cargoStorage))
				{
					foreach (BoxBase cargo in cargoStorage.CargosToLaunch)
					{
						if (ApplyContainerDamageOnce(itemDamageService, cargo, launchStation.GridPosition, request.Severity))
							++damagedContainers;
					}
				}
			}
		}

		if (request.IsDebugRequest)
		{
			Debug.Log(
				$"[DebugControl] Explosion processed at ({request.OriginCell.x},{request.OriginCell.y},{request.OriginCell.z}) " +
				$"radius={request.Radius}, severity={request.Severity}, " +
				$"healthTargets={damagedHealthTargets}, containers={damagedContainers}");
		}
		else
		{
			PublishHudEvent(in request, damagedHealthTargets, damagedContainers);
		}
	}

	private void CollectAffectedObjects(GridService gridService, in int3 origin, int radius, bool logAffectedCells)
	{
		affectedObjects.Clear();
		radius = Mathf.Max(0, radius);
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

				if (logAffectedCells)
					Debug.Log($"[DebugExplosion] Cell affected: ({x},{origin.y},{z})");

				int3 affectedCell = new(x, origin.y, z);
				if (cell.ObjectOnGrid != null)
					affectedObjects.TryAdd(cell.ObjectOnGrid, affectedCell);
				if (cell.OccupancyObjectOnGrid != null)
					affectedObjects.TryAdd(cell.OccupancyObjectOnGrid, affectedCell);
			}
		}
	}

	private bool ApplyContainerDamageOnce(
		ItemDamageService itemDamageService,
		IItemContainer container,
		in int3 originCell,
		int damage)
	{
		if (container == null || affectedContainers.Add(container) == false)
			return false;

		return ApplyContainerDamage(itemDamageService, container, in originCell, damage);
	}

	private static bool ApplyContainerDamage(
		ItemDamageService itemDamageService,
		IItemContainer container,
		in int3 originCell,
		int damage)
	{
		if (container == null || damage <= 0)
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

	private static void PublishHudEvent(
		in ExplosionRequest request,
		int damagedHealthTargets,
		int damagedContainers)
	{
		GameContext.Instance.HudEventManager?.Publish(
			HudEventType.Error,
			$"EXPLOSION item:{request.ItemId} @({request.OriginCell.x},{request.OriginCell.y},{request.OriginCell.z}) " +
			$"R:{request.Radius} S:{request.Severity} H:{damagedHealthTargets} C:{damagedContainers}");
	}

	private readonly struct ExplosionRequest
	{
		public readonly int3 OriginCell;
		public readonly int Radius;
		public readonly int Severity;
		public readonly uint ItemId;
		public readonly bool IsDebugRequest;

		public ExplosionRequest(
			in int3 originCell,
			int radius,
			int severity,
			uint itemId,
			bool isDebugRequest)
		{
			OriginCell = originCell;
			Radius = Mathf.Max(0, radius);
			Severity = Mathf.Clamp(severity, 0, 100);
			ItemId = itemId;
			IsDebugRequest = isDebugRequest;
		}
	}
}
