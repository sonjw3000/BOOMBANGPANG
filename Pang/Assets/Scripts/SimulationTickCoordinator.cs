public sealed class SimulationTickCoordinator
{
	private GameTime gameTime;
	private ExplosionService explosionService;
	private OxygenService oxygenService;
	private TemperatureService temperatureService;

	public void Bind(
		GameTime targetGameTime,
		ExplosionService targetExplosionService,
		OxygenService targetOxygenService,
		TemperatureService targetTemperatureService)
	{
		if (gameTime == targetGameTime &&
			explosionService == targetExplosionService &&
			oxygenService == targetOxygenService &&
			temperatureService == targetTemperatureService)
		{
			return;
		}

		Unbind();
		gameTime = targetGameTime;
		explosionService = targetExplosionService;
		oxygenService = targetOxygenService;
		temperatureService = targetTemperatureService;

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
	}

	private void OnSimulationTick(SimulationTickContext context)
	{
		explosionService?.ProcessSimulationTick(in context);
		oxygenService?.ProcessSimulationTick();

		if (context.Tick % GameTime.QuarterWeekSimulationTickInterval == 0)
			temperatureService?.ProcessQuarterWeekTick();
	}
}
