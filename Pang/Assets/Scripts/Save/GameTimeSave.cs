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
		elapsedWeek = data.ElapsedWeek;
		elapsedMonth = data.ElapsedMonth;
		preservedPauseCount = 0;
		preservedPauseTimeScale = data.TimeScale;
		ApplyTimeScale(data.TimeScale, true);
	}
}
