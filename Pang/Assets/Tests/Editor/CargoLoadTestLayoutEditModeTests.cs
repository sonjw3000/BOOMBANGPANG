using System;
using NUnit.Framework;
using UnityEngine;

public sealed class CargoLoadTestLayoutEditModeTests
{
	private GameObject root;
	private GridService grid;
	private AreaManager areas;
	[SetUp]
	public void SetUp()
	{
		root = new GameObject("Cargo Layout Test");
		grid = root.AddComponent<GridService>();
		grid.BuildDefaultMap();
		areas = root.AddComponent<AreaManager>();
	}
	[TearDown]
	public void TearDown() => UnityEngine.Object.DestroyImmediate(root);

	[Test]
	public void PlanPreservesCentralHoleAndReproducesPositionsWithoutMutatingGrid()
	{
		CargoLoadTestSettings settings = new();
		CargoLoadTestLayout first = CargoLoadTestLayout.Plan(settings, grid, areas);
		CargoLoadTestLayout second = CargoLoadTestLayout.Plan(settings, grid, areas);
		CollectionAssert.AreEqual(first.Centers, second.Centers);
		Assert.AreEqual(settings.pairCount * 2, first.Centers.Count);
		Assert.AreEqual(first.Centers.Count * first.Ports.Count, first.ReachablePortCount);
		int half = settings.diameter / 2;
		for (int z = half - settings.holeSize / 2; z <= half + settings.holeSize / 2; ++z)
			for (int x = half - settings.holeSize / 2; x <= half + settings.holeSize / 2; ++x)
				Assert.IsFalse(first.Cells[z * settings.diameter + x].IsOwned);
		foreach (CargoLoadTestLayout.Port port in first.Ports)
			Assert.IsTrue(Mathf.Abs(port.Outside.x) > settings.holeSize / 2 || Mathf.Abs(port.Outside.y) > settings.holeSize / 2);
		foreach (GridCell cell in grid.Map)
			Assert.AreEqual(0u, cell.BuildingId, "Planning must not assign ownership.");
	}

	[Test]
	public void UnreachablePortsAreRejectedEvenWhenBuildingsDoNotOverlap()
	{
		CargoLoadTestSettings settings = new();
		CargoLoadTestLayout plan = CargoLoadTestLayout.Plan(settings, grid, areas);
		int barrier = plan.SpawnBounds.xMax + 1;
		for (int z = 0; z < grid.MapSize.z; ++z)
			grid.GetCell(barrier, 0, z).Set(new FootprintCell { flags = GridFlags.BlockMovement }, null);
		Assert.Throws<InvalidOperationException>(() => plan.ValidateReachability(grid, settings.diameter, planned: true));
	}

	[Test]
	public void InsufficientSpaceFailsBeforeCreatingAnything()
	{
		CargoLoadTestSettings settings = new() { pairCount = 100, maxAttempts = 200 };
		Assert.Throws<InvalidOperationException>(() => CargoLoadTestLayout.Plan(settings, grid, areas));
		foreach (GridCell cell in grid.Map)
			Assert.IsNull(cell.OccupancyObjectOnGrid);
	}
}
