using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

public sealed class FireService
{
	public const float MinimumIgnitionOxygen = 40.0f;
	public const float MinimumSustainOxygen = 40.0f;
	public const float InitialFireIntensity = 25.0f;
	public const float IntensityStepPerTick = 25.0f;
	public const float MaximumFireIntensity = 100.0f;
	public const float NeighborIntensityMultiplier = 0.5f;

	private const int MaximumHealthDamagePerTick = 10;
	private const int MaximumItemDamagePerTick = 10;

	private static readonly int3[] AffectedDirections =
	{
		new(1, 0, 0),
		new(-1, 0, 0),
		new(0, 0, 1),
		new(0, 0, -1),
	};

	private readonly HashSet<IGridPlaceable> burningTargets = new();
	private readonly HashSet<GameObject> candidateObjects = new();
	private readonly List<IGridPlaceable> candidateTargets = new();
	private readonly List<IGridPlaceable> burningScratch = new();
	private readonly Dictionary<int3, float> projectedIntensities = new();
	private readonly HashSet<int3> previousProjectedCells = new();
	private readonly Dictionary<GameObject, float> damageTargets = new();

	public int BurningTargetCount => burningTargets.Count;

	public void ResetRuntimeState()
	{
		burningTargets.Clear();
		candidateObjects.Clear();
		candidateTargets.Clear();
		burningScratch.Clear();
		projectedIntensities.Clear();
		previousProjectedCells.Clear();
		damageTargets.Clear();
	}

	public void RebuildRuntimeState()
	{
		burningTargets.Clear();
		ClearAllCellFireIntensity();
		CollectCandidates();
		for (int i = 0; i < candidateTargets.Count; ++i)
		{
			IGridPlaceable target = candidateTargets[i];
			if (IsAvailable(target) && target.FireIntensity > 0.0f)
				burningTargets.Add(target);
		}

		ProjectActiveFires();
	}

	private static void ClearAllCellFireIntensity()
	{
		GridService gridService = GridService;
		if (gridService == null || gridService.IsReady == false)
			return;

		int3 size = gridService.MapSize;
		for (int x = 0; x < size.x; ++x)
			for (int y = 0; y < size.y; ++y)
				for (int z = 0; z < size.z; ++z)
					gridService.TrySetFireIntensity(new int3(x, y, z), 0.0f);
	}

	public void ProcessSimulationTick()
	{
		GridService gridService = GridService;
		if (gridService == null || gridService.IsReady == false)
			return;

		CollectCandidates();
		for (int i = 0; i < candidateTargets.Count; ++i)
			EvaluateTarget(candidateTargets[i]);

		RemoveMissingBurningTargets();
		ProjectActiveFires();
		ApplyFireDamage();
	}

	public void ReportTrigger(in ItemDamageIncidentTrigger trigger)
	{
		float intensity = Mathf.Max(InitialFireIntensity, trigger.Severity);
		if (trigger.Container is Component component &&
			component.TryGetComponent<IGridPlaceable>(out var directTarget))
		{
			ApplyIntensity(directTarget, intensity, showFloatingText: true);
		}
		else
		{
			TryApplyDebugFire(in trigger.OriginCell, intensity, out _);
		}

		GameContext.Instance.HudEventManager?.Publish(
			HudEventType.Warning,
			$"FIRE item:{trigger.DamageChange.ItemId} @({trigger.OriginCell.x},{trigger.OriginCell.y},{trigger.OriginCell.z}) " +
			$"I:{intensity:0} D:{trigger.DamageChange.PreviousDamage}>{trigger.DamageChange.CurrentDamage} {trigger.DamageChange.Cause}");
	}

	public bool TryApplyDebugFire(in int3 position, float intensity, out int affectedTargets)
	{
		affectedTargets = 0;
		GridCell cell = GridService?.GetCell(position);
		if (cell == null)
			return false;

		candidateObjects.Clear();
		candidateTargets.Clear();
		foreach (GameObject targetObject in cell.ObjectsOnGrid)
			AddCandidate(targetObject);

		for (int i = 0; i < candidateTargets.Count; ++i)
		{
			IGridPlaceable target = candidateTargets[i];
			if (IsAvailable(target) == false)
				continue;

			ApplyIntensity(target, intensity, showFloatingText: true);
			++affectedTargets;
		}

		ProjectActiveFires();
		return affectedTargets > 0;
	}

	private static GridService GridService => GameContext.HasInstance ? GameContext.Instance.GridService : null;

	private void CollectCandidates()
	{
		candidateObjects.Clear();
		candidateTargets.Clear();

		GridService gridService = GridService;
		if (gridService == null || gridService.IsReady == false)
			return;

		int3 size = gridService.MapSize;
		for (int x = 0; x < size.x; ++x)
		{
			for (int y = 0; y < size.y; ++y)
			{
				for (int z = 0; z < size.z; ++z)
				{
					GridCell cell = gridService.GetCell(x, y, z);
					if (cell == null)
						continue;

					foreach (GameObject targetObject in cell.ObjectsOnGrid)
						AddCandidate(targetObject);
				}
			}
		}

		burningScratch.Clear();
		foreach (IGridPlaceable target in burningTargets)
			burningScratch.Add(target);

		for (int i = 0; i < burningScratch.Count; ++i)
		{
			if (burningScratch[i] is Component component)
				AddCandidate(component.gameObject);
		}
	}

	private void AddCandidate(GameObject targetObject)
	{
		if (targetObject == null || candidateObjects.Add(targetObject) == false ||
			targetObject.TryGetComponent<IGridPlaceable>(out var target) == false)
		{
			return;
		}

		candidateTargets.Add(target);
		if (target is AIWorker worker && worker.CarryingAbility?.CarryingBox is BoxBase carryingBox &&
			candidateObjects.Add(carryingBox.gameObject))
		{
			candidateTargets.Add(carryingBox);
		}
	}

	private void EvaluateTarget(IGridPlaceable target)
	{
		if (IsAvailable(target) == false || TryResolvePosition(target, out int3 position) == false)
			return;

		GridCell cell = GridService.GetCell(position);
		if (cell == null)
			return;

		bool hasFuel = TryGetIgnitionTemperature(target, out float ignitionTemperature);
		float current = Mathf.Clamp(target.FireIntensity, 0.0f, MaximumFireIntensity);
		if (current <= 0.0f)
		{
			if (hasFuel && cell.Oxygen >= MinimumIgnitionOxygen &&
				cell.TemperatureCelsius >= ignitionTemperature)
			{
				ApplyIntensity(target, InitialFireIntensity, showFloatingText: true);
			}
			return;
		}

		if (hasFuel == false)
		{
			ApplyIntensity(target, 0.0f, showFloatingText: true);
			return;
		}

		float next = cell.Oxygen >= MinimumSustainOxygen
			? Mathf.Min(MaximumFireIntensity, current + IntensityStepPerTick)
			: Mathf.Max(0.0f, current - IntensityStepPerTick);
		ApplyIntensity(target, next, showFloatingText: true);
	}

	private bool TryGetIgnitionTemperature(IGridPlaceable target, out float ignitionTemperature)
	{
		ignitionTemperature = float.PositiveInfinity;
		bool hasFuel = false;

		if (target is Component component && target is not BoxBase &&
			(target is not IHealth health || health.Health > 0.0f) &&
			GridService.TryGetPlacementContext(component.gameObject, out PlacementContext context) &&
			context.placeableDefinition != null &&
			float.IsPositiveInfinity(context.placeableDefinition.IgnitionTemperatureCelsius) == false)
		{
			hasFuel = true;
			ignitionTemperature = context.placeableDefinition.IgnitionTemperatureCelsius;
		}

		if (target is not IItemContainer container || GameContext.Instance.ItemDB == null)
			return hasFuel;

		IReadOnlyList<ItemStack> stacks = container.Stacks;
		for (int i = 0; i < stacks.Count; ++i)
		{
			ItemStack stack = stacks[i];
			if (stack == null || stack.Quantity <= 0 || stack.HasQuality(ItemQuality.Waste) ||
				GameContext.Instance.ItemDB.GetItemData(stack.ItemID, out ItemDefinition definition) == false ||
				definition == null || float.IsPositiveInfinity(definition.IgnitionTemperatureCelsius))
			{
				continue;
			}

			hasFuel = true;
			ignitionTemperature = Mathf.Min(ignitionTemperature, definition.IgnitionTemperatureCelsius);
		}

		return hasFuel;
	}

	private bool ApplyIntensity(IGridPlaceable target, float intensity, bool showFloatingText)
	{
		if (IsAvailable(target) == false)
			return false;

		float clamped = Mathf.Clamp(intensity, 0.0f, MaximumFireIntensity);
		if (Mathf.Approximately(target.FireIntensity, clamped))
			return false;

		target.SetFireIntensity(clamped);
		if (clamped > 0.0f)
			burningTargets.Add(target);
		else
			burningTargets.Remove(target);

		if (showFloatingText && TryResolvePosition(target, out int3 position))
			PublishFireCue(in position, clamped);
		return true;
	}

	private void RemoveMissingBurningTargets()
	{
		burningScratch.Clear();
		foreach (IGridPlaceable target in burningTargets)
			burningScratch.Add(target);

		for (int i = 0; i < burningScratch.Count; ++i)
		{
			IGridPlaceable target = burningScratch[i];
			if (IsAvailable(target) == false || target.FireIntensity <= 0.0f ||
				TryResolvePosition(target, out _) == false)
			{
				burningTargets.Remove(target);
			}
		}
	}

	private void ProjectActiveFires()
	{
		GridService gridService = GridService;
		if (gridService == null || gridService.IsReady == false)
			return;

		foreach (int3 position in previousProjectedCells)
			gridService.TrySetFireIntensity(position, 0.0f);

		previousProjectedCells.Clear();
		projectedIntensities.Clear();
		foreach (IGridPlaceable target in burningTargets)
		{
			if (IsAvailable(target) == false || target.FireIntensity <= 0.0f ||
				TryResolvePosition(target, out int3 origin) == false)
			{
				continue;
			}

			ProjectIntensity(in origin, target.FireIntensity);
			for (int i = 0; i < AffectedDirections.Length; ++i)
			{
				int3 neighbor = origin + AffectedDirections[i];
				ProjectIntensity(in neighbor, target.FireIntensity * NeighborIntensityMultiplier);
			}
		}

		foreach (var projected in projectedIntensities)
		{
			gridService.TrySetFireIntensity(projected.Key, projected.Value);
			previousProjectedCells.Add(projected.Key);
		}
	}

	private void ProjectIntensity(in int3 position, float intensity)
	{
		if (GridService.GetCell(position) == null)
			return;

		if (projectedIntensities.TryGetValue(position, out float existing))
			projectedIntensities[position] = Mathf.Max(existing, intensity);
		else
			projectedIntensities[position] = intensity;
	}

	private void ApplyFireDamage()
	{
		damageTargets.Clear();
		foreach (var projected in projectedIntensities)
		{
			GridCell cell = GridService.GetCell(projected.Key);
			if (cell == null || projected.Value <= 0.0f)
				continue;

			foreach (GameObject targetObject in cell.ObjectsOnGrid)
			{
				AddDamageTarget(targetObject, projected.Value);
				if (targetObject != null && targetObject.TryGetComponent<AIWorker>(out var worker))
					AddDamageTarget(worker.CarryingAbility?.CarryingBox?.gameObject, projected.Value);
			}
		}

		foreach (var affected in damageTargets)
			ApplyDamage(affected.Key, affected.Value);
	}

	private void AddDamageTarget(GameObject targetObject, float intensity)
	{
		if (targetObject == null)
			return;

		if (damageTargets.TryGetValue(targetObject, out float existing))
			damageTargets[targetObject] = Mathf.Max(existing, intensity);
		else
			damageTargets[targetObject] = intensity;
	}

	private static void ApplyDamage(GameObject targetObject, float intensity)
	{
		if (targetObject == null || intensity <= 0.0f)
			return;

		int healthDamage = Mathf.Max(1, Mathf.RoundToInt(MaximumHealthDamagePerTick * intensity / MaximumFireIntensity));
		if (targetObject.TryGetComponent<IHealth>(out var health))
		{
			health.ApplyDamage(healthDamage);
			if (health.Health <= 0.0f && targetObject.TryGetComponent<IFacility>(out var facility) &&
				GameContext.Instance.FacilityMgr?.IsDestroyed(facility) == false)
			{
				DestroyContext destroyContext = new(DestroyContext.Destroycause.Fire);
				GameContext.Instance.FacilityMgr?.DestroyFacility(facility, in destroyContext);
			}
		}

		if (targetObject.TryGetComponent<IItemContainer>(out var container) == false)
			return;

		int3 origin = targetObject.TryGetComponent<IGridPlaceable>(out var placeable) &&
			TryResolvePosition(placeable, out int3 resolvedPosition)
			? resolvedPosition
			: default;
		int itemDamage = Mathf.Max(1, Mathf.RoundToInt(MaximumItemDamagePerTick * intensity / MaximumFireIntensity));
		IReadOnlyList<ItemStack> stacks = container.Stacks;
		for (int i = stacks.Count - 1; i >= 0; --i)
		{
			ItemStack stack = stacks[i];
			if (stack == null || stack.HasQuality(ItemQuality.Waste))
				continue;

			GameContext.Instance.ItemDamage?.TryApplyDamage(
				stack,
				itemDamage,
				in origin,
				container,
				ItemDamageCause.Fire,
				out _);
		}
	}

	private static bool TryResolvePosition(IGridPlaceable target, out int3 position)
	{
		position = default;
		if (IsAvailable(target) == false)
			return false;

		if (target is BoxBase box)
		{
			if (box.CurrentCarrier != null)
			{
				position = box.CurrentCarrier.GridPosition;
				return true;
			}

			if (box.IsPlacedOnGrid == false)
				return false;
		}

		position = target.GridPosition;
		return GridService?.GetCell(position) != null;
	}

	private static bool IsAvailable(IGridPlaceable target)
	{
		return target != null && (target is not Object unityObject || unityObject != null);
	}

	private static void PublishFireCue(in int3 position, float intensity)
	{
		if (GameContext.HasInstance == false)
			return;

		GameContext.Instance.FloatingTextManager?.ShowWorld(
			FloatingTextPreset.Error,
			$"FIRE {intensity:0}%",
			new Vector3(position.x, position.y + 1.0f, position.z));
	}
}
