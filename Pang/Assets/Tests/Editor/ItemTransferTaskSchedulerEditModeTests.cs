using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class ItemTransferTaskSchedulerEditModeTests
{
	private GameObject workerObject;

	[TearDown]
	public void TearDown()
	{
		if (workerObject != null)
			Object.DestroyImmediate(workerObject);
	}

	[Test]
	public void WorkerScope_DifferentBuilding_RejectsWorker()
	{
		AIWorker worker = CreateWorker(primaryBuildingId: 3);

		Assert.That(IsWorkerInScheduleScope(worker, buildingId: 2), Is.False);
	}

	[Test]
	public void WorkerScope_SameBuilding_AllowsWorker()
	{
		AIWorker worker = CreateWorker(primaryBuildingId: 3);

		Assert.That(IsWorkerInScheduleScope(worker, buildingId: 3), Is.True);
	}

	[Test]
	public void WorkerScope_GlobalSchedule_AllowsBuildingWorker()
	{
		AIWorker worker = CreateWorker(primaryBuildingId: 3);

		Assert.That(IsWorkerInScheduleScope(worker, buildingId: 0), Is.True);
	}

	private AIWorker CreateWorker(uint primaryBuildingId)
	{
		workerObject = new GameObject("Item Transfer Scheduler Test Worker");
		workerObject.SetActive(false);
		AIWorker worker = workerObject.AddComponent<HumanWorker>();
		worker.SetPrimaryBuildingId(primaryBuildingId);
		return worker;
	}

	private static bool IsWorkerInScheduleScope(AIWorker worker, uint buildingId)
	{
		MethodInfo method = typeof(ItemTransferTaskScheduler).GetMethod(
			"IsWorkerInScheduleScope",
			BindingFlags.Static | BindingFlags.NonPublic);
		Assert.That(method, Is.Not.Null);

		return (bool)method.Invoke(
			null,
			new object[] { worker, new ItemTransferScheduleKey(buildingId, ItemTransferScheduleMode.PackingInput) });
	}
}
