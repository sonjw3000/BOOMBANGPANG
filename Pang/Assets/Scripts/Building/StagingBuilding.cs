public sealed class StagingBuilding : Building
{

	public StagingBuilding(string displayName, System.Collections.Generic.List<GridCell> occupiedCells)
		: base(displayName, occupiedCells, BuildingType.Staging)
	{
	}

	protected override bool IsBufferOutboundReady(CapsuleBuffer capsuleBuffer)
	{
		// todo
		// check buffer items are fully labeled
		return capsuleBuffer.HasCapsule && capsuleBuffer.CanDispatchToOutbound();
	}

	protected override void OnCapsuleBufferDocked(CapsuleBuffer capsuleBuffer)
	{
		EnsureOutboundState(capsuleBuffer);
		base.OnCapsuleBufferDocked(capsuleBuffer);
	}

	protected override void OnCapsuleBufferContentChanged(CapsuleBuffer capsuleBuffer)
	{
		EnsureOutboundState(capsuleBuffer);
		base.OnCapsuleBufferContentChanged(capsuleBuffer);
	}

	protected override void OnCapsuleBufferStateChanged(CapsuleBuffer capsuleBuffer)
	{
		EnsureOutboundState(capsuleBuffer);
		base.OnCapsuleBufferStateChanged(capsuleBuffer);
	}

	private static void EnsureOutboundState(CapsuleBuffer capsuleBuffer)
	{
		if (capsuleBuffer == null ||
			capsuleBuffer.HasCapsule == false ||
			capsuleBuffer.IsCapsuleEmpty() ||
			capsuleBuffer.BufferState == CapsuleBufferState.OBOnly)
		{
			return;
		}

		capsuleBuffer.SetBufferState(CapsuleBufferState.OBOnly);
	}
}
