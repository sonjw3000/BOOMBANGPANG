using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

public class TimeControlUI : MonoBehaviour
{
	[SerializeField] private Button pauseButton;
	[SerializeField] private Button normalSpeedButton;
	[FormerlySerializedAs("increaseSpeedButton")]
	[SerializeField] private Button doubleSpeedButton;
	[SerializeField] private TMP_Text pauseLabel;
	[SerializeField] private TMP_Text normalSpeedLabel;
	[FormerlySerializedAs("increaseSpeedLabel")]
	[SerializeField] private TMP_Text doubleSpeedLabel;
	[SerializeField] private TMP_Text currentSpeedLabel;

	private GameTime GameTime => GameContext.Instance.GameTime;

	private void OnEnable()
	{
		if (pauseButton != null)
			pauseButton.onClick.AddListener(Pause);
		if (normalSpeedButton != null)
			normalSpeedButton.onClick.AddListener(SetNormalSpeed);
		if (doubleSpeedButton != null)
			doubleSpeedButton.onClick.AddListener(DoubleSpeed);

		if (GameContext.HasInstance && GameTime != null)
			GameTime.OnTimeScaleChanged += OnTimeScaleChanged;

		RefreshLabels();
	}

	private void OnDisable()
	{
		if (pauseButton != null)
			pauseButton.onClick.RemoveListener(Pause);
		if (normalSpeedButton != null)
			normalSpeedButton.onClick.RemoveListener(SetNormalSpeed);
		if (doubleSpeedButton != null)
			doubleSpeedButton.onClick.RemoveListener(DoubleSpeed);

		if (GameContext.HasInstance && GameTime != null)
			GameTime.OnTimeScaleChanged -= OnTimeScaleChanged;
	}

	private void Pause()
	{
		GameTime?.Pause();
	}

	private void SetNormalSpeed()
	{
		GameTime?.SetNormalSpeed();
	}

	private void DoubleSpeed()
	{
		GameTime?.DoubleSpeed();
	}

	private void OnTimeScaleChanged(float _)
	{
		RefreshLabels();
	}

	private void RefreshLabels()
	{
		if (pauseLabel != null)
			pauseLabel.text = "Pause";
		if (normalSpeedLabel != null)
			normalSpeedLabel.text = "1x";

		float currentScale = GameContext.HasInstance && GameTime != null ? GameTime.TimeScale : 1.0f;
		int maxScale = GameContext.HasInstance && GameTime != null ? GameTime.MaxTimeScale : 8;
		int nextScale = GetNextSpeed(currentScale, maxScale);

		if (doubleSpeedLabel != null)
			doubleSpeedLabel.text = $"{nextScale}x";
		if (currentSpeedLabel != null)
			currentSpeedLabel.text = currentScale <= 0.0f ? "Paused" : $"{currentScale:0.#}x";
		if (doubleSpeedButton != null)
			doubleSpeedButton.interactable = currentScale < maxScale;
	}

	private static int GetNextSpeed(float currentScale, int maxScale)
	{
		if (currentScale < 2.0f)
			return Mathf.Min(2, maxScale);

		return Mathf.Min(Mathf.RoundToInt(currentScale * 2.0f), maxScale);
	}
}
