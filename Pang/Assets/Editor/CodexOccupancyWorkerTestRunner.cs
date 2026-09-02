using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

[InitializeOnLoad]
internal static class CodexOccupancyWorkerTestRunner
{
	private const string RequestPath = "Temp/CodexOccupancyWorkerTestRequest.txt";
	private const string ResultPath = "Temp/CodexOccupancyWorkerTestResults.txt";
	private const string XmlPath = "Temp/CodexOccupancyWorkerTestResults.xml";
	private const string RunningKey = "Codex.OccupancyWorkerTests.Running";

	private static TestRunnerApi testRunnerApi;
	private static ResultCallbacks callbacks;

	static CodexOccupancyWorkerTestRunner()
	{
		if (File.Exists(RequestPath))
		{
			EditorApplication.delayCall += TryRun;
		}
	}

	private static void TryRun()
	{
		if (!File.Exists(RequestPath) || SessionState.GetBool(RunningKey, false))
		{
			return;
		}

		if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
		{
			EditorApplication.delayCall += TryRun;
			return;
		}

		string[] dirtyScenes = Enumerable.Range(0, EditorSceneManager.sceneCount)
			.Select(EditorSceneManager.GetSceneAt)
			.Where(scene => scene.isDirty)
			.Select(scene => string.IsNullOrEmpty(scene.path) ? scene.name : scene.path)
			.ToArray();

		if (dirtyScenes.Length > 0)
		{
			WriteTerminalResult("BLOCKED\tDirtyScenes\t" + string.Join(";", dirtyScenes));
			Debug.LogWarning("[CodexOccupancyWorkerTests] Aborted because open scenes are dirty: " + string.Join(", ", dirtyScenes));
			return;
		}

		SessionState.SetBool(RunningKey, true);
		File.WriteAllText(ResultPath, "STARTED\t" + DateTime.UtcNow.ToString("O"));

		try
		{
			testRunnerApi = ScriptableObject.CreateInstance<TestRunnerApi>();
			callbacks = new ResultCallbacks();
			testRunnerApi.RegisterCallbacks(callbacks);

			ExecutionSettings settings = new ExecutionSettings(
				CreateFilter("(^|\\.)GridServiceWorkerRelocationEditModeTests$"),
				CreateFilter("(^|\\.)RobotHumanCollisionEditModeTests$"))
			{
				runSynchronously = true
			};

			Debug.Log("[CodexOccupancyWorkerTests] Starting focused EditMode tests.");
			testRunnerApi.Execute(settings);
		}
		catch (Exception exception)
		{
			WriteTerminalResult("ERROR\t" + Sanitize(exception.ToString()));
			Debug.LogException(exception);
		}
	}

	private static Filter CreateFilter(string groupName)
	{
		return new Filter
		{
			testMode = TestMode.EditMode,
			groupNames = new[] { groupName }
		};
	}

	private static void WriteTerminalResult(string content)
	{
		File.WriteAllText(ResultPath, content);
		File.Delete(RequestPath);
		SessionState.SetBool(RunningKey, false);
	}

	private static string Sanitize(string value)
	{
		return string.IsNullOrEmpty(value)
			? string.Empty
			: value.Replace("\r", "\\r").Replace("\n", "\\n").Replace("\t", "\\t");
	}

	private sealed class ResultCallbacks : ICallbacks
	{
		private readonly List<string> leafResults = new List<string>();

		public void RunStarted(ITestAdaptor testsToRun)
		{
		}

		public void RunFinished(ITestResultAdaptor result)
		{
			StringBuilder builder = new StringBuilder();
			builder.Append("SUMMARY\t")
				.Append(result.TestStatus).Append('\t')
				.Append(result.PassCount).Append('\t')
				.Append(result.FailCount).Append('\t')
				.Append(result.SkipCount).Append('\t')
				.Append(result.InconclusiveCount).Append('\t')
				.Append(result.Duration.ToString("R"));

			foreach (string leafResult in leafResults)
			{
				builder.AppendLine().Append(leafResult);
			}

			File.WriteAllText(XmlPath, result.ToXml().ToString());
			WriteTerminalResult(builder.ToString());
			Debug.Log($"[CodexOccupancyWorkerTests] Finished: {result.TestStatus}, passed={result.PassCount}, failed={result.FailCount}, skipped={result.SkipCount}.");
		}

		public void TestStarted(ITestAdaptor test)
		{
		}

		public void TestFinished(ITestResultAdaptor result)
		{
			if (result.HasChildren)
			{
				return;
			}

			leafResults.Add(string.Join("\t", new[]
			{
				"TEST",
				result.FullName,
				result.TestStatus.ToString(),
				result.Duration.ToString("R"),
				Sanitize(result.Message),
				Sanitize(result.StackTrace)
			}));
		}
	}
}
