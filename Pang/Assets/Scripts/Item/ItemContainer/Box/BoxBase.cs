using System;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public enum BoxType
{
	None = -1,
	Cargo = 0,
	Personal = 1,
	Any = 2,
}

public abstract class BoxBase : MonoBehaviour, IItemContainer
{
	[SerializeField] BoxType boxType;
	[SerializeField] private float capacity = 10.0f;
	[SerializeField] private uint boxId = 0;
	protected float size = 0.0f;

	protected List<ItemStack> stacks = new();
	protected Dictionary<uint, int> itemTotals = new();

	protected ItemDatabase itemDB => GameContext.Instance.ItemDB;
	protected BoxPoolService BoxService => GameContext.Instance.WMSys.BoxPoolMgr;

	// totebox의 stacks는 많지 않을것으로 예상
	public float TotalSize => size;
	public float MaxSize => capacity;

	public IReadOnlyList<ItemStack> Stacks => stacks;
	public IReadOnlyDictionary<uint, int> ItemTotals => itemTotals;

	public float Capacity => capacity;
	public BoxType Type => boxType;
	public uint BoxId => boxId;

	public void SetBoxId(uint id) => boxId = id;

	public virtual void ResetContainer()
	{
		stacks.Clear();
		itemTotals.Clear();
		size = 0;
	}

	public void UpdateToteCapacity(float capacity) => this.capacity = capacity;

	private void Start()
	{
		BoxService.RegisterBox(this);
	}

	private void OnDestroy()
	{
		BoxService.UnRegisterBox(this);
	}

	public bool CanRegister() => true;

	public int GetQuantity(uint itemId)
	{
		return itemTotals.GetValueOrDefault(itemId);
	}

	public int GetAcceptableQuantity(uint itemId, int requested)
	{
		if (requested <= 0)
			return 0;

		float availableSize = capacity - size;
		float itemSize = itemDB.GetItemSize(itemId);
		if (itemSize <= 0.0f)
			return 0;

		return Mathf.Clamp(Mathf.FloorToInt(availableSize / itemSize), 0, requested);
	}

	public bool CanAcceptStack(ItemStack stack)
	{
		return stack != null && stack.Size + size <= MaxSize;
	}

	// return true when the payload fully moved
	public bool AddItem(List<ItemStack> payload)
	{
		for (int i = payload.Count - 1; i >= 0; --i) 
		{
			ItemStack stack = payload[i];

			int result = AddItem(stack.ItemID, stack.Quantity);
			stack.RemoveItem(result);

			if (stack.Quantity <= 0)
				payload.RemoveAt(i);

			itemTotals[stack.ItemID] = itemTotals.GetValueOrDefault(stack.ItemID, 0) + result;
		}

		return payload.Count <= 0;
	}

	public int AddItem(uint itemId, int quantity)
	{
		int requestedQuantity = quantity;
		float availableSize = capacity - size;
		float itemSize = itemDB.GetItemSize(itemId);

		// quantity를 줄여야한다
		if (availableSize < itemSize * quantity)
			quantity = Mathf.FloorToInt(availableSize / itemSize);

		if (quantity < 0)
		{
			Debug.LogError(
				$"[BoxBase] Negative add quantity calculated. " +
				$"BoxId={boxId}, BoxType={boxType}, ItemId={itemId}, " +
				$"Requested={requestedQuantity}, Adjusted={quantity}, " +
				$"Capacity={capacity}, CurrentSize={size}, AvailableSize={availableSize}, ItemSize={itemSize}, " +
				$"Stacks={BuildStackDebugText()}");
		}

		// 0이면 불필요한 로직을 타지 않게
		if (quantity == 0)
		{
			return 0;
		}

		ItemStack stack = stacks.Find(id => id.ItemID == itemId);

		if (stack == null)
		{
			stack = new ItemStack(itemId, this.capacity);
			stacks.Add(stack);
		}

		int res = stack.AddItem(quantity);

		if (stack.Quantity < 0)
		{
			Debug.LogError(
				$"[BoxBase] Stack quantity is negative after AddItem. " +
				$"BoxId={boxId}, BoxType={boxType}, ItemId={itemId}, " +
				$"Requested={requestedQuantity}, Adjusted={quantity}, Added={res}, " +
				$"Capacity={capacity}, CurrentSize={size}, StackQuantity={stack.Quantity}, " +
				$"Stacks={BuildStackDebugText()}");
		}

		itemTotals[itemId] = itemTotals.GetValueOrDefault(itemId, 0) + res;

		UpdateSize();

		return res;
	}

	public int RemoveItem(uint itemId, int quantity)
	{
		ItemStack stack = stacks.Find(id => id.ItemID == itemId);

		if (stack == null)
			return 0;

		int res = stack.RemoveItem(quantity);

		if (stack.Quantity < 0)
		{
			Debug.LogError(
				$"[BoxBase] Stack quantity is negative after RemoveItem. " +
				$"BoxId={boxId}, BoxType={boxType}, ItemId={itemId}, Requested={quantity}, Removed={res}, " +
				$"Capacity={capacity}, CurrentSize={size}, StackQuantity={stack.Quantity}, " +
				$"Stacks={BuildStackDebugText()}");
		}

		if (stack.Quantity <= 0)
		{
			stacks.Remove(stack);
		}

		itemTotals[itemId] = itemTotals.GetValueOrDefault(itemId, 0) - res;

		UpdateSize();

		return res;
	}

	private string BuildStackDebugText()
	{
		if (stacks == null || stacks.Count == 0)
			return "empty";

		List<string> stackTexts = new(stacks.Count);
		foreach (ItemStack stack in stacks)
		{
			if (stack == null)
				continue;

			stackTexts.Add($"{stack.ItemID}x{stack.Quantity}");
		}

		return string.Join(", ", stackTexts);
	}

	public bool AddStack(ItemStack stack)
	{
		if (CanAcceptStack(stack) == false)
			return false;

		stacks.Add(stack);
		itemTotals[stack.ItemID] = itemTotals.GetValueOrDefault(stack.ItemID) + stack.Quantity;

		UpdateSize();

		return true;
	}

	public bool RemoveStack(ItemStack stack)
	{
		if (stack == null || stacks.Remove(stack) == false)
			return false;

		itemTotals[stack.ItemID] = itemTotals.GetValueOrDefault(stack.ItemID) - stack.Quantity;
		if (itemTotals[stack.ItemID] <= 0)
			itemTotals.Remove(stack.ItemID);

		UpdateSize();

		return true;
	}

	// pallet같은 경우에는 소유한 pallet들의 capacity들을 합쳐야하기 때문에
	protected abstract void UpdateSize();

	public virtual BoxSaveData CaptureState(Func<OrderLine, int> registerOrderLine)
	{
		uint resolvedBoxId = boxId > 0 ? boxId : (GameContext.HasInstance ? BoxService.GetOrCreateBoxId(this) : 0);
		BoxSaveData data = new BoxSaveData
		{
			BoxId = resolvedBoxId,
			BoxType = boxType,
			ConcreteType = GetType().Name,
		};

		foreach (var stack in stacks)
		{
			if (stack is ItemPackage pkg)
			{
				data.Stacks.Add(new ItemStackSaveData
				{
					ItemId = pkg.ItemID,
					Quantity = pkg.Quantity,
					IsPackage = true,
					RelatedOrderLineId = registerOrderLine != null ? registerOrderLine(pkg.RelatedOrderLine) : -1,
					OutboundStage = pkg.OutboundStage,
				});
			}
			else
			{
				data.Stacks.Add(new ItemStackSaveData
				{
					ItemId = stack.ItemID,
					Quantity = stack.Quantity,
				});
			}
		}

		return data;
	}

	public virtual void RestoreState(BoxSaveData data, IReadOnlyDictionary<int, OrderLine> orderLines)
	{
		ResetContainer();
		if (data == null)
			return;

		foreach (var stackData in data.Stacks)
		{
			if (stackData.IsPackage &&
				orderLines != null &&
				orderLines.TryGetValue(stackData.RelatedOrderLineId, out var line))
			{
				AddStack(new ItemPackage(PackingType.Box, line, stackData.ItemId, stackData.Quantity, stackData.OutboundStage));
			}
			else
			{
				AddItem(stackData.ItemId, stackData.Quantity);
			}
		}
	}

}
