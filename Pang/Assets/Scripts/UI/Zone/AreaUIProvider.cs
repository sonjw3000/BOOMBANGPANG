using UnityEngine;
using UniverseLogistics.UI.Toolkit;

public sealed class AreaUIProvider : UIProvider<AreaSelectionProxy>, ISelectionInspectorProvider
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

	public void BuildInspectorModel(SelectionInspectorModel model)
	{
		model.Clear();
		model.AddOverview("Type", () => Area != null ? Area.Type.ToString() : "Unknown");
		model.AddOverview("Size", () => Area != null ? $"{Area.Bounds.width} × {Area.Bounds.height}" : "0 × 0");
		model.AddOverview("Position", () => Area != null ? $"{Area.Bounds.xMin}, {Area.Bounds.yMin}" : "0, 0");
		model.AddOverview("Floor", () => Area != null ? Area.Floor.ToString() : "0");
		if (Area?.Type == AreaType.RocketLanding)
			model.AddOverview("Destination", GetDestinationDisplay);
		model.AddAction("Delete Area", DeleteObject, isDangerous: true);
	}

	private string GetDestinationDisplay()
	{
		if (Area == null || Area.DestinationBuildingId == 0)
			return "Not linked";
		BuildingManager manager = GameContext.HasInstance ? GameContext.Instance.BuildingMgr : null;
		return manager != null && manager.TryGetBuilding(Area.DestinationBuildingId, out Building building) && building != null
			? building.DisplayName
			: $"Building #{Area.DestinationBuildingId}";
	}
}
