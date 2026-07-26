public sealed class SimulationTickCoordinator
{
	private GameTime gameTime;
	private ExplosionService explosionService;
	private OxygenService oxygenService;
	private TemperatureService temperatureService;
	private ThermalTransferService thermalTransferService;
	private FireService fireService;

	public void Bind(
		GameTime targetGameTime,
		ExplosionService targetExplosionService,
		OxygenService targetOxygenService,
		TemperatureService targetTemperatureService,
		ThermalTransferService targetThermalTransferService,
		FireService targetFireService)
	{
		if (gameTime == targetGameTime &&
			explosionService == targetExplosionService &&
			oxygenService == targetOxygenService &&
			temperatureService == targetTemperatureService &&
			thermalTransferService == targetThermalTransferService &&
			fireService == targetFireService)
		{
			return;
		}

		Unbind();
		gameTime = targetGameTime;
		explosionService = targetExplosionService;
		oxygenService = targetOxygenService;
		temperatureService = targetTemperatureService;
		thermalTransferService = targetThermalTransferService;
		fireService = targetFireService;

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
		thermalTransferService = null;
		fireService = null;
	}

	private void OnSimulationTick(SimulationTickContext context)
	{
		explosionService?.ProcessSimulationTick(in context);
		oxygenService?.ProcessSimulationTick();
		fireService?.ProcessSimulationTick();
		temperatureService?.ProcessSimulationTick();

		if (context.Tick % GameTime.QuarterWeekSimulationTickInterval == 0)
			temperatureService?.ProcessQuarterWeekTick();

		thermalTransferService?.ProcessSimulationTick(in context);
	}
}
