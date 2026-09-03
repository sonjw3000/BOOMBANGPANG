using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

public sealed class PangBuildWindow : EditorWindow
{
	[SerializeField] private bool enableCheats;

	[MenuItem("Tools/Universe Logistics/Build Windows")]
	public static void Open()
	{
		GetWindow<PangBuildWindow>("Pang Build").minSize = new Vector2(380f, 170f);
	}

	private void OnGUI()
	{
		EditorGUILayout.LabelField("Windows x64 Build", EditorStyles.boldLabel);
		enableCheats = EditorGUILayout.Toggle("Enable Cheat Mode", enableCheats);
		EditorGUILayout.HelpBox(
			enableCheats
				? "F1 opens Debug Controls. Enables the existing affordability bypass and worker debug controls. Costs are still recorded."
				: "Cheat mode is disabled in this build.",
			MessageType.Info);
		EditorGUILayout.LabelField("Uses the enabled scenes in Build Profiles.");

		using (new EditorGUI.DisabledScope(EditorApplication.isCompiling ||
			EditorApplication.isPlayingOrWillChangePlaymode || BuildPipeline.isBuildingPlayer))
		{
			if (GUILayout.Button("Build Windows x64..."))
			{
				if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo() == false)
					return;

				string directory = Path.GetFullPath(enableCheats ? "Builds/Windows-Cheats" : "Builds/Windows");
				Directory.CreateDirectory(directory);
				string outputPath = EditorUtility.SaveFilePanel("Build Windows x64", directory, "Pang", "exe");
				if (string.IsNullOrEmpty(outputPath))
					return;

				BuildReport report = BuildWindows(outputPath, enableCheats);
				if (report.summary.result == BuildResult.Succeeded)
					EditorUtility.RevealInFinder(report.summary.outputPath);
			}
		}
	}

	public static BuildReport BuildWindows(string outputPath, bool enableCheats)
	{
		if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || BuildPipeline.isBuildingPlayer)
			throw new BuildFailedException("Exit Play Mode and wait for compilation or the current build to finish.");
		if (string.IsNullOrWhiteSpace(outputPath) || Path.GetExtension(outputPath).Equals(".exe", StringComparison.OrdinalIgnoreCase) == false)
			throw new BuildFailedException("Choose an .exe output path.");
		if (BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64) == false)
			throw new BuildFailedException("Windows x64 build support is not installed.");

		string[] scenes = EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path).ToArray();
		if (scenes.Length == 0)
			throw new BuildFailedException("Enable at least one scene in Build Profiles.");
		if (PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.Standalone).Split(';').Contains("PANG_CHEATS"))
			throw new BuildFailedException("Remove PANG_CHEATS from Player Settings; use Enable Cheat Mode in this window.");

		Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPath)));
		BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
		{
			scenes = scenes,
			locationPathName = outputPath,
			target = BuildTarget.StandaloneWindows64,
			options = BuildOptions.DetailedBuildReport,
			extraScriptingDefines = enableCheats ? new[] { "PANG_CHEATS" } : Array.Empty<string>(),
		});

		string message = $"[PangBuild] {report.summary.result}, Cheats={enableCheats}, Errors={report.summary.totalErrors}, Path={report.summary.outputPath}";
		if (report.summary.result == BuildResult.Succeeded)
			Debug.Log(message);
		else
			Debug.LogError(message);
		return report;
	}
}
