using NUnit.Framework;
using UnityEngine;

public sealed class LogisticsWorkStatusEditModeTests
{
	private GameObject testObject;

	[TearDown]
	public void TearDown()
	{
		if (testObject != null)
			Object.DestroyImmediate(testObject);
	}

	[Test]
	public void CapsuleDock_DefaultsToIdleWithNoBlockReason()
	{
		CapsuleBuffer buffer = CreateBuffer();

		Assert.That(buffer, Is.InstanceOf<ILogisticsWorkStatusProvider>());
		Assert.That(buffer.LogisticsWorkStatus.State, Is.EqualTo(LogisticsWorkState.Idle));
		Assert.That(buffer.LogisticsWorkStatus.BlockReason, Is.EqualTo(LogisticsBlockReason.None));
	}

	[Test]
	public void CapsuleDock_StateAndBlockReasonCanChangeIndependently()
	{
		CapsuleBuffer buffer = CreateBuffer();

		buffer.SetLogisticsWorkState(LogisticsWorkState.Need);
		buffer.SetLogisticsBlockReason(LogisticsBlockReason.DestinationFull);

		Assert.That(buffer.LogisticsWorkStatus.State, Is.EqualTo(LogisticsWorkState.Need));
		Assert.That(
			buffer.LogisticsWorkStatus.BlockReason,
			Is.EqualTo(LogisticsBlockReason.DestinationFull));
	}

	[Test]
	public void CapsuleDock_StatusChangeEventOnlyFiresForActualChanges()
	{
		CapsuleBuffer buffer = CreateBuffer();
		int changeCount = 0;
		LogisticsWorkStatus latest = default;
		buffer.OnLogisticsWorkStatusChanged += (_, status) =>
		{
			++changeCount;
			latest = status;
		};

		buffer.SetLogisticsWorkStatus(LogisticsWorkState.Idle, LogisticsBlockReason.None);
		buffer.SetLogisticsWorkStatus(LogisticsWorkState.Waiting, LogisticsBlockReason.SourceFull);
		buffer.SetLogisticsWorkStatus(LogisticsWorkState.Waiting, LogisticsBlockReason.SourceFull);

		Assert.That(changeCount, Is.EqualTo(1));
		Assert.That(latest.State, Is.EqualTo(LogisticsWorkState.Waiting));
		Assert.That(latest.BlockReason, Is.EqualTo(LogisticsBlockReason.SourceFull));
	}

	[Test]
	public void CapsuleDock_ResetRestoresDefaultStatus()
	{
		CapsuleBuffer buffer = CreateBuffer();
		buffer.SetLogisticsWorkStatus(LogisticsWorkState.Active, LogisticsBlockReason.NoRoute);

		buffer.ResetLogisticsWorkStatus();

		Assert.That(buffer.LogisticsWorkStatus.State, Is.EqualTo(LogisticsWorkState.Idle));
		Assert.That(buffer.LogisticsWorkStatus.BlockReason, Is.EqualTo(LogisticsBlockReason.None));
	}

	private CapsuleBuffer CreateBuffer()
	{
		testObject = new GameObject("Logistics Work Status Test Buffer");
		return testObject.AddComponent<CapsuleBuffer>();
	}
}
