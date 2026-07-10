using System;
using UnityEngine;

public readonly struct SimulationTickContext
{
	public readonly ulong Tick;
	public readonly float ElapsedWeeks;

	public SimulationTickContext(ulong tick, float elapsedWeeks)
	{
		Tick = tick;
		ElapsedWeeks = elapsedWeeks;
	}
}

public partial class GameTime : MonoBehaviour
{
	[Header("게임 시간 설정")]
	[Tooltip("현실 시간 기준 1개월이 몇 초인가?")]
	[SerializeField] private float secondsPerMonth = 120f;

	[Header("게임 시간 배율")]
	[SerializeField] private float timeScale = 1.0f;
	[Tooltip("최대 배속 지수. 3이면 2^3 = 8배속까지 허용한다.")]
	[SerializeField, Min(0)] private int maxSpeedExponent = 3;

	public const float SimulationTickWeeks = 0.25f;

	private float SecondsPerWeek => secondsPerMonth / 4.0f;
	private float SecondsPerSimulationTick => SecondsPerWeek * SimulationTickWeeks;
	private float timeElapsed = 0f;
	private float simulationTickElapsed = 0f;
	private ulong simulationTicksPassed;
	private float preservedPauseTimeScale = 1.0f;
	private int preservedPauseCount;

	private int elapsedWeek = 0;
	private int elapsedMonth = 0;

	public int Week => elapsedWeek % 4 + 1;
	public int Month => elapsedMonth % 12 + 1;
	public int Year => elapsedMonth / 12;

	public int WeeksPassed => elapsedWeek;
	public int MonthsPassed => elapsedMonth;
	public float TimeScale => timeScale;
	public int MaxSpeedExponent => maxSpeedExponent;
	public int MaxTimeScale => 1 << Mathf.Max(0, maxSpeedExponent);
	public bool IsPaused => Mathf.Approximately(timeScale, 0.0f);
	public ulong SimulationTicksPassed => simulationTicksPassed;

	public event Action<SimulationTickContext> OnSimulationTick;
	public event Action OnWeekPassed;
	public event Action OnMonthPassed;
	public event Action OnYearPassed;
	public event Action<float> OnTimeScaleChanged;

	private void Awake()
	{
		ApplyTimeScale(timeScale, false);
	}


	private void Update()
	{
		float elapsed = Time.deltaTime;
		timeElapsed += elapsed;
		simulationTickElapsed += elapsed;

		while (simulationTickElapsed >= SecondsPerSimulationTick)
		{
			simulationTickElapsed -= SecondsPerSimulationTick;
			++simulationTicksPassed;
			OnSimulationTick?.Invoke(new SimulationTickContext(simulationTicksPassed, SimulationTickWeeks));
		}

		while (timeElapsed >= SecondsPerWeek)
		{
			timeElapsed -= SecondsPerWeek;
			PassWeek();
		}
	}

	private void PassWeek()
	{
		++elapsedWeek;
		OnWeekPassed?.Invoke();

		if (elapsedWeek % 4 == 0)
		{
			PassMonth();
		}
	}

	private void PassMonth()
	{
		++elapsedMonth;
		OnMonthPassed?.Invoke();

		if (elapsedMonth % 12 == 0)
		{
			OnYearPassed?.Invoke();
		}
	}

	public float WeekToSeconds(int weeks)
	{
		return SecondsPerWeek * weeks;
	}

	public void Pause()
	{
		SetTimeScale(0.0f);
	}

	public void PausePreservingSpeed()
	{
		if (preservedPauseCount == 0)
			preservedPauseTimeScale = timeScale;

		++preservedPauseCount;
		ApplyTimeScale(0.0f, true);
	}

	public void ResumePreservedSpeed()
	{
		if (preservedPauseCount <= 0)
			return;

		--preservedPauseCount;
		if (preservedPauseCount == 0)
			ApplyTimeScale(preservedPauseTimeScale, true);
	}

	public void SetNormalSpeed()
	{
		SetTimeScale(1.0f);
	}

	public void DoubleSpeed()
	{
		if (timeScale < 2.0f)
		{
			SetTimeScale(2.0f);
			return;
		}

		SetTimeScale(timeScale * 2.0f);
	}

	public void SetTimeScale(float value)
	{
		preservedPauseCount = 0;
		ApplyTimeScale(value, true);
	}

	private void ApplyTimeScale(float value, bool notify)
	{
		timeScale = Mathf.Clamp(value, 0.0f, MaxTimeScale);
		Time.timeScale = timeScale;

		if (notify)
			OnTimeScaleChanged?.Invoke(timeScale);
	}

	private void OnValidate()
	{
		maxSpeedExponent = Mathf.Max(0, maxSpeedExponent);
		timeScale = Mathf.Clamp(timeScale, 0.0f, MaxTimeScale);
	}
}
