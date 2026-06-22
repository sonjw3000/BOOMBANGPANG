public sealed class StagingBuilding : Building
{
	public StagingBuilding(string displayName, System.Collections.Generic.List<GridCell> occupiedCells)
		: base(displayName, occupiedCells, BuildingType.Staging)
	{
	}

	protected override bool CanDispatchBufferToOutbound(CapsuleBuffer capsuleBuffer)
	{
		return capsuleBuffer != null && capsuleBuffer.IsCapsuleEmpty() == false;
	}
}
