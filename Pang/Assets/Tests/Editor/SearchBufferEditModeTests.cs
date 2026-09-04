using System;
using System.Reflection;
using NUnit.Framework;
using Unity.Mathematics;

public sealed class SearchBufferEditModeTests
{
	[Test]
	public void StateIndices_RoundTripEveryCellAndDirection()
	{
		SearchBuffer buffer = new(new int3(5, 3, 7));
		FacingDirection[] directions = (FacingDirection[])Enum.GetValues(typeof(FacingDirection));
		Assert.That(SearchBuffer.DirectionCount, Is.EqualTo(directions.Length));
		for (int y = 0; y < 3; ++y)
		for (int z = 0; z < 7; ++z)
		for (int x = 0; x < 5; ++x)
		foreach (FacingDirection direction in directions)
		{
			int3 cell = new(x, y, z);
			int index = buffer.GetStateIndex(cell, direction);
			Assert.That(buffer.GetPosition(index), Is.EqualTo(cell));
			Assert.That(buffer.GetFacingDirection(index), Is.EqualTo(direction));
		}
	}

	[Test]
	public void Reset_ClearsOpenAndClosedRecordsAndHeapOnNextAccess()
	{
		SearchBuffer buffer = new(new int3(5, 1, 5));
		AssertDefault(buffer.GetStateRecordByStateIndex(0));
		ref PathNodeRecord open = ref buffer.GetStateRecordByStateIndex(0);
		open.ParentIndex = 4;
		open.GCost = 12;
		open.HCost = 7;
		open.HeapIndex = 3;
		open.VisitState = NodeVisitedState.Open;
		buffer.OpenSet.Push(0);
		ref PathNodeRecord closed = ref buffer.GetStateRecordByStateIndex(4);
		closed.ParentIndex = 0;
		closed.GCost = 3;
		closed.VisitState = NodeVisitedState.Closed;
		buffer.ResetBuffer();
		Assert.That(buffer.OpenSet.Count, Is.Zero);
		AssertDefault(buffer.GetStateRecordByStateIndex(0));
		AssertDefault(buffer.GetStateRecordByStateIndex(4));
		AssertDefault(buffer.GetStateRecordByStateIndex(buffer.StateCount - 1));
		ref PathNodeRecord current = ref buffer.GetStateRecordByStateIndex(4);
		current.GCost = 19;
		Assert.That(buffer.GetStateRecordByStateIndex(4).GCost, Is.EqualTo(19),
			"Repeated access within one search must preserve its state.");
		buffer.ResetBuffer();
		buffer.ResetBuffer();
		AssertDefault(buffer.GetStateRecordByStateIndex(4));
	}

	[Test]
	public void GenerationRollover_DoesNotReviveAncientRecords()
	{
		SearchBuffer buffer = new(new int3(2, 1, 2));
		buffer.GetStateRecordByStateIndex(0).GCost = 123;
		typeof(SearchBuffer).GetField("generation", BindingFlags.Instance | BindingFlags.NonPublic)
			.SetValue(buffer, uint.MaxValue);
		buffer.GetStateRecordByStateIndex(1).GCost = 456;
		buffer.OpenSet.Push(1);
		buffer.ResetBuffer();
		Assert.That(buffer.OpenSet.Count, Is.Zero);
		AssertDefault(buffer.GetStateRecordByStateIndex(0));
		AssertDefault(buffer.GetStateRecordByStateIndex(1));
	}

	[Test]
	public void WarmCoordinateLookupsAndResets_DoNotAllocateManagedMemory()
	{
		SearchBuffer buffer = new(new int3(5, 1, 5));
		int3 cell = new(2, 0, 3);
		RunLookupsAndResets(buffer, cell, 10);
		long before = GC.GetAllocatedBytesForCurrentThread();
		RunLookupsAndResets(buffer, cell, 1000);
		long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
		Assert.That(allocated, Is.Zero);
	}

	private static void RunLookupsAndResets(SearchBuffer buffer, int3 cell, int count)
	{
		for (int i = 0; i < count; ++i)
		{
			int index = buffer.GetStateIndex(cell, FacingDirection.East);
			buffer.GetPosition(index);
			buffer.GetFacingDirection(index);
			buffer.GetStateRecordByStateIndex(index).GCost = i;
			buffer.ResetBuffer();
		}
	}

	private static void AssertDefault(PathNodeRecord record)
	{
		Assert.That(record.ParentIndex, Is.EqualTo(-1));
		Assert.That(record.GCost, Is.EqualTo(int.MaxValue));
		Assert.That(record.HCost, Is.Zero);
		Assert.That(record.HeapIndex, Is.EqualTo(-1));
		Assert.That(record.VisitState, Is.EqualTo(NodeVisitedState.None));
	}
}
