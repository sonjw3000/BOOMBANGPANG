public sealed class SimulationTickCoordinator
{
	private GameTime gameTime;
	private ExplosionService explosionService;
	private TemperatureService temperatureService;

	public void Bind(
		GameTime targetGameTime,
		ExplosionService targetExplosionService,
		TemperatureService targetTemperatureService)
	{
		if (gameTime == targetGameTime &&
			explosionService == targetExplosionService &&
			temperatureService == targetTemperatureService)
		{
			return;
		}

		Unbind();
		gameTime = targetGameTime;
		explosionService = targetExplosionService;
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
		temperatureService = null;
	}

	private void OnSimulationTick(SimulationTickContext context)
	{
		explosionService?.ProcessSimulationTick(in context);

		if (context.Tick % GameTime.QuarterWeekSimulationTickInterval == 0)
			temperatureService?.ProcessQuarterWeekTick();
	}
}
