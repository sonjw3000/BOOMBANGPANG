public sealed class StagingBuilding : Building
{
	private bool overrideCapsuleThreshold = false;
	private float capsuleThresholdPercent = 80.0f;

	public StagingBuilding(string displayName, System.Collections.Generic.List<GridCell> occupiedCells)
		: base(displayName, occupiedCells, BuildingType.Staging)
	{
	}

	public bool OverrideCapsuleThreshold => overrideCapsuleThreshold;
	public float CapsuleThresholdPercent => capsuleThresholdPercent;

	public void SetOverrideCapsuleThreshold(bool value)
	{
		overrideCapsuleThreshold = value;
	}

	public void SetCapsuleThresholdPercent(float value)
	{
		capsuleThresholdPercent = UnityEngine.Mathf.Clamp(value, 0.0f, 100.0f);
	}

	protected override bool CanDispatchBufferToOutbound(CapsuleBuffer capsuleBuffer)
	{
		if (capsuleBuffer == null || capsuleBuffer.CanDispatchToOutbound() == false)
			return false;

		float workflowThreshold = GameContext.HasInstance && GameContext.Instance.OBWorkflowSvc != null
			? GameContext.Instance.OBWorkflowSvc.CargoPortThresholdPercent
			: capsuleThresholdPercent;
		float threshold = overrideCapsuleThreshold ? capsuleThresholdPercent : workflowThreshold;
		return capsuleBuffer.FilledPercent >= threshold;
	}
}
