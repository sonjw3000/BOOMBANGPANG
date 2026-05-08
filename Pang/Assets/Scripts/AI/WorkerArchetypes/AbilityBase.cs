using UnityEngine;

public abstract class AbilityBase : MonoBehaviour
{
	protected AIWorker worker;

	public AIWorker Worker => worker;

	private void Start()
	{
		worker = GetComponent<AIWorker>();

		if (worker == null)
		{
			Debug.LogError("No Worker on this object!!");
			return;
		}

		OnInit();

	}
	protected virtual void OnInit() { }
}

