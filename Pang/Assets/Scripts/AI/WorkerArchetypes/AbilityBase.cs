using UnityEngine;

public abstract class AbilityBase : MonoBehaviour
{
	protected AIWorker worker;
	private bool initialized;

	public AIWorker Worker => worker;

	private void Awake()
	{
		EnsureInitialized();
	}

	private void Start()
	{
		EnsureInitialized();
	}

	protected bool EnsureInitialized()
	{
		if (initialized)
			return worker != null;

		worker = GetComponent<AIWorker>();

		if (worker == null)
		{
			Debug.LogError("No Worker on this object!!");
			return false;
		}

		initialized = true;
		OnInit();
		return true;
	}

	protected virtual void OnInit() { }
}

