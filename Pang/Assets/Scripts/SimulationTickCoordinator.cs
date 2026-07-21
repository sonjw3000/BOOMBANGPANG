public sealed class SimulationTickCoordinator
{
	private GameTime gameTime;
	private ExplosionService explosionService;
	private OxygenService oxygenService;
	private TemperatureService temperatureService;
	private FireService fireService;

	public void Bind(
		GameTime targetGameTime,
		ExplosionService targetExplosionService,
		OxygenService targetOxygenService,
		TemperatureService targetTemperatureService,
		FireService targetFireService)
	{
		if (gameTime == targetGameTime &&
			explosionService == targetExplosionService &&
			oxygenService == targetOxygenService &&
			temperatureService == targetTemperatureService &&
			fireService == targetFireService)
		{
			return;
		}

		Unbind();
		gameTime = targetGameTime;
		explosionService = targetExplosionService;
		oxygenService = targetOxygenService;
		temperatureService = targetTemperatureService;
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
	}
}
