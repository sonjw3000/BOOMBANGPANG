using System;
using System.Collections.Generic;
using UnityEngine;

public readonly struct BuildingItemKey : IEquatable<BuildingItemKey>
{
	public readonly uint ItemId;
	public readonly ItemStatus Status;

	public BuildingItemKey(uint itemId, ItemStatus status)
	{
		ItemId = itemId;
		Status = status;
	}

	public bool Equals(BuildingItemKey other)
	{
		return ItemId == other.ItemId && Status == other.Status;
	}

	public override bool Equals(object obj)
	{
		return obj is BuildingItemKey other && Equals(other);
	}

	public override int GetHashCode()
	{
		return HashCode.Combine(ItemId, Status);
	}

	public override string ToString()
	{
		return $"{ItemId}:{Status}";
	}
}

public sealed class BuildingItemIndex
{
	private sealed class ContainerSnapshot
	{
		public readonly Dictionary<BuildingItemKey, int> Quantities = new();
		public readonly Dictionary<BuildingItemKey, int> ReservedQuantities = new();
	}

	private readonly Building owner;
	private readonly HashSet<IItemContainer> containers = new();
	private readonly Dictionary<IItemContainer, ContainerSnapshot> snapshots = new();
	private readonly Dictionary<uint, int> totalQuantityByItemId = new();
	private readonly Dictionary<BuildingItemKey, Dictionary<IItemContainer, int>> quantityByKeyAndContainer = new();
	private readonly Dictionary<BuildingItemKey, Dictionary<IItemContainer, int>> reservedByKeyAndContainer = new();

	public Building Owner => owner;
	public IReadOnlyCollection<IItemContainer> Containers => containers;
	public IReadOnlyDictionary<uint, int> TotalQuantityByItemId => totalQuantityByItemId;
	public IReadOnlyDictionary<BuildingItemKey, Dictionary<IItemContainer, int>> QuantityByKeyAndContainer => quantityByKeyAndContainer;
	public IReadOnlyDictionary<BuildingItemKey, Dictionary<IItemContainer, int>> ReservedByKeyAndContainer => reservedByKeyAndContainer;

	public event Action<uint, ItemStatus, IItemContainer> OnItemStatusAdded;

	public BuildingItemIndex(Building owner)
	{
		this.owner = owner;
	}

	public bool Register(IItemContainer container, IGridPlaceable placeable)
	{
		if (container == null || placeable == null || containers.Add(container) == false)
			return false;

		snapshots[container] = BuildSnapshot(container);
		ApplySnapshot(container, snapshots[container], 1);
		PublishItemStatusAdded(container, snapshots[container]);
		Subscribe(container);
		return true;
	}

	public bool Unregister(IItemContainer container)
	{
		if (container == null || containers.Remove(container) == false)
			return false;

		Unsubscribe(container);
		if (snapshots.TryGetValue(container, out ContainerSnapshot snapshot))
		{
			ApplySnapshot(container, snapshot, -1);
			snapshots.Remove(container);
		}

		return true;
	}

	public void Rebuild()
	{
		ClearIndex();

		foreach (IItemContainer container in containers)
		{
			ContainerSnapshot snapshot = BuildSnapshot(container);
			snapshots[container] = snapshot;
			ApplySnapshot(container, snapshot, 1);
			PublishItemStatusAdded(container, snapshot);
		}
	}

	public int GetTotalQuantity(uint itemId)
	{
		return totalQuantityByItemId.GetValueOrDefault(itemId);
	}

	public IReadOnlyDictionary<IItemContainer, int> GetContainers(uint itemId, ItemStatus status)
	{
		BuildingItemKey key = new(itemId, status);
		return quantityByKeyAndContainer.TryGetValue(key, out Dictionary<IItemContainer, int> values)
			? values
			: EmptyContainerQuantities;
	}

	public IReadOnlyDictionary<IItemContainer, int> GetReservedContainers(uint itemId, ItemStatus status)
	{
		BuildingItemKey key = new(itemId, status);
		return reservedByKeyAndContainer.TryGetValue(key, out Dictionary<IItemContainer, int> values)
			? values
			: EmptyContainerQuantities;
	}

	private static readonly IReadOnlyDictionary<IItemContainer, int> EmptyContainerQuantities = new Dictionary<IItemContainer, int>();

	private void RefreshContainer(IItemContainer container)
	{
		if (container == null || containers.Contains(container) == false)
			return;

		if (snapshots.TryGetValue(container, out ContainerSnapshot oldSnapshot))
			ApplySnapshot(container, oldSnapshot, -1);

		ContainerSnapshot newSnapshot = BuildSnapshot(container);
		snapshots[container] = newSnapshot;
		ApplySnapshot(container, newSnapshot, 1);
		PublishItemStatusAdded(container, newSnapshot);
	}

	private ContainerSnapshot BuildSnapshot(IItemContainer container)
	{
		ContainerSnapshot snapshot = new();
		if (container == null)
			return snapshot;

		for (int i = 0; i < container.Stacks.Count; ++i)
		{
			ItemStack stack = container.Stacks[i];
			if (stack == null || stack.Quantity <= 0)
				continue;

			AddTo(snapshot.Quantities, new BuildingItemKey(stack.ItemID, stack.Status), stack.Quantity);
		}

		if (container is ShelfBase shelf)
		{
			foreach (var reserved in shelf.ItemToBePicked)
			{
				if (reserved.Value > 0)
					AddTo(snapshot.ReservedQuantities, new BuildingItemKey(reserved.Key, ItemStatus.None), reserved.Value);
			}
		}

		return snapshot;
	}

	private void ApplySnapshot(IItemContainer container, ContainerSnapshot snapshot, int sign)
	{
		if (container == null || snapshot == null)
			return;

		foreach (var entry in snapshot.Quantities)
		{
			ApplyTotal(entry.Key.ItemId, entry.Value * sign);
			ApplyNested(quantityByKeyAndContainer, entry.Key, container, entry.Value * sign);
		}

		foreach (var entry in snapshot.ReservedQuantities)
			ApplyNested(reservedByKeyAndContainer, entry.Key, container, entry.Value * sign);
	}

	private void PublishItemStatusAdded(IItemContainer container, ContainerSnapshot snapshot)
	{
		if (container == null || snapshot == null || OnItemStatusAdded == null)
			return;

		foreach (var entry in snapshot.Quantities)
		{
			int reservedQuantity = snapshot.ReservedQuantities.GetValueOrDefault(entry.Key);
			if (entry.Value - reservedQuantity > 0)
				OnItemStatusAdded.Invoke(entry.Key.ItemId, entry.Key.Status, container);
		}
	}

	private void ApplyTotal(uint itemId, int delta)
	{
		if (delta == 0)
			return;

		int next = totalQuantityByItemId.GetValueOrDefault(itemId) + delta;
		if (next > 0)
			totalQuantityByItemId[itemId] = next;
		else
			totalQuantityByItemId.Remove(itemId);
	}

	private static void ApplyNested(
		Dictionary<BuildingItemKey, Dictionary<IItemContainer, int>> target,
		BuildingItemKey key,
		IItemContainer container,
		int delta)
	{
		if (delta == 0)
			return;

		if (target.TryGetValue(key, out Dictionary<IItemContainer, int> byContainer) == false)
		{
			if (delta < 0)
				return;

			byContainer = new Dictionary<IItemContainer, int>();
			target[key] = byContainer;
		}

		int next = byContainer.GetValueOrDefault(container) + delta;
		if (next > 0)
			byContainer[container] = next;
		else
			byContainer.Remove(container);

		if (byContainer.Count <= 0)
			target.Remove(key);
	}

	private static void AddTo(Dictionary<BuildingItemKey, int> target, BuildingItemKey key, int quantity)
	{
		if (quantity <= 0)
			return;

		target[key] = target.GetValueOrDefault(key) + quantity;
	}

	private void ClearIndex()
	{
		totalQuantityByItemId.Clear();
		quantityByKeyAndContainer.Clear();
		reservedByKeyAndContainer.Clear();
		snapshots.Clear();
	}

	private void Subscribe(IItemContainer container)
	{
		switch (container)
		{
			case ShelfBase shelf:
				shelf.OnItemQuantityChanged += HandleShelfQuantityChanged;
				shelf.OnItemReservedPickChanged += HandleShelfReservedChanged;
				break;
			case CapsuleBuffer capsuleBuffer:
				capsuleBuffer.OnCapsuleDocked += HandleCapsuleBufferDocked;
				capsuleBuffer.OnCapsuleUndocking += HandleCapsuleBufferUndocking;
				capsuleBuffer.OnCapsuleUndocked += HandleCapsuleBufferUndocked;
				capsuleBuffer.OnCapsuleContentChanged += HandleCapsuleBufferContentChanged;
				break;
			case PackingStation packingStation:
				packingStation.OnItemContentChanged += HandlePackingStationContentChanged;
				break;
		}
	}

	private void Unsubscribe(IItemContainer container)
	{
		switch (container)
		{
			case ShelfBase shelf:
				shelf.OnItemQuantityChanged -= HandleShelfQuantityChanged;
				shelf.OnItemReservedPickChanged -= HandleShelfReservedChanged;
				break;
			case CapsuleBuffer capsuleBuffer:
				capsuleBuffer.OnCapsuleDocked -= HandleCapsuleBufferDocked;
				capsuleBuffer.OnCapsuleUndocking -= HandleCapsuleBufferUndocking;
				capsuleBuffer.OnCapsuleUndocked -= HandleCapsuleBufferUndocked;
				capsuleBuffer.OnCapsuleContentChanged -= HandleCapsuleBufferContentChanged;
				break;
			case PackingStation packingStation:
				packingStation.OnItemContentChanged -= HandlePackingStationContentChanged;
				break;
		}
	}

	private void HandleShelfQuantityChanged(ShelfBase shelf, uint itemId, int quantityDelta)
	{
		RefreshContainer(shelf);
	}

	private void HandleShelfReservedChanged(ShelfBase shelf, uint itemId, int reservedQuantityDelta)
	{
		RefreshContainer(shelf);
	}

	private void HandleCapsuleBufferDocked(CapsuleDock dock)
	{
		if (dock is CapsuleBuffer capsuleBuffer)
			RefreshContainer(capsuleBuffer);
	}

	private void HandleCapsuleBufferUndocking(CapsuleBuffer capsuleBuffer, CargoCapsule capsule)
	{
		RefreshContainer(capsuleBuffer);
	}

	private void HandleCapsuleBufferUndocked(CapsuleDock dock)
	{
		if (dock is CapsuleBuffer capsuleBuffer)
			RefreshContainer(capsuleBuffer);
	}

	private void HandleCapsuleBufferContentChanged(CapsuleBuffer capsuleBuffer)
	{
		RefreshContainer(capsuleBuffer);
	}

	private void HandlePackingStationContentChanged(PackingStation packingStation)
	{
		RefreshContainer(packingStation);
	}
}
