using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Unity.Mathematics;

public sealed class PathResultBufferMergeEditModeTests
{
	private readonly List<PathResultBuffer> buffers = new();

	[SetUp]
	public void SetUp() => PathResultBuffer.InitializePool(128);

	[TearDown]
	public void TearDown()
	{
		foreach (PathResultBuffer buffer in buffers)
			buffer.Clear();
		buffers.Clear();
	}

	[Test]
	public void EarlyRejoin_ResumesAfterIntersectionWithoutBacktracking()
	{
		PathResultBuffer root = Create(Enumerable.Range(25, 10).Reverse().Select(x => P(x, 3)).ToArray());
		Advance(root, 2); // The blocked target was 33; RequestSubPath advances the parent to 32.
		PathResultBuffer detour = Create(P(34, 3), P(34, 4), P(34, 5), P(33, 5), P(32, 5),
			P(31, 5), P(30, 5), P(29, 5), P(29, 4), P(29, 3), P(30, 3), P(31, 3), P(32, 3));

		root.SubPathResult = detour;

		Assert.That(root.CurrentIndex, Is.EqualTo(5), "The parent cursor must point at the actual rejoin cell, 29.");
		Assert.That(detour.Path.Last.Value.Position, Is.EqualTo(P(29, 3)));
		root.MoveToNextNode(); // FindRoute.OnPathFound skips the detour's source cell.
		int3[] expected = { P(34, 4), P(34, 5), P(33, 5), P(32, 5), P(31, 5), P(30, 5),
			P(29, 5), P(29, 4), P(29, 3), P(28, 3), P(27, 3), P(26, 3), P(25, 3) };
		List<int3> upcoming = new();
		Assert.That(root.CollectUpcomingPositions(upcoming, 100), Is.EqualTo(expected.Length));
		Assert.That(upcoming, Is.EqualTo(expected), "Traffic protection must not include the skipped parent segment.");
		HashSet<int3> remaining = new();
		root.CollectRemainingPositions(remaining);
		Assert.That(remaining, Is.EquivalentTo(expected), "Planned-path registration must use the merged continuation.");
		Assert.That(root.AreRemainingPositionsValid(cell => !cell.Equals(P(31, 3))), Is.True,
			"Invalidating a skipped parent cell must not invalidate the merged route.");
		Assert.That(Drain(root, P(34, 3)), Is.EqualTo(expected));
	}

	[Test]
	public void RejoinAtParentGoal_CompletesWithoutReplayingParentSuffix()
	{
		PathResultBuffer root = Create(P(0, 0), P(1, 0), P(2, 0), P(3, 0));
		Advance(root, 2);
		root.SubPathResult = Create(P(0, 0), P(0, 1), P(1, 1), P(2, 1), P(3, 1), P(3, 0), P(2, 0));

		Assert.That(root.CurrentIndex, Is.EqualTo(3));
		Assert.That(root.IsGoalReached, Is.False, "The parent goal is not physically reached until its child arrives.");
		root.MoveToNextNode();
		Assert.That(Drain(root, P(0, 0)), Is.EqualTo(new[] { P(0, 1), P(1, 1), P(2, 1), P(3, 1), P(3, 0) }));
		Assert.That(root.IsGoalReached, Is.True);
		Assert.That(root.CurrentIndex, Is.EqualTo(root.Path.Count));
		Assert.That(root.NextNode, Is.Null);
	}

	[Test]
	public void RejoinAtOriginalTarget_PreservesNormalContinuation()
	{
		PathResultBuffer root = Create(P(0, 0), P(1, 0), P(2, 0), P(3, 0));
		Advance(root, 2);
		PathResultBuffer detour = Create(P(0, 0), P(0, 1), P(1, 1), P(2, 1), P(2, 0));
		root.SubPathResult = detour;

		Assert.That(root.CurrentIndex, Is.EqualTo(2));
		Assert.That(detour.Path.Count, Is.EqualTo(5));
		root.MoveToNextNode();
		Assert.That(Drain(root, P(0, 0)), Is.EqualTo(new[] { P(0, 1), P(1, 1), P(2, 1), P(2, 0), P(3, 0) }));
	}

	[Test]
	public void NestedRejoin_AdvancesImmediateParentCursorWithoutChangingRootCursor()
	{
		PathResultBuffer root = Create(P(0, 0), P(1, 0), P(2, 0), P(3, 0), P(4, 0), P(5, 0));
		Advance(root, 2);
		PathResultBuffer first = Create(P(0, 0), P(0, 1), P(1, 1), P(2, 1), P(3, 1), P(3, 0), P(2, 0));
		root.SubPathResult = first;
		root.MoveToNextNode(); // Arrive at 0,1; a new conflict skips 1,1 and targets 2,1.
		Advance(root, 2);
		PathResultBuffer second = Create(P(0, 1), P(0, 2), P(1, 2), P(2, 2), P(3, 2), P(3, 1), P(2, 1));

		root.SubPathResult = second;

		Assert.That(second.ParentBuffer, Is.SameAs(first));
		Assert.That(root.CurrentIndex, Is.EqualTo(3));
		Assert.That(first.CurrentIndex, Is.EqualTo(4));
		Assert.That(second.Path.Last.Value.Position, Is.EqualTo(P(3, 1)));
		root.MoveToNextNode();
		Assert.That(Drain(root, P(0, 1)), Is.EqualTo(new[] { P(0, 2), P(1, 2), P(2, 2), P(3, 2), P(3, 1), P(3, 0), P(4, 0), P(5, 0) }));
	}

	[Test]
	public void NestedRejoin_DoesNotMistakeRootSuffixForImmediateParentIntersection()
	{
		PathResultBuffer root = Create(P(0, 0), P(1, 0), P(2, 0), P(3, 0), P(4, 0), P(5, 0));
		Advance(root, 2);
		PathResultBuffer first = Create(P(0, 0), P(0, 1), P(1, 1), P(2, 1), P(2, 0));
		root.SubPathResult = first;
		Advance(root, 3);
		PathResultBuffer second = Create(P(0, 1), P(0, 2), P(1, 2), P(2, 2), P(3, 2), P(4, 2),
			P(4, 1), P(4, 0), P(3, 0), P(3, 1), P(2, 1));

		root.SubPathResult = second;

		Assert.That(root.CurrentIndex, Is.EqualTo(2));
		Assert.That(first.CurrentIndex, Is.EqualTo(3));
		Assert.That(second.Path.Last.Value.Position, Is.EqualTo(P(2, 1)));
		Assert.That(second.Path.Count, Is.EqualTo(11));
		root.MoveToNextNode();
		Drain(root, P(0, 1)); // In particular, never jump from root's 4,0 to the leaf's 2,0.
	}

	[Test]
	public void NextNode_AfterNestedParentGoal_FindsGrandparentContinuation()
	{
		PathResultBuffer root = Create(P(0, 0), P(1, 0), P(2, 0), P(3, 0));
		Advance(root, 2);
		PathResultBuffer first = Create(P(0, 0), P(0, 1), P(1, 1), P(2, 1), P(2, 0));
		root.SubPathResult = first;
		Advance(root, 3);
		PathResultBuffer second = Create(P(0, 1), P(0, 2), P(1, 2), P(2, 2), P(3, 2), P(3, 1), P(3, 0), P(2, 0), P(2, 1));
		root.SubPathResult = second;
		root.MoveToNextNode();
		for (int i = 0; i < 20 && !root.CurrentNode.Position.Equals(P(2, 0)); ++i)
			root.MoveToNextNode();

		Assert.That(root.CurrentNode.Position, Is.EqualTo(P(2, 0)));
		Assert.That(root.CurrentLinkedListNode, Is.SameAs(second.Path.Last), "The innermost detour is still completing.");
		Assert.That(first.CurrentIndex, Is.EqualTo(first.Path.Count - 1));
		Assert.That(root.NextNode, Is.Not.Null);
		Assert.That(root.NextNode.Position, Is.EqualTo(P(3, 0)));
		root.MoveToNextNode();
		Assert.That(root.CurrentNode.Position, Is.EqualTo(P(3, 0)));
	}

	[Test]
	public void DuplicateParentPosition_UsesFirstRemainingOccurrence()
	{
		PathResultBuffer root = Create(P(0, 0), P(1, 0), P(2, 0), P(3, 0), P(2, 0), P(2, -1));
		Advance(root, 2);
		root.SubPathResult = Create(P(0, 0), P(0, 1), P(1, 1), P(2, 1), P(2, 0));
		Assert.That(root.CurrentIndex, Is.EqualTo(2));
		root.MoveToNextNode();
		Assert.That(Drain(root, P(0, 0)), Is.EqualTo(new[] { P(0, 1), P(1, 1), P(2, 1), P(2, 0), P(3, 0), P(2, 0), P(2, -1) }));
	}

	private PathResultBuffer Create(params int3[] points)
	{
		LinkedList<PathNode> nodes = new();
		foreach (int3 point in points)
			nodes.AddLast(PathResultBuffer.GetNewNode(point, FacingDirection.West));
		PathResultBuffer buffer = new(nodes, null);
		buffers.Add(buffer);
		return buffer;
	}

	private static void Advance(PathResultBuffer buffer, int count)
	{
		for (int i = 0; i < count; ++i)
			buffer.MoveToNextNode();
	}

	private static List<int3> Drain(PathResultBuffer root, int3 previous)
	{
		List<int3> result = new();
		while (!root.IsGoalReached && result.Count < 100)
		{
			Assert.That(root.CurrentNode, Is.Not.Null);
			int3 next = root.CurrentNode.Position;
			int3 delta = math.abs(next - previous);
			Assert.That(delta.x + delta.y + delta.z, Is.EqualTo(1), $"Invalid merged step: {previous} -> {next}");
			result.Add(next);
			previous = next;
			root.MoveToNextNode();
		}
		Assert.That(root.IsGoalReached, Is.True, "The merged route must terminate.");
		return result;
	}

	private static int3 P(int x, int z) => new(x, 0, z);
}
