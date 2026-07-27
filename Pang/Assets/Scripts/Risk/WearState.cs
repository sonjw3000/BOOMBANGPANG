using System;
using UnityEngine;

public interface IWearable
{
	float Wear { get; }
	float WearEfficiency { get; }
	float PassiveWearPerQuarterWeek { get; }
	float OperatingWearPerQuarterWeek { get; }

	void ApplyWear(float amount);
	void SetWearFromSave(float value);
}

public interface IWearableFacility : IFacility, IWearable
{
}

[Serializable]
public sealed class WearState
{
	[SerializeField, Range(0.0f, 1.0f)] private float wear;
	[SerializeField, Min(0.0f)] private float passiveWearPerQuarterWeek = 0.00001f;
	[SerializeField, Min(0.0f)] private float operatingWearPerQuarterWeek = 0.00025f;

	public float Wear => wear;
	public float Efficiency => 1.0f - 0.9f * wear;
	public float PassiveWearPerQuarterWeek => passiveWearPerQuarterWeek;
	public float OperatingWearPerQuarterWeek => operatingWearPerQuarterWeek;

	public void Apply(float amount)
	{
		if (float.IsNaN(amount) || float.IsInfinity(amount) || amount <= 0.0f)
			return;

		wear = Mathf.Clamp01(wear + amount);
	}

	public void SetFromSave(float value)
	{
		wear = float.IsNaN(value) || float.IsInfinity(value)
			? 0.0f
			: Mathf.Clamp01(value);
	}
}
