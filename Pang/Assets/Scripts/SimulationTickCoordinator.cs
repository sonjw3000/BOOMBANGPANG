public sealed class SimulationTickCoordinator
{
	private GameTime gameTime;
	private ExplosionService explosionService;
	private OxygenService oxygenService;
	private TemperatureService temperatureService;
	private ItemThermalService itemThermalService;
	private FireService fireService;
	private WearService wearService;
	private WorkerManager workerManager;

	public void Bind(
		GameTime targetGameTime,
		ExplosionService targetExplosionService,
		OxygenService targetOxygenService,
		TemperatureService targetTemperatureService,
		ItemThermalService targetItemThermalService,
		FireService targetFireService,
		WearService targetWearService,
		WorkerManager targetWorkerManager)
	{
		if (gameTime == targetGameTime &&
			explosionService == targetExplosionService &&
			oxygenService == targetOxygenService &&
			temperatureService == targetTemperatureService &&
			itemThermalService == targetItemThermalService &&
			fireService == targetFireService &&
			wearService == targetWearService &&
			workerManager == targetWorkerManager)
		{
			return;
		}

		Unbind();
		gameTime = targetGameTime;
		explosionService = targetExplosionService;
		oxygenService = targetOxygenService;
		temperatureService = targetTemperatureService;
		itemThermalService = targetItemThermalService;
		fireService = targetFireService;
		wearService = targetWearService;
		workerManager = targetWorkerManager;

		if (gameTime != null)
			gameTime.OnSimulationTick += OnSimulationTick;
	}

	public void Unbind()
	{
		if (gameTime != null)
			gameTime.OnSimulationTick -= OnSimulationTick;

		gameTime = null;
		explosionService = null;
		oxygenService = null;
		temperatureService = null;
		itemThermalService = null;
		fireService = null;
		wearService = null;
		workerManager = null;
	}

	private void OnSimulationTick(SimulationTickContext context)
	{
		explosionService?.ProcessSimulationTick(in context);
		workerManager?.ReportRobotWear(in context, wearService);
		oxygenService?.ProcessSimulationTick(in context);
		fireService?.ProcessSimulationTick();
		temperatureService?.ProcessSimulationTick();

		if (context.Tick % GameTime.QuarterWeekSimulationTickInterval == 0)
		{
			temperatureService?.ProcessQuarterWeekTick();
			wearService?.ProcessQuarterWeekTick();
		}

		itemThermalService?.ProcessSimulationTick(in context);
	}
}
