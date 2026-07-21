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
	Debug,
}

public readonly struct ItemDamageChange
{
	public readonly uint ItemId;
	public readonly int AffectedQuantity;
	public readonly byte PreviousDamage;
	public readonly byte CurrentDamage;
	public readonly ItemDamageCause Cause;
	public readonly ItemQuality PreviousQuality;
	public readonly ItemQuality CurrentQuality;

	public bool WasApplied => CurrentDamage > PreviousDamage;
	public bool QualityChanged => CurrentQuality != PreviousQuality;

	public ItemDamageChange(
		uint itemId,
		int affectedQuantity,
		byte previousDamage,
		byte currentDamage,
		ItemDamageCause cause,
		ItemQuality previousQuality,
		ItemQuality currentQuality)
	{
		ItemId = itemId;
		AffectedQuantity = affectedQuantity;
		PreviousDamage = previousDamage;
		CurrentDamage = currentDamage;
		Cause = cause;
		PreviousQuality = previousQuality;
		CurrentQuality = currentQuality;
	}
}

public readonly struct ItemDamageIncidentTrigger
{
	public readonly ItemDamageIncidentDefinition Definition;
	public readonly ItemDamageChange DamageChange;
	public readonly int3 OriginCell;
	public readonly IItemContainer Container;

	public ItemDamageIncidentType IncidentType => Definition.IncidentType;
	public int Radius => Definition.Radius;
	public int Severity => Definition.Severity;
	public int EdgeDamagePercent => Definition.EdgeDamagePercent;
	public int TriggerDelayTicks => Definition.TriggerDelayTicks;

	public ItemDamageIncidentTrigger(
		ItemDamageIncidentDefinition definition,
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

	public int CalculateDamageIncrease(ItemStack stack, int baseDamage, ItemDamageCause cause)
	{
		if (stack == null || baseDamage <= 0)
			return 0;

		float multiplier = 1.0f;
		if (cause == ItemDamageCause.Explosion && IsFragile(stack.ItemID))
			multiplier = Mathf.Max(1.0f, fragileExplosionDamageMultiplier);

		return Mathf.Clamp(Mathf.RoundToInt(baseDamage * multiplier), 0, 100);
	}

	public bool WouldTriggerIncident(
		ItemStack stack,
		int baseDamage,
		ItemDamageCause cause,
		ItemDamageIncidentType incidentType)
	{
		if (stack == null || stack.Quantity <= 0 || stack.Damage >= 100)
			return false;

		int damageIncrease = CalculateDamageIncrease(stack, baseDamage, cause);
		if (damageIncrease <= 0 || TryGetItemDefinition(stack.ItemID, out ItemDefinition itemDefinition) == false)
			return false;

		int predictedDamage = Mathf.Clamp(stack.Damage + damageIncrease, 0, 100);
		IReadOnlyList<ItemDamageIncidentDefinition> incidents = itemDefinition.DamageIncidents;
		if (incidents == null)
			return false;

		for (int i = 0; i < incidents.Count; ++i)
		{
			ItemDamageIncidentDefinition incident = incidents[i];
			if (incident != null &&
				incident.IncidentType == incidentType &&
				stack.Damage < incident.TriggerDamage &&
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

		byte previousDamage = stack.Damage;
		byte currentDamage = (byte)Mathf.Clamp(targetDamage, 0, 100);
		if (currentDamage == previousDamage)
			return false;

		if (currentDamage > previousDamage)
		{
			return TryApplyDamage(
				stack,
				currentDamage - previousDamage,
				in originCell,
				container,
				ItemDamageCause.Debug,
				out damageChange);
		}

		stack.SetDamage(currentDamage);
		damageChange = new ItemDamageChange(
			stack.ItemID,
			stack.Quantity,
			previousDamage,
			currentDamage,
			ItemDamageCause.Debug,
			stack.Quality,
			stack.Quality);
		return true;
	}

	public bool TryApplyDamage(
		ItemStack stack,
		int damageIncrease,
		in int3 originCell,
		IItemContainer container,
		ItemDamageCause cause,
		out ItemDamageChange damageChange)
	{
		damageChange = default;
		int adjustedDamageIncrease = CalculateDamageIncrease(stack, damageIncrease, cause);
		if (TryApplyDamageValue(stack, adjustedDamageIncrease, cause, out damageChange) == false)
			return false;

		CommitDamage(in damageChange, in originCell, container);
		return true;
	}

	public bool TryCreateDamagedStack(
		ItemStack sourceStack,
		int quantity,
		int damageIncrease,
		ItemDamageCause cause,
		out ItemStack damagedStack,
		out ItemDamageChange damageChange)
	{
		damagedStack = null;
		damageChange = default;
		if (sourceStack == null || quantity <= 0 || damageIncrease <= 0 || sourceStack.Damage >= 100)
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

		if (damageChange.QualityChanged)
			GameContext.Instance.BuildingMgr?.RefreshItemContainerState(container);

		ItemDatabase itemDatabase = GameContext.Instance.ItemDB;
		if (itemDatabase == null ||
			itemDatabase.GetItemData(damageChange.ItemId, out ItemDefinition itemDefinition) == false ||
			itemDefinition == null)
		{
			return;
		}

		IReadOnlyList<ItemDamageIncidentDefinition> incidents = itemDefinition.DamageIncidents;
		if (incidents == null)
			return;

		for (int i = 0; i < incidents.Count; ++i)
		{
			ItemDamageIncidentDefinition incident = incidents[i];
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

	private static bool IsFragile(uint itemId)
	{
		return TryGetItemDefinition(itemId, out ItemDefinition itemDefinition) &&
			(itemDefinition.Tag & ItemTag.Fragile) != 0;
	}

	private static bool TryApplyDamageValue(
		ItemStack stack,
		int damageIncrease,
		ItemDamageCause cause,
		out ItemDamageChange damageChange)
	{
		damageChange = default;
		if (stack == null || stack.Quantity <= 0 || damageIncrease <= 0 || stack.Damage >= 100)
			return false;

		byte previousDamage = stack.Damage;
		ItemQuality previousQuality = stack.Quality;
		byte currentDamage = (byte)Mathf.Clamp(previousDamage + damageIncrease, 0, 100);
		if (currentDamage <= previousDamage)
			return false;

		stack.SetDamage(currentDamage);
		if (currentDamage >= 100 && previousDamage < 100)
			stack.AddQuality(ItemQuality.Waste);

		damageChange = new ItemDamageChange(
			stack.ItemID,
			stack.Quantity,
			previousDamage,
			currentDamage,
			cause,
			previousQuality,
			stack.Quality);
		return true;
	}
}
