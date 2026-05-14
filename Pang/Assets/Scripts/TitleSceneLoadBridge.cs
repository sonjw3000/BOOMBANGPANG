using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class TitleSceneLoadBridge : MonoBehaviour
{
	private static TitleSceneLoadBridge instance;

	private TitleSceneStartRequest pendingRequest;
	private string initialSceneName;
	private string finalGameplaySceneName;
	private Coroutine restoreCoroutine;

	public static bool HasPendingRequest => instance != null && instance.pendingRequest.IsValid;

	public static TitleSceneLoadBridge EnsureInstance()
	{
		if (instance != null)
			return instance;

		GameObject bridgeObject = new(nameof(TitleSceneLoadBridge));
		instance = bridgeObject.AddComponent<TitleSceneLoadBridge>();
		DontDestroyOnLoad(bridgeObject);
		return instance;
	}

	public static void BeginRoute(TitleSceneStartRequest request, string initialSceneName, string finalGameplaySceneName)
	{
		TitleSceneLoadBridge bridge = EnsureInstance();
		bridge.pendingRequest = request;
		bridge.initialSceneName = initialSceneName;
		bridge.finalGameplaySceneName = finalGameplaySceneName;
		bridge.LoadScene(initialSceneName);
	}

	public static bool TryGetPendingRequest(out TitleSceneStartRequest request)
	{
		if (instance != null && instance.pendingRequest.IsValid)
		{
			request = instance.pendingRequest;
			return true;
		}

		request = default;
		return false;
	}

	public static void ContinueToFinalScene()
	{
		if (instance == null || string.IsNullOrWhiteSpace(instance.finalGameplaySceneName))
			return;

		instance.LoadScene(instance.finalGameplaySceneName);
	}

	private void OnEnable()
	{
		SceneManager.sceneLoaded += HandleSceneLoaded;
	}

	private void OnDisable()
	{
		SceneManager.sceneLoaded -= HandleSceneLoaded;
	}

	private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
	{
		if (pendingRequest.IsValid == false)
			return;

		if (scene.name != finalGameplaySceneName)
			return;

		if (restoreCoroutine != null)
			StopCoroutine(restoreCoroutine);

		restoreCoroutine = StartCoroutine(RestorePendingSaveAfterSceneLoad());
	}

	private IEnumerator RestorePendingSaveAfterSceneLoad()
	{
		yield return null;

		if (pendingRequest.HasSavePath)
		{
			int waitFrame = 0;
			while ((GameContext.HasInstance == false || GameContext.Instance.SaveService == null) && waitFrame < 120)
			{
				waitFrame++;
				yield return null;
			}

			if (GameContext.HasInstance && GameContext.Instance.SaveService != null)
			{
				GameContext.Instance.SaveService.LoadGame(pendingRequest.SavePath);
			}
			else
			{
				Debug.LogError("[Title] Failed to find GameSaveService after loading gameplay scene.");
			}
		}

		pendingRequest = default;
		restoreCoroutine = null;
		Destroy(gameObject);
	}

	private void LoadScene(string sceneName)
	{
		if (string.IsNullOrWhiteSpace(sceneName))
		{
			Debug.LogError("[Title] Scene name is empty.");
			return;
		}

		SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
	}
}

public readonly struct TitleSceneStartRequest
{
	public TitleSceneStartRequest(string savePath)
	{
		SavePath = savePath;
	}

	public string SavePath { get; }
	public bool HasSavePath => string.IsNullOrWhiteSpace(SavePath) == false;
	public bool IsValid => HasSavePath || SavePath == string.Empty;

	public static TitleSceneStartRequest NewGame()
	{
		return new TitleSceneStartRequest(string.Empty);
	}

	public static TitleSceneStartRequest LoadSave(string savePath)
	{
		return new TitleSceneStartRequest(savePath);
	}
}
