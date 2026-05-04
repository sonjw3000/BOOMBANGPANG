using System;
using UnityEngine;

public class GameTime : MonoBehaviour
{
	[Header("게임 시간 설정")]
	[Tooltip("현실 시간 기준 1개월이 몇 초인가?")]
	[SerializeField] private float secondsPerMonth = 120f;

	[Header("게임 시간 배율")]
	[SerializeField] private float timeScale = 1.0f;

	private float SecondsPerWeek => secondsPerMonth / 4.0f;
	private float timeElapsed = 0f;

	private int elapsedWeek = 0;
	private int elapsedMonth = 0;

	public int Week => elapsedWeek % 4 + 1;
	public int Month => elapsedMonth % 12 + 1;
	public int Year => elapsedMonth / 12;

	public int WeeksPassed => elapsedWeek;
	public int MonthsPassed => elapsedMonth;

	public event Action OnWeekPassed;
	public event Action OnMonthPassed;
	public event Action OnYearPassed;


	private void Update()
	{
		// todo
		// UI로 빼자
		Time.timeScale = timeScale;

		timeElapsed += Time.deltaTime;

		if (timeElapsed >= SecondsPerWeek)
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
}
