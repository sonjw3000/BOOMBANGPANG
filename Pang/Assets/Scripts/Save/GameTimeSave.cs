using UnityEngine;

public partial class GameTime
{
	public TimeSaveData CaptureState()
	{
		return new TimeSaveData
		{
			TimeElapsed = timeElapsed,
			ElapsedWeek = elapsedWeek,
			ElapsedMonth = elapsedMonth,
			TimeScale = preservedPauseCount > 0 ? preservedPauseTimeScale : timeScale,
		};
	}

	public void RestoreState(TimeSaveData data)
	{
		if (data == null)
			return;

		timeElapsed = data.TimeElapsed;
		simulationTickElapsed = SecondsPerSimulationTick > 0f ? timeElapsed % SecondsPerSimulationTick : 0f;
		simulationTicksPassed = (ulong)Mathf.Max(0, data.ElapsedWeek * 4 + Mathf.FloorToInt(timeElapsed / SecondsPerSimulationTick));
		elapsedWeek = data.ElapsedWeek;
		elapsedMonth = data.ElapsedMonth;
		preservedPauseCount = 0;
		preservedPauseTimeScale = data.TimeScale;
		ApplyTimeScale(data.TimeScale, true);
	}
}
