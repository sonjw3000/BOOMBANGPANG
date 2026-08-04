using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

public enum ItemDamageCause
{
	Unknown,
	Handling,
	HardLanding,
	Explosion,
	Fire,
	Contamination,
	Corrosion,
	Radiation,
	Freezing,
	Overheating,
	Debug,
}

public readonly struct ItemDamageChange
{
	public readonly uint ItemId;
	public readonly int AffectedQuantity;
	public readonly float PreviousIntegrity;
	public readonly float CurrentIntegrity;
	public readonly float PreviousDamage;
	public readonly float CurrentDamage;
	public readonly ItemDamageCause Cause;
	public readonly ItemQuality PreviousQuality;
	public readonly ItemQuality CurrentQuality;

	public bool WasApplied => CurrentIntegrity < PreviousIntegrity;
	public bool QualityChanged => CurrentQuality != PreviousQuality;

	public ItemDamageChange(
		uint itemId,
		int affectedQuantity,
		float previousIntegrity,
		float currentIntegrity,
		float previousDamage,
		float currentDamage,
		ItemDamageCause cause,
		ItemQuality previousQuality,
		ItemQuality currentQuality)
	{
		ItemId = itemId;
		AffectedQuantity = affectedQuantity;
		PreviousIntegrity = previousIntegrity;
		CurrentIntegrity = currentIntegrity;
		PreviousDamage = previousDamage;
		CurrentDamage = currentDamage;
		Cause = cause;
		PreviousQuality = previousQuality;
		CurrentQuality = currentQuality;
	}
}

public readonly struct ItemDamageIncidentTrigger
{
	public readonly DamageIncidentDefinition Definition;
	public readonly ItemDamageChange DamageChange;
	public readonly int3 OriginCell;
	public readonly IItemContainer Container;

	public ItemDamageIncidentType IncidentType => Definition.IncidentType;
	public int Radius => Definition.Radius;
	public int Severity => Definition.Severity;
	public int EdgeDamagePercent => Definition.EdgeDamagePercent;
	public int TriggerDelayTicks => Definition.TriggerDelayTicks;

	public ItemDamageIncidentTrigger(
		DamageIncidentDefinition definition,
		in ItemDamageChange damageChange,
		in int3 originCell,
		IItemContainer container)
	{
		Definition = definition;
		DamageChange = damageChange;
		OriginCell = originCell;
		Container = container;
	}
}

public class ItemDamageService : MonoBehaviour
{
	[SerializeField, Min(1.0f)] private float fragileExplosionDamageMultiplier = 1.5f;

	public event Action<ItemDamageIncidentTrigger> OnIncidentTriggered;

	public float CalculateDamageIncrease(ItemStack stack, float baseDamage, ItemDamageCause cause)
	{
		if (stack == null || baseDamage <= 0)
			return 0.0f;

		float multiplier = 1.0f;
		if (cause == ItemDamageCause.Explosion && IsFragile(stack.ItemID))
			multiplier = Mathf.Max(1.0f, fragileExplosionDamageMultiplier);

		return Mathf.Max(0.0f, baseDamage * multiplier);
	}

	public bool WouldTriggerIncident(
		ItemStack stack,
		float baseDamage,
		ItemDamageCause cause,
		ItemDamageIncidentType incidentType)
	{
		if (stack == null || stack.Quantity <= 0 || stack.IsDestroyed)
			return false;

		float damageIncrease = CalculateDamageIncrease(stack, baseDamage, cause);
		if (damageIncrease <= 0 || TryGetItemDefinition(stack.ItemID, out ItemDefinition itemDefinition) == false)
			return false;

		float previousDamage = stack.DamageRatio * 100.0f;
		float predictedIntegrity = Mathf.Max(0.0f, stack.CurrentIntegrity - damageIncrease);
		float predictedDamage = Mathf.Clamp01(
			(stack.MaximumIntegrity - predictedIntegrity) / stack.MaximumIntegrity) * 100.0f;
		IReadOnlyList<DamageIncidentDefinition> incidents = itemDefinition.DamageIncidents;
		if (incidents == null)
			return false;

		for (int i = 0; i < incidents.Count; ++i)
		{
			DamageIncidentDefinition incident = incidents[i];
			if (incident != null &&
				incident.IncidentType == incidentType &&
				previousDamage < incident.TriggerDamage &&
				predictedDamage >= incident.TriggerDamage)
			{
				return true;
			}
		}

		return false;
	}

	public bool TrySetDebugDamage(
		ItemStack stack,
		int targetDamage,
		in int3 originCell,
		IItemContainer container,
		out ItemDamageChange damageChange)
	{
		damageChange = default;
		if (stack == null || stack.Quantity <= 0)
			return false;

		float previousIntegrity = stack.CurrentIntegrity;
		float previousDamage = stack.DamageRatio * 100.0f;
		float targetDamagePercent = Mathf.Clamp(targetDamage, 0, 100);
		float targetIntegrity = stack.MaximumIntegrity * (1.0f - targetDamagePercent / 100.0f);
		if (Mathf.Approximately(targetIntegrity, previousIntegrity))
			return false;

		if (targetIntegrity < previousIntegrity)
		{
			return TryApplyDamage(
				stack,
				previousIntegrity - targetIntegrity,
				in originCell,
				container,
				ItemDamageCause.Debug,
				out damageChange);
		}

		stack.SetCurrentIntegrity(targetIntegrity);
		damageChange = new ItemDamageChange(
			stack.ItemID,
			stack.Quantity,
			previousIntegrity,
			stack.CurrentIntegrity,
			previousDamage,
			stack.DamageRatio * 100.0f,
			ItemDamageCause.Debug,
			stack.Quality,
			stack.Quality);
		return true;
	}

	public bool TryApplyDamage(
		ItemStack stack,
		float damageIncrease,
		in int3 originCell,
		IItemContainer container,
		ItemDamageCause cause,
		out ItemDamageChange damageChange)
	{
		damageChange = default;
		float adjustedDamageIncrease = CalculateDamageIncrease(stack, damageIncrease, cause);
		if (TryApplyDamageValue(stack, adjustedDamageIncrease, cause, out damageChange) == false)
			return false;

		CommitDamage(in damageChange, in originCell, container);
		return true;
	}

	public bool TryCreateDamagedStack(
		ItemStack sourceStack,
		int quantity,
		float damageIncrease,
		ItemDamageCause cause,
		out ItemStack damagedStack,
		out ItemDamageChange damageChange)
	{
		damagedStack = null;
		damageChange = default;
		if (sourceStack == null || quantity <= 0 || damageIncrease <= 0.0f || sourceStack.IsDestroyed)
			return false;

		damagedStack = sourceStack.CloneWithQuantity(quantity);
		if (damagedStack == null)
			return false;

		if (TryApplyDamageValue(damagedStack, damageIncrease, cause, out damageChange))
			return true;

		damagedStack.Recycle();
		damagedStack = null;
		return false;
	}

	public void CommitDamage(
		in ItemDamageChange damageChange,
		in int3 originCell,
		IItemContainer container)
	{
		if (damageChange.WasApplied == false || GameContext.HasInstance == false)
			return;

		OutboundWorkflowService outbound = GameContext.Instance.OBWorkflowSvc;
		int previousDamagePercent = CalculateDamagePercent(damageChange.PreviousDamage);
		int currentDamagePercent = CalculateDamagePercent(damageChange.CurrentDamage);
		bool crossedOutboundThreshold = outbound != null &&
			outbound.OutboundQualityControlEnabled &&
			previousDamagePercent <= outbound.MaximumOutboundDamagePercent &&
			currentDamagePercent > outbound.MaximumOutboundDamagePercent;
		if (damageChange.QualityChanged || crossedOutboundThreshold)
			GameContext.Instance.BuildingMgr?.RefreshItemContainerState(container);

		ItemDatabase itemDatabase = GameContext.Instance.ItemDB;
		if (itemDatabase == null ||
			itemDatabase.GetItemData(damageChange.ItemId, out ItemDefinition itemDefinition) == false ||
			itemDefinition == null)
		{
			return;
		}

		IReadOnlyList<DamageIncidentDefinition> incidents = itemDefinition.DamageIncidents;
		if (incidents == null)
			return;

		for (int i = 0; i < incidents.Count; ++i)
		{
			DamageIncidentDefinition incident = incidents[i];
			if (incident == null ||
				damageChange.PreviousDamage >= incident.TriggerDamage ||
				damageChange.CurrentDamage < incident.TriggerDamage)
			{
				continue;
			}

			ItemDamageIncidentTrigger trigger = new(incident, in damageChange, in originCell, container);
			RouteIncident(in trigger);
			OnIncidentTriggered?.Invoke(trigger);
		}
	}

	private static void RouteIncident(in ItemDamageIncidentTrigger trigger)
	{
		if (GameContext.HasInstance == false)
			return;

		switch (trigger.IncidentType)
		{
			case ItemDamageIncidentType.Fire:
				FireService fireService = GameContext.Instance.FireSvc;
				if (fireService != null)
					fireService.ReportTrigger(in trigger);
				break;

			case ItemDamageIncidentType.Explosion:
				ExplosionService explosionService = GameContext.Instance.ExplosionSvc;
				if (explosionService != null)
					explosionService.Enqueue(in trigger);
				break;

			case ItemDamageIncidentType.Contamination:
				ContaminationService contaminationService = GameContext.Instance.ContaminationSvc;
				if (contaminationService != null)
					contaminationService.ReportTrigger(in trigger);
				break;

			case ItemDamageIncidentType.Corrosion:
				CorrosionService corrosionService = GameContext.Instance.CorrosionSvc;
				if (corrosionService != null)
					corrosionService.ReportTrigger(in trigger);
				break;

			case ItemDamageIncidentType.RadiationLeak:
				RadiationService radiationService = GameContext.Instance.RadiationSvc;
				if (radiationService != null)
					radiationService.ReportTrigger(in trigger);
				break;
		}
	}

	private static bool TryGetItemDefinition(uint itemId, out ItemDefinition itemDefinition)
	{
		itemDefinition = null;
		return GameContext.HasInstance &&
			GameContext.Instance.ItemDB != null &&
			GameContext.Instance.ItemDB.GetItemData(itemId, out itemDefinition) &&
			itemDefinition != null;
	}

	private static int CalculateDamagePercent(float damagePercent)
	{
		return Mathf.Clamp(
			Mathf.FloorToInt(Mathf.Clamp(damagePercent, 0.0f, 100.0f) + 0.0001f),
			0,
			100);
	}

	private static bool IsFragile(uint itemId)
	{
		return TryGetItemDefinition(itemId, out ItemDefinition itemDefinition) &&
			(itemDefinition.Tag & ItemTag.Fragile) != 0;
	}

	private static bool TryApplyDamageValue(
		ItemStack stack,
		float damageIncrease,
		ItemDamageCause cause,
		out ItemDamageChange damageChange)
	{
		damageChange = default;
		if (stack == null || stack.Quantity <= 0 || damageIncrease <= 0.0f || stack.IsDestroyed)
			return false;

		float previousIntegrity = stack.CurrentIntegrity;
		float previousDamage = stack.DamageRatio * 100.0f;
		ItemQuality previousQuality = stack.Quality;
		float appliedDamage = stack.ApplyIntegrityDamage(damageIncrease);
		if (appliedDamage <= 0.0f)
			return false;

		float currentDamage = stack.DamageRatio * 100.0f;
		if (stack.IsDestroyed && previousIntegrity > 0.0f)
			stack.AddQuality(ItemQuality.Waste);

		damageChange = new ItemDamageChange(
			stack.ItemID,
			stack.Quantity,
			previousIntegrity,
			stack.CurrentIntegrity,
			previousDamage,
			currentDamage,
			cause,
			previousQuality,
			stack.Quality);
		return true;
	}
}
