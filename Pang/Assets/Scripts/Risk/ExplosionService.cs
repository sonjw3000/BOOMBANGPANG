using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

public sealed class ExplosionService
{
	private readonly Queue<ItemDamageIncidentTrigger> pendingTriggers = new();
	private readonly HashSet<GameObject> affectedObjects = new();

	private GameTime gameTime;
	private bool isProcessing;

	public int PendingTriggerCount => pendingTriggers.Count;

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
		pendingTriggers.Clear();
		affectedObjects.Clear();
		isProcessing = false;
	}

	public void Enqueue(in ItemDamageIncidentTrigger trigger)
	{
		pendingTriggers.Enqueue(trigger);
	}

	private void HandleSimulationTick(SimulationTickContext context)
	{
		if (isProcessing || pendingTriggers.Count == 0)
			return;

		isProcessing = true;
		try
		{
			while (pendingTriggers.Count > 0)
			{
				ItemDamageIncidentTrigger trigger = pendingTriggers.Dequeue();
				ProcessTrigger(in trigger);
			}
		}
		finally
		{
			isProcessing = false;
		}
	}

	private void ProcessTrigger(in ItemDamageIncidentTrigger trigger)
	{
		if (GameContext.HasInstance == false)
			return;

		GridService gridService = GameContext.Instance.GridService;
		ItemDamageService itemDamageService = GameContext.Instance.ItemDamage;
		if (gridService == null || gridService.IsReady == false || itemDamageService == null)
			return;

		CollectAffectedObjects(gridService, in trigger.OriginCell, trigger.Radius);

		int damagedHealthTargets = 0;
		int damagedContainers = 0;
		foreach (GameObject targetObject in affectedObjects)
		{
			if (targetObject == null)
				continue;

			if (targetObject.TryGetComponent<AIWorker>(out var worker))
			{
				if (worker.ApplyDamage(trigger.Severity) > 0.0f)
					++damagedHealthTargets;

				BoxBase carryingBox = worker.CarryingAbility?.CarryingBox;
				if (carryingBox != null && ApplyContainerDamage(
						itemDamageService,
						carryingBox,
						worker.GridPosition,
						trigger.Severity))
				{
					++damagedContainers;
				}

				continue;
			}

			if (targetObject.TryGetComponent<IFacility>(out var facility) == false)
				continue;

			if (facility.ApplyDamage(trigger.Severity) > 0.0f)
				++damagedHealthTargets;

			if (facility.Health <= 0.0f)
			{
				DestroyContext destroyContext = new(DestroyContext.Destroycause.Explosion);
				GameContext.Instance.FacilityMgr?.DestroyFacility(facility, in destroyContext);
				continue;
			}

			if (targetObject.TryGetComponent<ShelfBase>(out var shelf) && ApplyContainerDamage(
					itemDamageService,
					shelf,
					shelf.GridPosition,
					trigger.Severity))
			{
				++damagedContainers;
			}
		}

		PublishHudEvent(in trigger, damagedHealthTargets, damagedContainers);
	}

	private void CollectAffectedObjects(GridService gridService, in int3 origin, int radius)
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

				if (cell.ObjectOnGrid != null)
					affectedObjects.Add(cell.ObjectOnGrid);
				if (cell.OccupancyObjectOnGrid != null)
					affectedObjects.Add(cell.OccupancyObjectOnGrid);
			}
		}
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
		in ItemDamageIncidentTrigger trigger,
		int damagedHealthTargets,
		int damagedContainers)
	{
		ItemDamageChange damage = trigger.DamageChange;
		GameContext.Instance.HudEventManager?.Publish(
			HudEventType.Error,
			$"EXPLOSION item:{damage.ItemId} @({trigger.OriginCell.x},{trigger.OriginCell.y},{trigger.OriginCell.z}) " +
			$"R:{trigger.Radius} S:{trigger.Severity} H:{damagedHealthTargets} C:{damagedContainers}");
	}
}
