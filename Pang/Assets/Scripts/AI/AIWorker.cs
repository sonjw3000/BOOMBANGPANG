using UnityEngine;

public abstract class AIWorker : MonoBehaviour
{
	public string Name { get; private set; }
	public int WorkerID { get; private set; }
	private BehaviorTree BTMain;

#if UNITY_EDITOR
	[SerializeField] private bool ActionStart = false;
#endif

	public void OnEnable()
	{
		// register AI's BT to AI Manager
		AIManager.Instance.RegisterWorker(this);
	}

	public void OnDisable()
	{
		// unregister AI
		AIManager.Instance.UnregisterWorker(this);
	}

	bool RunBT()
	{


		return true;
	}
}
