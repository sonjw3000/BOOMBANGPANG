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

	public bool WasApplied => CurrentDamage > PreviousDamage;

	public ItemDamageChange(
		uint itemId,
		int affectedQuantity,
		byte previousDamage,
		byte currentDamage,
		ItemDamageCause cause)
	{
		ItemId = itemId;
		AffectedQuantity = affectedQuantity;
		PreviousDamage = previousDamage;
		CurrentDamage = currentDamage;
		Cause = cause;
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
	public event Action<ItemDamageIncidentTrigger> OnIncidentTriggered;

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
			ItemDamageCause.Debug);
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
		if (TryApplyDamageValue(stack, damageIncrease, cause, out damageChange) == false)
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
		byte currentDamage = (byte)Mathf.Clamp(previousDamage + damageIncrease, 0, 100);
		if (currentDamage <= previousDamage)
			return false;

		stack.SetDamage(currentDamage);
		damageChange = new ItemDamageChange(
			stack.ItemID,
			stack.Quantity,
			previousDamage,
			currentDamage,
			cause);
		return true;
	}
}
