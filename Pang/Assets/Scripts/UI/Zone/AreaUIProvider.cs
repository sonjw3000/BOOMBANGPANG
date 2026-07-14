using UnityEngine;

public sealed class AreaUIProvider : UIProvider<AreaSelectionProxy>
{
	private Area Area => currentTarget != null ? currentTarget.Area : null;

	public override string Name => Area != null ? Area.DisplayName : "Unknown Area";
	public override string Subtitle => Area != null ? Area.Type.ToString() : "Unknown Area Type";
	public override Sprite Icon => null;

	public override void BuildInfoBlocks()
	{
		infoBlocks.Clear();
		if (Area != null)
			infoBlocks.Add(new KeyValueBlock("Type", Area.Type.ToString()));
	}

	public override void DeleteObject()
	{
		if (currentTarget == null || Area == null || currentTarget.AreaManager == null)
			return;

		if (currentTarget.AreaManager.RemoveArea(Area))
			GameContext.Instance.InteractionCtx.ClearSelection();
	}
}
