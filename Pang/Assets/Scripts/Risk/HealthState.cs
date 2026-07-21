using System;
using UnityEngine;

public interface IHealth
{
	float Health { get; }
	float MaxHealth { get; }

	float ApplyDamage(float amount);
	void RestoreHealth(float value);
}

[Serializable]
public sealed class HealthState
{
	[Tooltip("Maximum health in absolute HP units. This is not a percentage.")]
	[SerializeField, Min(1.0f)] private float maxHealth = 100.0f;
	[SerializeField] private float health = 100.0f;
	[SerializeField, HideInInspector] private bool initialized;

	public float Health
	{
		get
		{
			EnsureInitialized();
			return health;
		}
	}

	public float MaxHealth => Mathf.Max(1.0f, maxHealth);

	public float ApplyDamage(float amount)
	{
		EnsureInitialized();
		if (float.IsNaN(amount) || float.IsInfinity(amount) || amount <= 0.0f || health <= 0.0f)
			return 0.0f;

		float previous = health;
		health = Mathf.Max(0.0f, health - amount);
		return previous - health;
	}

	public void RestoreHealth(float value)
	{
		initialized = true;
		health = float.IsNaN(value) || float.IsInfinity(value)
			? MaxHealth
			: Mathf.Clamp(value, 0.0f, MaxHealth);
	}

	private void EnsureInitialized()
	{
		if (initialized)
			return;

		initialized = true;
		health = MaxHealth;
	}
}
