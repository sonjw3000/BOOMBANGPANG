public sealed class SimulationTickCoordinator
{
	private GameTime gameTime;
	private ExplosionService explosionService;
	private OxygenService oxygenService;
	private TemperatureService temperatureService;
	private ItemThermalService itemThermalService;
	private FireService fireService;

	public void Bind(
		GameTime targetGameTime,
		ExplosionService targetExplosionService,
		OxygenService targetOxygenService,
		TemperatureService targetTemperatureService,
		ItemThermalService targetItemThermalService,
		FireService targetFireService)
	{
		if (gameTime == targetGameTime &&
			explosionService == targetExplosionService &&
			oxygenService == targetOxygenService &&
			temperatureService == targetTemperatureService &&
			itemThermalService == targetItemThermalService &&
			fireService == targetFireService)
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
	}

	private void OnSimulationTick(SimulationTickContext context)
	{
		explosionService?.ProcessSimulationTick(in context);
		oxygenService?.ProcessSimulationTick();
		fireService?.ProcessSimulationTick();
		temperatureService?.ProcessSimulationTick();

		if (context.Tick % GameTime.QuarterWeekSimulationTickInterval == 0)
			temperatureService?.ProcessQuarterWeekTick();

		itemThermalService?.ProcessSimulationTick(in context);
	}
}
