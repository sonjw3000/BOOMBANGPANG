using System.Linq;
using System.Collections.Generic;
using UnityEngine;

public enum CapsuleLogisticsState
{
	IB = 0,
	Inside = 1,
	Empty = 2,
	OB = 3,
}

public enum CargoRouteKind
{
	Standard,
	Waste,
}

public class CargoCapsule : BoxBase
{
	public event System.Action OnQuantityChanged;
	public event System.Action OnContentStateChanged;
	public event System.Action<CargoCapsule> OnLogisticsStateChanged;

	[SerializeField] private CapsuleLogisticsState logisticsState = CapsuleLogisticsState.IB;
	private CapsuleDock currentDock;
	private readonly HashSet<ItemStack> observedStacks = new();

	public CapsuleLogisticsState LogisticsState => logisticsState;
	public virtual CargoRouteKind RouteKind => CargoRouteKind.Standard;
	public CapsuleDock CurrentDock => currentDock;
	public CapsuleBuffer CurrentBuffer => currentDock as CapsuleBuffer;

	public void SetLogisticsState(CapsuleLogisticsState newState)
	{
		if (logisticsState == newState)
			return;

		logisticsState = newState;
		OnLogisticsStateChanged?.Invoke(this);
	}

	public void SetCurrentDock(CapsuleDock dock)
	{
		if (dock != null)
			SyncObservedStacks();

		currentDock = dock;
	}

	public override void ResetContainer()
	{
		UnsubscribeObservedStacks();
		base.ResetContainer();
	}

	public void ApplyDamage(int damageRate, int maximumDamageAmount)
	{
		int totalQuantity = 0;
		for (int i = 0; i < stacks.Count; ++i)
		{
			ItemStack stack = stacks[i];
			if (stack == null || stack.Quantity <= 0)
				continue;

			totalQuantity += stack.Quantity;
		}

		damageRate = Mathf.Clamp(damageRate, 0, 100);
		if (totalQuantity <= 0 || damageRate <= 0)
			return;

		maximumDamageAmount = Mathf.Clamp(maximumDamageAmount, 10, 100);

		int damageRateRoll = Random.Range(1, damageRate + 1);
		int damageAmount = Random.Range(10, maximumDamageAmount + 1);
		int targetQuantity = Mathf.Clamp(
			Mathf.CeilToInt(totalQuantity * (damageRateRoll / 100.0f)),
			1,
			totalQuantity);

		ApplyDamageToStacks(targetQuantity, damageAmount);
		MergeMatchingStacks();
		UpdateSize();
	}

	private void ApplyDamageToStacks(int targetQuantity, int damageAmount)
	{
		if (targetQuantity <= 0 || damageAmount <= 0 || GameContext.HasInstance == false)
			return;

		ItemDamageService itemDamageService = GameContext.Instance.ItemDamage;
		if (itemDamageService == null || TryGetDamageOrigin(out Unity.Mathematics.int3 originCell) == false)
			return;

		List<ItemStack> targets = new(stacks.Count);
		for (int i = 0; i < stacks.Count; ++i)
		{
			ItemStack stack = stacks[i];
			if (stack != null && stack.Quantity > 0)
				targets.Add(stack);
		}

		for (int i = targets.Count - 1; i > 0; --i)
		{
			int swapIndex = Random.Range(0, i + 1);
			ItemStack temp = targets[i];
			targets[i] = targets[swapIndex];
			targets[swapIndex] = temp;
		}

		for (int i = 0; i < targets.Count && targetQuantity > 0; ++i)
		{
			ItemStack stack = targets[i];
			if (stack == null || stack.Quantity <= 0)
				continue;

			int appliedQuantity = Mathf.Min(targetQuantity, stack.Quantity);
			if (appliedQuantity >= stack.Quantity)
			{
				itemDamageService.TryApplyDamage(
					stack,
					damageAmount,
					in originCell,
					this,
					ItemDamageCause.HardLanding,
					out _);
			}
			else
			{
				ItemStack damagedStack = stack.Split(appliedQuantity);
				if (damagedStack == null)
					continue;

				stacks.Add(damagedStack);
				itemDamageService.TryApplyDamage(
					damagedStack,
					damageAmount,
					in originCell,
					this,
					ItemDamageCause.HardLanding,
					out _);
			}

			targetQuantity -= appliedQuantity;
		}
	}

	private bool TryGetDamageOrigin(out Unity.Mathematics.int3 originCell)
	{
		if (currentDock is IGridPlaceable gridPlaceable)
		{
			originCell = gridPlaceable.GridPosition;
			return true;
		}

		originCell = default;
		return false;
	}

	private void MergeMatchingStacks()
	{
		for (int i = stacks.Count - 1; i >= 0; --i)
		{
			ItemStack source = stacks[i];
			if (source == null)
			{
				stacks.RemoveAt(i);
				continue;
			}

			if (source.Quantity <= 0)
			{
				stacks.RemoveAt(i);
				source.Recycle();
				continue;
			}

			for (int j = 0; j < i; ++j)
			{
				ItemStack target = stacks[j];
				if (target == null || target.Quantity <= 0)
					continue;

				if (target.TryMergeFrom(source) == false)
					continue;

				if (source.Quantity <= 0)
				{
					stacks.RemoveAt(i);
					source.Recycle();
				}

				break;
			}
		}
	}

	protected override void UpdateSize()
	{
		size = stacks.Sum(s => itemDB.GetItemSize(s.ItemID) * s.Quantity);
		RebuildItemTags();
		SyncObservedStacks();
		OnQuantityChanged?.Invoke();
	}

	private void SyncObservedStacks()
	{
		ItemStack[] observed = observedStacks.ToArray();
		for (int i = 0; i < observed.Length; ++i)
		{
			ItemStack stack = observed[i];
			if (stack != null && stacks.Contains(stack))
				continue;

			if (stack != null)
				stack.OnStateChanged -= HandleStackStateChanged;
			observedStacks.Remove(stack);
		}

		for (int i = 0; i < stacks.Count; ++i)
		{
			ItemStack stack = stacks[i];
			if (stack == null || observedStacks.Add(stack) == false)
				continue;

			stack.OnStateChanged += HandleStackStateChanged;
		}
	}

	private void UnsubscribeObservedStacks()
	{
		foreach (ItemStack stack in observedStacks)
		{
			if (stack != null)
				stack.OnStateChanged -= HandleStackStateChanged;
		}

		observedStacks.Clear();
	}

	private void HandleStackStateChanged(ItemStack _)
	{
		OnContentStateChanged?.Invoke();
	}
}
