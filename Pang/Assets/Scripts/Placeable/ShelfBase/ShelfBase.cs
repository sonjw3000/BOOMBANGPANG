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
	protected Dictionary<uint, int> itemTotalsTobe = new();

	protected ItemDatabase itemDB => GameContext.Instance.ItemDB;

	static private Cell[,,] GridMap => GameContext.Instance.MapResources.mapRef;

	// 각자의 manager에 의해 관리될 수 있다
	public event System.Action<ShelfBase, uint> OnItemRegistered;
	public event System.Action<ShelfBase, uint> OnItemUnregistered;

	//public int CurrentStackCount => currentStackCount;
	public IReadOnlyList<ItemStack> Stacks => stacks;
	public IReadOnlyDictionary<uint, int> ItemTotals => itemTotals;
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

		int curItemQty = itemTotals.GetValueOrDefault(itemId);

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

		int afterQty = itemTotals.GetValueOrDefault(itemId);

		itemTotalsTobe[itemId] = itemTotalsTobe.GetValueOrDefault(itemId) + remain;

		// item이 배치되었다면
		if (curItemQty == 0 && afterQty != 0)
		{
			OnItemRegistered?.Invoke(this, itemId);
		}

		return quantity - remain;
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
			itemTotalsTobe.Remove(itemId);
			OnItemUnregistered?.Invoke(this, itemId);
		}

		return quantity - remain;
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
		if (itemTotalsTobe.TryGetValue(itemId, out int val) == false)
		{
			Debug.LogError("NO ITEMS HERE");
			return quantity;
		}

		int canRemove = math.clamp(quantity, 0, itemTotalsTobe[itemId]);
		itemTotalsTobe[itemId] -= canRemove;

		return quantity - canRemove;
		//itemTotalsTobe[itemId] = itemTotalsTobe.GetValueOrDefault(itemId) + remain;
	}
}
