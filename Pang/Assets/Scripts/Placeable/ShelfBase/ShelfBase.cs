using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

public abstract class ShelfBase : 
	MonoBehaviour, 
	IItemContainer, 
	IGridPlaceable, 
	IGridPlacementEffect,
	IInteractionPoint
{
	[SerializeField] protected int maxStacks = 16;
	[SerializeField] protected float sizePerStack;

	//private int currentStackCount;
	private int3 position;
	protected List<int3> interactionPoints = new();

	protected List<ItemStack> stacks;
	protected Dictionary<uint, int> itemTotals = new();
	protected Dictionary<uint, int> itemsReservedPick = new();

	protected ItemDatabase itemDB => GameContext.Instance.ItemDB;

	static private Cell[,,] GridMap => GameContext.Instance.MapResources.mapRef;

	// item의 종류가 등록/해제 되었을 경우
	public event System.Action<ShelfBase, uint, bool> OnItemPresentChanged;

	// item quantity의 변경이 일어났을 경우
	public event System.Action<uint, int> OnItemQuantityChanged;

	//public int CurrentStackCount => currentStackCount;
	public IReadOnlyList<ItemStack> Stacks => stacks;
	public IReadOnlyDictionary<uint, int> ItemTotals => itemTotals;
	public IReadOnlyDictionary<uint, int> ItemToBePicked => itemsReservedPick;
	public bool CanRegister() => MaxStack > Stacks.Count;
	public float MaxStack => maxStacks;

	public int3 GridPosition => position;
	public IReadOnlyList<int3> InteractionPoints => interactionPoints;

	protected virtual void Awake()
	{
		stacks = new List<ItemStack>(capacity: maxStacks);
	}

	// 가장 쉬운 숫자는?
	// 190,000

	// 식인종이 우사인볼트를 보면?
	// 패스트푸드

	public int AddItem(uint itemId, int quantity)
	{
		if (quantity <= 0)
			return 0;

		int befItemCounts = itemTotals.GetValueOrDefault(itemId);
		int befItemStacks = stacks.Count;

		// 기존 인덱스에 넣기
		int remain = quantity;
		for (int i = 0; i < stacks.Count; ++i) 
		{
			ItemStack stack = stacks[i];

			if (stack.ItemID != itemId)
				continue;

			int itemAdded = stack.AddItem(remain);
			itemTotals[itemId] = itemTotals.GetValueOrDefault(itemId) + itemAdded;
			remain -= itemAdded;

			if (remain <= 0) break;
		}

		// 기존 인덱스가 없다면 새로 만들어 채우기
		while (remain > 0 && stacks.Count < maxStacks)
		{
			ItemStack stack = new ItemStack(itemId, sizePerStack);
			stacks.Add(stack);

			int itemAdded = stack.AddItem(remain);
			itemTotals[itemId] = itemTotals.GetValueOrDefault(itemId) + itemAdded;
			remain -= itemAdded;
		}

		int curItemStacks = stacks.Count;

		// 새로운 종류의 stack이 register 되었다면?
		if (befItemCounts == 0 && befItemStacks != curItemStacks)
		{
			OnItemPresentChanged?.Invoke(this, itemId, true);
		}

		int addedItem = quantity - remain;
		OnItemQuantityChanged?.Invoke(itemId, addedItem);

		return addedItem;
	}

	public int RemoveItem(uint itemId, int quantity)
	{
		if (quantity <= 0)
			return 0;

		int remain = quantity;

		for (int i = stacks.Count - 1; i >= 0; --i)
		{
			ItemStack stack = stacks[i];

			if (stack.ItemID != itemId)
				continue;
			
			int itemRemoved = stack.RemoveItem(remain);
			itemTotals[itemId] = itemTotals.GetValueOrDefault(itemId) - itemRemoved;
			remain -= itemRemoved;
			if (stack.Quantity <= 0)
				stacks.RemoveAt(i);
		}

		// 아이템이 사라졌다면
		if (itemTotals.TryGetValue(itemId, out int value) && value == 0)
		{
			itemTotals.Remove(itemId);
			OnItemPresentChanged?.Invoke(this, itemId, false);
		}

		int removed = quantity - remain;

		// adjust tobepicked
		itemsReservedPick[itemId] = itemsReservedPick.GetValueOrDefault(itemId) - removed;
		if (itemsReservedPick[itemId] <= 0)
		{
			// 0보다 작은 경우의 에러를 체크해보자
			if (itemsReservedPick[itemId] < 0)
				Debug.LogWarning("Reserved pick count went below zero. Adjusting to zero.");

			itemsReservedPick.Remove(itemId);
		}

		OnItemQuantityChanged?.Invoke(itemId, -removed);

		return removed;
	}

	public bool CanAccept(uint itemId, int quantity)
	{
		int capacity = 0;
		float itemSize = itemDB.GetItemSize(itemId);

		for (int i = 0; i < stacks.Count; ++i)
		{
			ItemStack stack = stacks[i];
			if (stack.ItemID == itemId)
				capacity += stack.AvailableAmount;
		}

		int freeslots = maxStacks - stacks.Count;
		capacity += freeslots * (int)(sizePerStack / itemSize);

		return capacity >= quantity;
	}

	protected abstract void SetInteractionPoints();

	public void OnRemoved()
	{
		foreach (int3 interPos in interactionPoints)
		{
			// pickingposition위에 아무것도 없는 경우엔 삭제, 뭔가 있다 == 로봇이 올라가 있다 -> 삭제하면 안됨
			Cell cell = GridMap[interPos.x, interPos.y, interPos.z];
			if (cell.type < 0)
			{
				cell.type = 0;
			}
			cell.previousType = 0;
		}

		Cell thisPos = GridMap[position.x, position.y, position.z];
		thisPos.type = thisPos.previousType;
	}

	public void OnPositionSet(in int3 position)
	{
		enabled = true;

		// set position
		this.position = position;

		// set pickingPosition
		SetInteractionPoints();
		foreach (int3 pickingPos in interactionPoints)
		{
			Cell pickPos = GridMap[pickingPos.x, pickingPos.y, pickingPos.z];

			// set picking position's tile -1
			if (pickPos.type == 0)
			{
				pickPos.type = -1;
			}
			pickPos.previousType = -1;
		}
	}

	public void OnDestroyedBy(in DestroyContext ctx)
	{
		// 부셔지면 뭐 본인이 가진 아이템을 뭐시기 해야함
		// 근데 지가 로켓이면 뭐 로켓이 로케트 부순거니까
		// 근데 로케트의 아이템은 인벤토리에서 관리를 안해
		// 제가 꽁꽁 숨겨뒀으니 찾아보세요
	}

	public int ReservePicking(uint itemId, int quantity)
	{
		if (itemTotals.TryGetValue(itemId, out int val) == false)
		{
			Debug.LogError("NO ITEMS HERE");
			return quantity;
		}

		int befReserved= itemsReservedPick.GetValueOrDefault(itemId);

		int canReserve = math.clamp(quantity, 0, val - befReserved);
		itemsReservedPick[itemId] = befReserved + canReserve;

		return canReserve;
	}
}
