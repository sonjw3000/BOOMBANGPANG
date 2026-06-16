using UnityEngine;

public sealed class ZoneUIProvider : UIProvider<ZoneSelectionProxy>
{
	private ZoneArea Zone => currentTarget != null ? currentTarget.Zone : null;
	public override string Name => Zone != null ? Zone.DisplayName : "Unknown Zone";
    public override string Subtitle => Zone != null ? Zone.Type.ToString() : "Unknown Zone Type";
	public override Sprite Icon => null;

	public override void BuildInfoBlocks()
	{
		infoBlocks.Clear();
		if (Zone == null)
			return;

		infoBlocks.Add(new KeyValueBlock("Type", Zone.Type.ToString()));
	}

	public override void DeleteObject()
	{
		if (currentTarget == null || Zone == null || currentTarget.ZoneManager == null)
			return;

		if (currentTarget.ZoneManager.RemoveZone(Zone))
			GameContext.Instance.InteractionCtx.ClearSelection();
	}
}
