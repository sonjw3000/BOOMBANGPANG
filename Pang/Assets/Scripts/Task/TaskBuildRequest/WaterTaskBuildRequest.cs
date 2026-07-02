public sealed class WaterTaskBuildRequest : TaskBuildRequest<WaterTask>
{
	private readonly IInteractionPoint source;

	public WaterTaskBuildRequest(CapsuleBuffer sourceBuffer, uint requestedBuildingID) : base(requestedBuildingID)
	{
		source = sourceBuffer;
	}

	public WaterTaskBuildRequest(PackingStation sourceStation, uint requestedBuildingID) : base(requestedBuildingID)
	{
		source = sourceStation;
	}

	public override WorkerTask.TaskType TaskType => WorkerTask.TaskType.Water;
	public override object RequestKey => GetRequestKey(source);
	public override bool IsStillValid => source switch
	{
		CapsuleBuffer buffer => buffer.CanProvideInboundItems(),
		PackingStation station => station.EndPackingBox != null,
		_ => false,
	};

	public static object GetRequestKey(IInteractionPoint source)
	{
		return new TaskBuildRequestKey(WorkerTask.TaskType.Water, source);
	}

	protected override bool TryBuildTask(out WaterTask task)
	{
		task = null;
		if (IsStillValid == false || BuildingManager == null || BuildingManager.TryGetBuilding(RequestedBuildingID, out Building building) == false)
			return false;

		return source switch
		{
			CapsuleBuffer buffer => TryBuildFromBuffer(building, buffer, out task),
			PackingStation station => TryBuildFromStation(building, station, out task),
			_ => false,
		};
	}

	private bool TryBuildFromBuffer(Building building, CapsuleBuffer sourceBuffer, out WaterTask task)
	{
		task = null;
		if (building is not PackingBuilding packingBuilding ||
			packingBuilding.CanBuildWaterTaskRequest(sourceBuffer) == false)
			return false;

		PackingStationService packingStationService = Ctx?.OBWorkflowSvc?.PackingStationService;
		if (packingStationService == null || packingStationService.TryClaimWaitingStation(RequestedBuildingID, out PackingStation station) == false)
			return false;

		task = new WaterTask(
			new TransferContext(sourceBuffer, TransferObjectType.Item),
			new TransferContext(station, TransferObjectType.Box));
		return true;
	}

	private bool TryBuildFromStation(Building building, PackingStation sourceStation, out WaterTask task)
	{
		task = null;
		if (building is not PackingBuilding packingBuilding ||
			packingBuilding.CanBuildWaterTaskRequest(sourceStation) == false)
			return false;

		PackingStationService packingStationService = Ctx?.OBWorkflowSvc?.PackingStationService;
		if (packingStationService == null || packingStationService.TryResolveOutboundBuffer(sourceStation, out CapsuleBuffer targetBuffer) == false)
			return false;

		task = new WaterTask(
			new TransferContext(sourceStation, TransferObjectType.Box),
			new TransferContext(targetBuffer, TransferObjectType.Item));
		return true;
	}
}
