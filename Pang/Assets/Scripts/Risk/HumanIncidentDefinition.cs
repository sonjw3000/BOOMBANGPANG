using System;
using System.Collections.Generic;
using UnityEngine;

public enum HumanIncidentType
{
	WorkMistake,
	MinorInjury,
	Collapse
}

public enum HumanIncidentCause
{
	None,
	Fatigue,
	Overwork,
	Overload,
	UnqualifiedHazard,
}

[Serializable]
public struct IncidentChancePoint
{
	[Min(0.0f)] public float RiskScore;
	[Range(0.0f, 1.0f)] public float Chance;

	public IncidentChancePoint(float riskScore, float chance)
	{
		RiskScore = riskScore;
		Chance = chance;
	}
}

[Serializable]
public struct IncidentSeverityBand
{
	[Min(0.0f)] public float MinimumRiskScore;
	[Min(0.0f)] public float MistakeWeight;
	[Min(0.0f)] public float MinorInjuryWeight;
	[Min(0.0f)] public float MajorIncidentWeight;

	public IncidentSeverityBand(
		float minimumRiskScore,
		float mistakeWeight,
		float minorInjuryWeight,
		float majorIncidentWeight)
	{
		MinimumRiskScore = minimumRiskScore;
		MistakeWeight = mistakeWeight;
		MinorInjuryWeight = minorInjuryWeight;
		MajorIncidentWeight = majorIncidentWeight;
	}
}

[Serializable]
public struct HandlingFatiguePoint
{
	[Min(0.0f)] public float LoadRatio;
	[Min(0.0f)] public float Multiplier;

	public HandlingFatiguePoint(float loadRatio, float multiplier)
	{
		LoadRatio = loadRatio;
		Multiplier = multiplier;
	}
}

[CreateAssetMenu(menuName = "Risk/Human Incident Definition")]
public class HumanIncidentDefinition : ScriptableObject
{
	[SerializeField] private List<IncidentChancePoint> chancePoints = new()
	{
		new IncidentChancePoint(0.0f, 0.0f),
		new IncidentChancePoint(59.0f, 0.0f),
		new IncidentChancePoint(60.0f, 0.05f),
		new IncidentChancePoint(80.0f, 0.50f),
		new IncidentChancePoint(95.0f, 1.0f),
	};

	[SerializeField] private List<IncidentSeverityBand> severityBands = new()
	{
		new IncidentSeverityBand(60.0f, 85.0f, 15.0f, 0.0f),
		new IncidentSeverityBand(70.0f, 70.0f, 28.0f, 2.0f),
		new IncidentSeverityBand(80.0f, 50.0f, 42.0f, 8.0f),
		new IncidentSeverityBand(90.0f, 30.0f, 50.0f, 20.0f),
		new IncidentSeverityBand(95.0f, 15.0f, 45.0f, 40.0f),
		new IncidentSeverityBand(105.0f, 5.0f, 30.0f, 65.0f),
	};

	[SerializeField] private List<HandlingFatiguePoint> handlingFatiguePoints = new()
	{
		new HandlingFatiguePoint(0.0f, 0.75f),
		new HandlingFatiguePoint(0.5f, 1.0f),
		new HandlingFatiguePoint(1.0f, 1.5f),
		new HandlingFatiguePoint(1.5f, 2.0f),
		new HandlingFatiguePoint(2.0f, 3.0f),
		new HandlingFatiguePoint(3.0f, 4.0f),
	};

	[Header("Unsafe Exposure")]
	[SerializeField, Min(0.0f)] private float maximumUnsafeExposure = 40.0f;
	[SerializeField, Min(0.0f)] private float unqualifiedHazardExposure = 10.0f;
	[SerializeField, Min(0.0f)] private float recoveryExposurePerSecond = 2.0f;

	[Header("Incident Effects")]
	[SerializeField, Min(0.0f)] private float mistakeCleanupSeconds = 6.0f;
	[SerializeField] private Vector2 minorInjuryHealthDamage = new(5.0f, 20.0f);
	[SerializeField] private Vector2 majorIncidentHealthDamage = new(30.0f, 60.0f);
	[SerializeField] private Vector2 mistakeItemDamage = new(5.0f, 15.0f);
	[SerializeField, Range(0.0f, 1.0f)] private float mistakeItemDamageChance = 0.5f;
	[SerializeField, Min(1.0f)] private float fragileDamageChanceMultiplier = 1.5f;
	[SerializeField, Min(1.0f)] private float fragileDamageAmountMultiplier = 1.5f;

	public float MaximumUnsafeExposure => Mathf.Max(0.0f, maximumUnsafeExposure);
	public float UnqualifiedHazardExposure => Mathf.Max(0.0f, unqualifiedHazardExposure);
	public float RecoveryExposurePerSecond => Mathf.Max(0.0f, recoveryExposurePerSecond);
	public float MistakeCleanupSeconds => Mathf.Max(0.0f, mistakeCleanupSeconds);
	public float MistakeItemDamageChance => Mathf.Clamp01(mistakeItemDamageChance);
	public float FragileDamageChanceMultiplier => Mathf.Max(1.0f, fragileDamageChanceMultiplier);
	public float FragileDamageAmountMultiplier => Mathf.Max(1.0f, fragileDamageAmountMultiplier);

	public float EvaluateIncidentChance(float riskScore)
		=> EvaluatePoints(chancePoints, riskScore, point => point.RiskScore, point => point.Chance, 0.0f);

	public float EvaluateHandlingFatigueMultiplier(float loadRatio)
		=> EvaluatePoints(
			handlingFatiguePoints,
			Mathf.Max(0.0f, loadRatio),
			point => point.LoadRatio,
			point => point.Multiplier,
			1.0f);

	public IncidentSeverityBand GetSeverityBand(float riskScore)
	{
		if (severityBands == null || severityBands.Count == 0)
			return new IncidentSeverityBand(0.0f, 1.0f, 0.0f, 0.0f);

		IncidentSeverityBand selected = severityBands[0];
		for (int i = 1; i < severityBands.Count; ++i)
		{
			if (riskScore < severityBands[i].MinimumRiskScore)
				break;

			selected = severityBands[i];
		}

		return selected;
	}

	public float GetOverworkExposure(float fatigue)
	{
		if (fatigue >= 90.0f) return 4.0f;
		if (fatigue >= 80.0f) return 2.0f;
		if (fatigue >= 70.0f) return 1.0f;
		return 0.0f;
	}

	public float GetOverloadExposure(float loadRatio)
	{
		if (loadRatio > 3.0f) return 10.0f;
		if (loadRatio > 2.0f) return 5.0f;
		if (loadRatio > 1.5f) return 3.0f;
		if (loadRatio > 1.0f) return 1.0f;
		return 0.0f;
	}

	public float GetHealthDamage(HumanIncidentType type, float roll)
	{
		Vector2 range = type == HumanIncidentType.Collapse
			? majorIncidentHealthDamage
			: minorInjuryHealthDamage;
		float minimum = Mathf.Max(0.0f, Mathf.Min(range.x, range.y));
		float maximum = Mathf.Max(minimum, Mathf.Max(range.x, range.y));
		return Mathf.Lerp(minimum, maximum, Mathf.Clamp01(roll));
	}

	public float GetMistakeItemDamage(float roll, bool fragile)
	{
		float minimum = Mathf.Max(0.0f, Mathf.Min(mistakeItemDamage.x, mistakeItemDamage.y));
		float maximum = Mathf.Max(minimum, Mathf.Max(mistakeItemDamage.x, mistakeItemDamage.y));
		float damage = Mathf.Lerp(minimum, maximum, Mathf.Clamp01(roll));
		return fragile ? damage * FragileDamageAmountMultiplier : damage;
	}

	private static float EvaluatePoints<T>(
		IReadOnlyList<T> points,
		float input,
		Func<T, float> getInput,
		Func<T, float> getOutput,
		float fallback)
	{
		if (points == null || points.Count == 0)
			return fallback;

		if (input <= getInput(points[0]))
			return getOutput(points[0]);

		for (int i = 1; i < points.Count; ++i)
		{
			float upperInput = getInput(points[i]);
			if (input > upperInput)
				continue;

			float lowerInput = getInput(points[i - 1]);
			float range = upperInput - lowerInput;
			if (range <= Mathf.Epsilon)
				return getOutput(points[i]);

			float t = Mathf.Clamp01((input - lowerInput) / range);
			return Mathf.Lerp(getOutput(points[i - 1]), getOutput(points[i]), t);
		}

		return getOutput(points[points.Count - 1]);
	}

	private void OnValidate()
	{
		ValidateAscending(chancePoints, point => point.RiskScore, nameof(chancePoints));
		ValidateAscending(severityBands, band => band.MinimumRiskScore, nameof(severityBands));
		ValidateAscending(handlingFatiguePoints, point => point.LoadRatio, nameof(handlingFatiguePoints));
	}

	private void ValidateAscending<T>(IReadOnlyList<T> values, Func<T, float> getValue, string fieldName)
	{
		if (values == null || values.Count == 0)
		{
			Debug.LogWarning($"[{nameof(HumanIncidentDefinition)}] {fieldName} is empty on {name}.", this);
			return;
		}

		for (int i = 1; i < values.Count; ++i)
		{
			if (getValue(values[i]) <= getValue(values[i - 1]))
			{
				Debug.LogWarning($"[{nameof(HumanIncidentDefinition)}] {fieldName} must be strictly ascending on {name}.", this);
				return;
			}
		}
	}
}
