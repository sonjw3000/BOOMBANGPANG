using System.Collections.Generic;
using UnityEngine;

public abstract class WorkforceMarketData_SO : ScriptableObject
{
	// full, part, illegal, transport robot, etc
	[SerializeField] protected string workforceMarketName = string.Empty;

	public string WorkForceMarketName => workforceMarketName;

	protected static void ClampMinimum<T>(ref T min, ref T max) where T : System.IComparable<T>
	{
		if (min.CompareTo(max) > 0)
			min = max;
	}

	public virtual int GetMaxCount() { return int.MaxValue; }

	protected abstract void OnValidation();
	public abstract void FillWorkerArchetype(WorkerArchetype target, System.Random rng, int page, int count);

	private void OnValidate()
	{
		OnValidation();
	}
}
