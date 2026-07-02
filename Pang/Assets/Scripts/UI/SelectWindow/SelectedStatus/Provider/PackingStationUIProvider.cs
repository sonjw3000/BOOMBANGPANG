using UnityEngine;

public sealed class PackingStationUIProvider : UIProvider<PackingStation>
{
	public override string Name => currentTarget != null ? currentTarget.name : "Unknown Packing Station";
	public override string Subtitle => "Packing Station";
	public override Sprite Icon => null;

	public string CurrentWorkerName => currentTarget?.CurrentPackingWorker != null ? currentTarget.CurrentPackingWorker.name : "None";
	public string WorkStatus => ResolveWorkStatus();
	public string WorkStage => ResolveWorkStage();
	public string IncomingWorkerName => currentTarget?.IncomingPickingWorker != null ? currentTarget.IncomingPickingWorker.name : "None";
	public string IncomingRequestDisplay => currentTarget != null && currentTarget.IncomingRequestSuspended ? "Suspended" : "Active";

	public override void BuildInfoBlocks()
	{
		infoBlocks.Clear();
		infoBlocks.Add(new KeyValueBlock("Worker", CurrentWorkerName));
		infoBlocks.Add(new KeyValueBlock("Status", WorkStatus));
		infoBlocks.Add(new KeyValueBlock("Stage", WorkStage));
	}

	public override void OnUpdate()
	{
		if (infoBlocks.Count < 3)
			return;

		(infoBlocks[0] as KeyValueBlock)?.UpdateValue(CurrentWorkerName);
		(infoBlocks[1] as KeyValueBlock)?.UpdateValue(WorkStatus);
		(infoBlocks[2] as KeyValueBlock)?.UpdateValue(WorkStage);
	}

	public ItemContainerDisplayInfo GetWaitingBoxDisplay() => BuildItemContainerDisplay(currentTarget?.WaitingBox?.Box);
	public ItemContainerDisplayInfo GetCurrentBoxDisplay() => BuildItemContainerDisplay(currentTarget?.CurrentPackingBox?.Box);
	public ItemContainerDisplayInfo GetEndBoxDisplay() => BuildItemContainerDisplay(currentTarget?.EndPackingBox?.Box);

	private ItemContainerDisplayInfo BuildItemContainerDisplay(BoxBase box)
	{
		return new ItemContainerDisplayInfo
		{
			ContainerName = box != null ? $"{box.Type} Box #{box.BoxId}" : "None",
			HasContainer = box != null,
			Items = ItemContainerDisplayUtility.BuildItemRows(box),
			ManifestItems = ItemContainerDisplayUtility.BuildManifestRows(box),
		};
	}

	private string ResolveWorkStatus()
	{
		if (currentTarget == null)
			return "None";

		if (currentTarget.EndPackingBox != null)
			return "Completed Box Ready";

		if (currentTarget.CurrentPackingBox?.IsFullyPacked == true)
			return "Finishing";

		if (currentTarget.CurrentPackingBox != null)
			return "Packing";

		if (currentTarget.WaitingBox != null)
			return "Waiting Box Ready";

		if (currentTarget.IncomingPickingWorker != null)
			return "Awaiting Incoming Box";

		return "Idle";
	}

	private string ResolveWorkStage()
	{
		if (currentTarget == null)
			return "None";

		if (currentTarget.EndPackingBox != null)
			return "Waiting Outbound Pickup";

		if (currentTarget.CurrentPackingBox?.IsFullyPacked == true)
			return "Move Box To End";

		if (currentTarget.CurrentPackingBox != null)
			return "Pack Items";

		if (currentTarget.IsBoxMoveableToPack)
			return "Move Waiting Box";

		if (currentTarget.IncomingPickingWorker != null)
			return "Incoming Delivery";

		return "No Active Stage";
	}
}
