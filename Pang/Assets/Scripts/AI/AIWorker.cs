using System.Runtime.CompilerServices;
using Unity.IO.LowLevel.Unsafe;
using UnityEngine;

public abstract class AIWorker : MonoBehaviour
{
	public string Name { get; private set; }
	public int WorkerID { get; private set; }
	protected BehaviorTree BTMain;

#if UNITY_EDITOR
	[SerializeField] private bool ActionStart = false;
#endif

	// should build BT here
	protected abstract void EnableAction();
	protected abstract void DisableAction();

	public void OnEnable()
	{
		// register AI's BT to AI Manager
		WorkerManager.Instance.RegisterWorker(this);

		Debug.Log("AI 등장");
		EnableAction();
	}

	public void OnDisable()
	{
		// unregister AI
		WorkerManager.Instance.UnregisterWorker(this);

		Debug.Log("AI 퇴장");
		DisableAction();
	}

	public bool RunBT()
	{
		BTMain?.RunBT();

		return true;
	}
}
