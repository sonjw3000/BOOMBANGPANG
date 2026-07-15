using UnityEngine;
using UniverseLogistics.UI.Toolkit;

public sealed class PackingStationUIProvider : UIProvider<PackingStation>, ISelectionInspectorProvider
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

	public void BuildInspectorModel(SelectionInspectorModel model)
	{
		model.Clear();
		model.AddTab("Waiting", () => GetBoxVersion(currentTarget?.WaitingBox?.Box), () => BuildBoxPanel("WAITING", currentTarget?.WaitingBox?.Box, GetWaitingBoxDisplay()));
		model.AddTab("Processing", () => GetBoxVersion(currentTarget?.CurrentPackingBox?.Box), () => BuildBoxPanel("PROCESSING", currentTarget?.CurrentPackingBox?.Box, GetCurrentBoxDisplay()));
		model.AddTab("Output", () => GetBoxVersion(currentTarget?.EndPackingBox?.Box), () => BuildBoxPanel("OUTPUT", currentTarget?.EndPackingBox?.Box, GetEndBoxDisplay()));
		model.AddOverview("Worker", () => CurrentWorkerName);
		model.AddOverview("Status", () => WorkStatus);
		model.AddOverview("Stage", () => WorkStage);
		model.AddOverview("Incoming Worker", () => IncomingWorkerName);
		model.AddOverview("Incoming Request", () => IncomingRequestDisplay);
		model.AddAction("Remove", DeleteObject, isDangerous: true);
	}

	private static int GetBoxVersion(BoxBase box)
	{
		unchecked
		{
			return SelectionDetailContentUtility.GetItemContainerVersion(box) * 31 + (box != null ? (int)box.BoxId : 0);
		}
	}

	private static SelectionDetailPanelModel BuildBoxPanel(string title, BoxBase box, ItemContainerDisplayInfo display)
	{
		string summary = box != null
			? $"{display.ContainerName} · {box.TotalSize:0.0} / {box.MaxSize:0.0} units"
			: "No box in this stage.";
		return SelectionDetailContentUtility.BuildItemContainerPanel(title, summary, display);
	}

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
