using System;
using System.Collections.Generic;
using System.IO;
using System.Globalization;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

// One isolated Play session per case. No remote inspection is needed during measurement.
[InitializeOnLoad]
public static class CargoLoadBenchmarkRunner
{
	private const string requestKey = "CargoLoadBenchmark.Request";
	[Serializable]
	private sealed class Request
	{
		public CargoLoadTestSettings settings;
		public string output;
		public bool started;
		public bool captureCpu;
		public bool deepProfile;
		public bool profilerStarted;
		public bool disableWorkforceUi;
	}
	private static Request request;
	private static CargoLoadTestSession session;
	private static double nextCheck;
	private static readonly List<string> errors = new();

	static CargoLoadBenchmarkRunner()
	{
		EditorApplication.update += Tick;
		Application.logMessageReceived += OnLog;
	}

	public static void Run(CargoLoadTestSettings settings, string outputDirectory, bool captureCpu = false,
		bool deepProfile = false, bool disableWorkforceUi = false)
	{
		if (EditorApplication.isPlayingOrWillChangePlaymode || !string.IsNullOrEmpty(SessionState.GetString(requestKey, "")))
			throw new InvalidOperationException("An existing Play session or benchmark is active.");
		if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().path != "Assets/Scenes/GameScene.unity" ||
			UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty)
			throw new InvalidOperationException("Open the saved GameScene before running a benchmark.");
		CargoLoadTestLayout.ValidateSettings(settings);
		request = new Request
		{
			settings = settings,
			output = Path.GetFullPath(outputDirectory),
			captureCpu = captureCpu,
			deepProfile = captureCpu && deepProfile,
			disableWorkforceUi = disableWorkforceUi,
		};
		Directory.CreateDirectory(request.output);
		File.WriteAllText(Path.Combine(request.output, "status.json"), "{\"status\":\"starting\"}");
		SessionState.SetString(requestKey, JsonUtility.ToJson(request));
		UnityEditorInternal.ProfilerDriver.enabled = false;
		UnityEngine.Profiling.Profiler.enabled = false;
		UnityEditorInternal.ProfilerDriver.deepProfiling = request.deepProfile;
		EditorApplication.isPlaying = true;
	}

	private static void Tick()
	{
		if (EditorApplication.timeSinceStartup < nextCheck) return;
		nextCheck = EditorApplication.timeSinceStartup + 0.5;
		if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
		string stored = SessionState.GetString(requestKey, "");
		if (string.IsNullOrEmpty(stored)) return;
		request ??= JsonUtility.FromJson<Request>(stored);
		if (!EditorApplication.isPlaying || EditorApplication.isPaused) return;
		try
		{
			if (!request.started)
			{
				if (!GameContext.HasInstance || !GameContext.Instance.GridService.IsReady || Time.unscaledTime < 1) return;
				request.started = true;
				SessionState.SetString(requestKey, JsonUtility.ToJson(request));
				errors.Clear();
				if (UnityEditorInternal.ProfilerDriver.deepProfiling != request.deepProfile)
					throw new InvalidOperationException("Profiler Deep Profile state does not match the diagnostic request.");
				QualitySettings.vSyncCount = 0;
				Application.targetFrameRate = -1;
				Application.runInBackground = true;
				WorkerVisualDefinition visual = AssetDatabase.LoadAssetAtPath<WorkerVisualDefinition>(
					"Assets/ScriptableObjs/Worker/WorkerArchetypes/Human/FullTime/Visual/FullTime_Visual.asset");
				session = CargoLoadTestWindow.Build(request.settings, visual, null);
				if (request.disableWorkforceUi)
				{
					UniverseLogistics.UI.Toolkit.WorkforceManagementWindow workforceWindow =
						UnityEngine.Object.FindFirstObjectByType<UniverseLogistics.UI.Toolkit.WorkforceManagementWindow>(
							FindObjectsInactive.Include);
					if (workforceWindow != null)
						workforceWindow.enabled = false;
				}
				Selection.activeObject = null;
				GameContext.Instance.GameTime.SetNormalSpeed();
				session.StartSupply(request.settings.supplyInterval, request.settings.operationsPerFrame,
					request.settings.warmupSeconds, request.settings.measureSeconds, request.settings.recoverTestWorkerFatigue);
				WriteStatus("warming");
			}
			else
			{
				if (session == null) session = UnityEngine.Object.FindFirstObjectByType<CargoLoadTestSession>();
				if (session == null) throw new InvalidOperationException("Benchmark session was removed.");
				if (request.captureCpu && !request.profilerStarted && session.IsMeasuring)
				{
					UnityEditorInternal.ProfilerDriver.ClearAllFrames();
					UnityEngine.Profiling.Profiler.enabled = true;
					UnityEditorInternal.ProfilerDriver.enabled = true;
					request.profilerStarted = true;
					SessionState.SetString(requestKey, JsonUtility.ToJson(request));
					WriteStatus(request.deepProfile ? "deep-profiling" : "profiling");
				}
				if (session.IsSupplying) return;
				session.ExportCsv(Path.Combine(request.output, "samples.csv"));
				session.ExportFramesCsv(Path.Combine(request.output, "frames.csv"));
				WriteStatus(session.enabled && !session.State.StartsWith("오류") ? "completed" : "failed");
				if (request.captureCpu && request.profilerStarted)
				{
					CaptureCpu(Path.Combine(request.output, "cpu-samples.csv"));
				}
				Finish();
			}
		}
		catch (Exception exception)
		{
			errors.Add(exception.ToString());
			WriteStatus("failed");
			Finish();
		}
	}

	private static void WriteStatus(string state)
	{
		File.WriteAllText(Path.Combine(request.output, "status.json"), Newtonsoft.Json.JsonConvert.SerializeObject(new
		{
			status = state, utc = DateTime.UtcNow, settings = Newtonsoft.Json.JsonConvert.DeserializeObject(JsonUtility.ToJson(request.settings)),
			deepProfile = UnityEditorInternal.ProfilerDriver.deepProfiling,
			profilerRecording = UnityEditorInternal.ProfilerDriver.enabled,
			workforceUiDisabled = request.disableWorkforceUi,
			vSync = QualitySettings.vSyncCount, targetFps = Application.targetFrameRate,
			unity = Application.unityVersion, cpu = SystemInfo.processorType, gpu = SystemInfo.graphicsDeviceName,
			ramMb = SystemInfo.systemMemorySize, width = Screen.width, height = Screen.height,
			sources = session != null ? session.SourceCount : 0,
			workers = session != null ? session.WorkerCount : 0,
			supplied = session != null ? session.Supplied : 0,
			delivered = session != null ? session.Delivered : 0,
			collected = session != null ? session.Collected : 0,
			lastSample = session != null ? session.LastSample : null,
			errors = errors.ToArray(),
		}, Newtonsoft.Json.Formatting.Indented));
	}
	private static void Finish()
	{
		UnityEditorInternal.ProfilerDriver.enabled = false;
		UnityEngine.Profiling.Profiler.enabled = false;
		UnityEditorInternal.ProfilerDriver.deepProfiling = false;
		SessionState.EraseString(requestKey);
		request = null;
		session = null;
		EditorApplication.isPlaying = false;
	}

	// Diagnostic recording is a separate case, excluded from the unprofiled FPS limit results.
	private static void CaptureCpu(string path)
	{
		var frames = new List<(int index, float ms)>();
		for (int index = UnityEditorInternal.ProfilerDriver.firstFrameIndex; index <= UnityEditorInternal.ProfilerDriver.lastFrameIndex; ++index)
		{
			using var frame = UnityEditorInternal.ProfilerDriver.GetRawFrameDataView(index, 0);
			if (frame.valid) frames.Add((index, frame.frameTimeMs));
		}
		StringBuilder csv = new("frame,frame_ms,sample_ms,self_ms,path\n");
		foreach (var selected in frames.OrderByDescending(f => f.ms).Take(12))
		{
			using var frame = UnityEditorInternal.ProfilerDriver.GetRawFrameDataView(selected.index, 0);
			var parents = new Stack<(int end, string path)>();
			for (int i = 0; i < frame.sampleCount; ++i)
			{
				while (parents.Count > 0 && i > parents.Peek().end) parents.Pop();
				string name = frame.GetSampleName(i);
				string fullPath = parents.Count == 0 ? name : parents.Peek().path + "/" + name;
				int end = i + frame.GetSampleChildrenCountRecursive(i);
				float ms = frame.GetSampleTimeMs(i), self = ms;
				for (int child = i + 1; child <= end; child += 1 + frame.GetSampleChildrenCountRecursive(child))
					self -= frame.GetSampleTimeMs(child);
				if (ms >= 0.05f)
					csv.Append(selected.index).Append(',').Append(selected.ms.ToString("F4", CultureInfo.InvariantCulture)).Append(',')
						.Append(ms.ToString("F4", CultureInfo.InvariantCulture)).Append(',').Append(Mathf.Max(0, self).ToString("F4", CultureInfo.InvariantCulture))
						.Append(',').Append('"').Append(fullPath.Replace("\"", "\"\"")).Append('"').Append('\n');
				if (end > i) parents.Push((end, fullPath));
			}
		}
		File.WriteAllText(path, csv.ToString(), new UTF8Encoding(true));
	}
	private static void OnLog(string message, string stack, LogType type)
	{
		if (request != null && (type == LogType.Error || type == LogType.Exception) && errors.Count < 10)
			errors.Add(message + "\n" + stack);
	}
}
