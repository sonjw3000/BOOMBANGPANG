using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed class PathFindingBudgetEditModeTests
{
	private const BindingFlags InstanceFlags = BindingFlags.Instance | BindingFlags.NonPublic;
	private static readonly FieldInfo ContextInstance = typeof(GameContext).GetField(
		"instance", BindingFlags.Static | BindingFlags.NonPublic);
	private static readonly int3 StartCell = new(1, 0, 1);
	private static readonly int3 BlockedGoal = new(90, 0, 90);
	private readonly List<GameObject> objects = new();
	private GameContext previousContext;
	private GridService grid;
	private PathFindingService service;
	private GameObject obstacle;

	[SetUp]
	public void SetUp()
	{
		previousContext = (GameContext)ContextInstance.GetValue(null);
		ContextInstance.SetValue(null, null);
		GameContext context = CreateObject("Path Budget Context").AddComponent<GameContext>();
		grid = CreateObject("Path Budget Grid").AddComponent<GridService>();
		grid.BuildDefaultMap();
		service = CreateObject("Path Budget Service").AddComponent<PathFindingService>();
		typeof(GameContext).GetField("gridService", InstanceFlags).SetValue(context, grid);
		typeof(GameContext).GetField("pathFindingService", InstanceFlags).SetValue(context, service);
		ContextInstance.SetValue(null, context);
		typeof(PathFindingService).GetMethod("Start", InstanceFlags).Invoke(service, null);
		obstacle = CreateObject("Path Budget Obstacle");
		Block(BlockedGoal);
	}

	[TearDown]
	public void TearDown()
	{
		ContextInstance.SetValue(null, null);
		try
		{
			for (int i = objects.Count - 1; i >= 0; --i)
				Object.DestroyImmediate(objects[i]);
			objects.Clear();
		}
		finally { ContextInstance.SetValue(null, previousContext); }
	}

	[Test]
	public void ManyRequests_Share500StepsAndContinueFromNextTurnAcrossFrames()
	{
		for (int i = 0; i < 12; ++i) RequestLongSearch();
		Assert.That(service.ActiveJobCount, Is.EqualTo(12), "There is no five-job admission limit.");
		Tick();
		Assert.That(service.LastFrameSearchSteps, Is.EqualTo(500));
		Assert.That(TotalClosedStates(), Is.EqualTo(500));
		Assert.That(ClosedStates(Jobs[10]), Is.Zero);
		Assert.That(ClosedStates(Jobs[11]), Is.Zero);
		Tick();
		Assert.That(service.LastFrameSearchSteps, Is.EqualTo(500));
		Assert.That(TotalClosedStates(), Is.EqualTo(1000));
		foreach (PathSearchJob job in Jobs)
			Assert.That(ClosedStates(job), Is.GreaterThan(0), "Waiting requests must receive a turn next frame.");
	}

	[Test]
	public void SingleRequest_CanUseRemainingBudgetAcrossMultipleTurns()
	{
		RequestLongSearch();
		Tick();
		Assert.That(service.LastFrameSearchSteps, Is.EqualTo(500));
		Assert.That(ClosedStates(Jobs[0]), Is.EqualTo(500));
	}

	[Test]
	public void EarlyCompletion_GivesUnusedBudgetToNextRequest()
	{
		SetBudget(75);
		int completions = 0;
		service.RequestRoute(new PathRequest(StartCell, StartCell, FacingDirection.North,
			result => { ++completions; result.Clear(); }));
		RequestLongSearch();
		Tick();
		Assert.That(completions, Is.EqualTo(1));
		Assert.That(service.ActiveJobCount, Is.EqualTo(1));
		Assert.That(service.LastFrameSearchSteps, Is.EqualTo(75));
		Assert.That(ClosedStates(Jobs[0]), Is.EqualTo(74));
	}

	[Test]
	public void CompletionCallback_NewRequestWaitsUntilNextFrame()
	{
		int completions = 0;
		service.RequestRoute(new PathRequest(StartCell, StartCell, FacingDirection.North, result =>
		{
			++completions;
			result.Clear();
			service.RequestRoute(new PathRequest(StartCell, StartCell, FacingDirection.North,
				nextResult => { ++completions; nextResult.Clear(); }));
		}));
		Tick();
		Assert.That(completions, Is.EqualTo(1));
		Assert.That(service.LastFrameSearchSteps, Is.EqualTo(1));
		Assert.That(service.ActiveJobCount, Is.EqualTo(1));
		Tick();
		Assert.That(completions, Is.EqualTo(2));
		Assert.That(service.ActiveJobCount, Is.Zero);
		Tick();
		Assert.That(service.LastFrameSearchSteps, Is.Zero);
	}

	[Test]
	public void PreviewRoute_CompletesOnceAcrossSmallBudgetFrames()
	{
		SetBudget(2);
		int completions = 0;
		IReadOnlyList<int3> path = null;
		int3 goal = new(4, 0, 1);
		Assert.That(service.RequestPreviewRoute(StartCell, goal, result =>
		{
			++completions;
			path = result;
		}), Is.True);
		for (int frame = 0; frame < 100 && service.ActiveJobCount > 0; ++frame)
		{
			Tick();
			Assert.That(service.LastFrameSearchSteps, Is.LessThanOrEqualTo(2));
		}
		Assert.That(service.ActiveJobCount, Is.Zero);
		Assert.That(completions, Is.EqualTo(1));
		Assert.That(path, Is.Not.Empty);
		Assert.That(path[0], Is.EqualTo(StartCell));
		Assert.That(path[path.Count - 1], Is.EqualTo(goal));
	}

	[Test]
	public void UnreachableRequest_CompletesWithEmptyPathAndReleasesJob()
	{
		Block(StartCell + new int3(1, 0, 0));
		Block(StartCell + new int3(-1, 0, 0));
		Block(StartCell + new int3(0, 0, 1));
		Block(StartCell + new int3(0, 0, -1));
		int completions = 0;
		service.RequestPreviewRoute(StartCell, BlockedGoal, path =>
		{
			++completions;
			Assert.That(path, Is.Empty);
		});
		Tick();
		Assert.That(completions, Is.EqualTo(1));
		Assert.That(service.LastFrameSearchSteps, Is.EqualTo(1));
		Assert.That(service.ActiveJobCount, Is.Zero);
	}

	[TestCase(false)]
	[TestCase(true)]
	public void ReusedBuffer_AfterPartialOrCompletedSearchMatchesFreshSearch(bool completeFirst)
	{
		SearchBuffer reused = new(grid.MapSize);
		PathSearchJob previousJob = new();
		int3 firstGoal = completeFirst ? new int3(4, 0, 1) : BlockedGoal;
		previousJob.Setup(new PathRequest(StartCell, firstGoal, FacingDirection.East,
			result => result.Clear()), reused);
		if (completeFirst)
		{
			Assert.That(previousJob.Execute(1000), Is.True);
			previousJob.SetPath();
		}
		else
			Assert.That(previousJob.Execute(20), Is.False);

		int3 newStart = new(8, 0, 1);
		int3 goal = new(4, 0, 1);
		List<int3> actual = SearchPositions(reused, newStart, goal);
		List<int3> expected = SearchPositions(new SearchBuffer(grid.MapSize), newStart, goal);
		CollectionAssert.AreEqual(expected, actual);
		Assert.That(actual[0], Is.EqualTo(newStart));
		Assert.That(actual[actual.Count - 1], Is.EqualTo(goal));
	}

	private static List<int3> SearchPositions(SearchBuffer buffer, int3 start, int3 goal)
	{
		List<int3> positions = new();
		PathSearchJob job = new();
		job.Setup(new PathRequest(start, goal, FacingDirection.West, result =>
		{
			foreach (PathNode node in result.Path) positions.Add(node.Position);
			result.Clear();
		}), buffer);
		bool complete = false;
		for (int frame = 0; frame < 100 && !complete; ++frame)
			complete = job.Execute(50);
		Assert.That(complete, Is.True);
		job.SetPath();
		return positions;
	}

	private List<PathSearchJob> Jobs =>
		(List<PathSearchJob>)typeof(PathFindingService).GetField("activeJobs", InstanceFlags).GetValue(service);

	private void RequestLongSearch() => service.RequestRoute(
		new PathRequest(StartCell, BlockedGoal, FacingDirection.North, result => result.Clear()));

	private void Tick() => typeof(PathFindingService).GetMethod("Update", InstanceFlags).Invoke(service, null);
	private void SetBudget(int budget) =>
		typeof(PathFindingService).GetField("stepBudgetPerFrame", InstanceFlags).SetValue(service, budget);

	private void Block(int3 cell) => grid.GetCell(cell).Set(new FootprintCell
	{
		flags = GridFlags.BlockMovement,
		occupancyCategory = GridOccupancyCategory.Other,
	}, obstacle);

	private static int ClosedStates(PathSearchJob job)
	{
		int count = 0;
		for (int i = 0; i < job.Buffer.StateCount; ++i)
			if (job.Buffer.GetStateRecordByStateIndex(i).VisitState == NodeVisitedState.Closed) ++count;
		return count;
	}

	private int TotalClosedStates()
	{
		int count = 0;
		foreach (PathSearchJob job in Jobs) count += ClosedStates(job);
		return count;
	}

	private GameObject CreateObject(string name)
	{
		GameObject result = new(name);
		result.SetActive(false);
		objects.Add(result);
		return result;
	}
}
