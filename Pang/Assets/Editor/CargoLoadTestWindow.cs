using System;
using System.Collections.Generic;
using System.IO;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

public sealed class CargoLoadTestWindow : EditorWindow
{
	[SerializeField] private CargoLoadTestSettings settings = new();
	[SerializeField] private WorkerVisualDefinition workerVisual;
	[SerializeField] private ItemDefinition payloadItem;
	private CargoLoadTestSession session;
	private Vector2 scroll;
	private string message = "GameScene을 실행한 후 배치 영역을 확인하세요.";
	private double nextRepaint;

	[MenuItem("Tools/Universe Logistics/Cargo Load Test")]
	public static void Open() => GetWindow<CargoLoadTestWindow>("Cargo Load Test");

	private void OnEnable()
	{
		minSize = new Vector2(440, 620);
		workerVisual ??= AssetDatabase.LoadAssetAtPath<WorkerVisualDefinition>(
			"Assets/ScriptableObjs/Worker/WorkerArchetypes/Human/FullTime/Visual/FullTime_Visual.asset");
	}
	private void OnInspectorUpdate()
	{
		if (EditorApplication.timeSinceStartup < nextRepaint) return;
		nextRepaint = EditorApplication.timeSinceStartup + 0.5;
		if (session == null && EditorApplication.isPlaying)
			session = FindFirstObjectByType<CargoLoadTestSession>();
		Repaint();
	}

	private void OnGUI()
	{
		scroll = EditorGUILayout.BeginScrollView(scroll);
		EditorGUILayout.HelpBox("Play Mode 전용 임시 테스트입니다. 중앙 빈 공간은 비워 두고 외곽 포트만 사용합니다. Play 종료로 초기화하며 테스트 상태를 게임 저장/불러오기에 사용하지 마세요.", MessageType.Info);
		bool ready = EditorApplication.isPlaying && !EditorApplication.isPaused && !EditorApplication.isCompiling &&
			GameContext.HasInstance && GameContext.Instance.GridService != null && GameContext.Instance.GridService.IsReady;
		using (new EditorGUI.DisabledScope(session != null))
		{
			settings.pairCount = EditorGUILayout.IntField("건물 쌍 개수 n", settings.pairCount);
			settings.diameter = EditorGUILayout.IntField("+ 전체 크기 (홀수)", settings.diameter);
			settings.armWidth = EditorGUILayout.IntField("팔 너비 (홀수)", settings.armWidth);
			settings.holeSize = EditorGUILayout.IntField("중앙 빈 공간 (홀수)", settings.holeSize);
			settings.gap = EditorGUILayout.IntField("최소 건물 간격", settings.gap);
			settings.workers = EditorGUILayout.IntField("실외 작업자 m", settings.workers);
			settings.seed = EditorGUILayout.IntField("Random Seed", settings.seed);
			settings.maxAttempts = EditorGUILayout.IntField("최대 배치 시도", settings.maxAttempts);
			settings.bounds = EditorGUILayout.RectIntField("배치 영역 (X, Z)", settings.bounds);
			using (new EditorGUI.DisabledScope(!ready))
				if (GUILayout.Button("현재 맵 전체로 영역 설정"))
				{
					int3 size = GameContext.Instance.GridService.MapSize;
					settings.bounds = new RectInt(2, 2, size.x - 4, size.z - 4);
				}
			workerVisual = (WorkerVisualDefinition)EditorGUILayout.ObjectField("인간 작업자 외형", workerVisual, typeof(WorkerVisualDefinition), false);
			payloadItem = (ItemDefinition)EditorGUILayout.ObjectField("캡슐 내용물 (1개)", payloadItem, typeof(ItemDefinition), false);
			EditorGUILayout.HelpBox("내용물 미지정 시 현재 ItemDatabase의 첫 아이템을 사용합니다. 생성 비용이 부족하면 EconomyService로 테스트 자금을 지급합니다.", MessageType.None);
			using (new EditorGUI.DisabledScope(!ready))
			{
				if (GUILayout.Button("배치 검사만 실행")) Execute(() =>
				{
					CargoLoadTestLayout plan = CargoLoadTestLayout.Plan(settings, GameContext.Instance.GridService, GameContext.Instance.AreaMgr);
					message = $"검사 통과: {plan.Centers.Count}개 건물, 건물당 {plan.Ports.Count}개 포트, 접근 가능한 포트 {plan.ReachablePortCount}개. 실제 생성 전입니다.";
				});
				if (GUILayout.Button("테스트 환경 생성")) Execute(() =>
				{
					session = Build(settings, workerVisual, payloadItem);
					message = $"생성 완료: {settings.pairCount}쌍, OB {session.SourceCount} / IB {session.DestinationCount}, 작업자 {session.WorkerCount}.";
				});
			}
		}
		EditorGUILayout.Space();
		using (new EditorGUI.DisabledScope(session != null && session.IsSupplying))
		{
			settings.supplyInterval = EditorGUILayout.FloatField("소스 재공급 간격 (실제 초)", settings.supplyInterval);
			settings.operationsPerFrame = EditorGUILayout.IntField("프레임당 공급/회수 상한", settings.operationsPerFrame);
			settings.warmupSeconds = EditorGUILayout.FloatField("워밍업 (실제 초)", settings.warmupSeconds);
			settings.measureSeconds = EditorGUILayout.FloatField("측정 (실제 초)", settings.measureSeconds);
			settings.recoverTestWorkerFatigue = EditorGUILayout.Toggle("테스트 작업자 피로 회복", settings.recoverTestWorkerFatigue);
		}
		using (new EditorGUI.DisabledScope(!ready || session == null || !session.IsConfigured || session.IsSupplying || session.InFlight != 0))
			if (GUILayout.Button("캡슐 공급 및 측정 시작")) Execute(() =>
			{
				CargoLoadTestLayout.ValidateSettings(settings);
				session.StartSupply(settings.supplyInterval, settings.operationsPerFrame, settings.warmupSeconds, settings.measureSeconds, settings.recoverTestWorkerFatigue);
			});
		using (new EditorGUI.DisabledScope(session == null || !session.IsConfigured))
		{
			if (GUILayout.Button("공급 중지 (기존 운반은 계속)")) session.StopSupply();
			if (GUILayout.Button("CSV 저장")) Execute(() =>
			{
				string path = EditorUtility.SaveFilePanel("Cargo Load Test CSV", Path.GetFullPath("Temp"), "cargo-load-" + DateTime.Now.ToString("yyyyMMdd-HHmmss"), "csv");
				if (!string.IsNullOrEmpty(path)) { session.ExportCsv(path); message = "저장: " + path; }
			});
		}
		EditorGUILayout.HelpBox(message, MessageType.None);
		if (!ready) EditorGUILayout.HelpBox("초기화가 끝난 Play Mode에서 사용할 수 있습니다. Editor Pause도 해제하세요.", MessageType.Info);
		if (session != null)
		{
			EditorGUILayout.LabelField("상태", session.State);
			EditorGUILayout.LabelField($"공급 {session.Supplied} / IB 도착 {session.Delivered} / 회수 {session.Collected}");
			EditorGUILayout.LabelField("마지막 측정", session.LastSample, EditorStyles.wordWrappedLabel);
			if (session.WorkerCount > session.SourceCount)
				EditorGUILayout.HelpBox("작업자 수가 OB 소스 수보다 많습니다. 소스당 운반 하나를 유지하므로 일부 작업자는 대기합니다.", MessageType.Info);
		}
		EditorGUILayout.HelpBox("Main Thread 시간에는 대기·에디터 비용이 포함됩니다. 공급·회수·테스트 피로 회복 비용은 CargoLoadTest.FeedAndCollect로 분리됩니다. 피로 회복은 생성한 작업자에게 기존 회복 API를 호출하므로 위험 노출도도 감소합니다. 산소·사고 판정 로직은 유지됩니다.", MessageType.None);
		EditorGUILayout.EndScrollView();
	}

	private void Execute(Action action)
	{
		try { action(); }
		catch (Exception exception)
		{
			message = exception.Message;
			Debug.LogException(exception);
			session = FindFirstObjectByType<CargoLoadTestSession>();
		}
	}

	public static CargoLoadTestSession Build(CargoLoadTestSettings settings, WorkerVisualDefinition visual, ItemDefinition payload)
	{
		if (!EditorApplication.isPlaying || EditorApplication.isPaused || !GameContext.HasInstance || !GameContext.Instance.GridService.IsReady)
			throw new InvalidOperationException("초기화된 Play Mode에서 실행하세요.");
		if (FindFirstObjectByType<CargoLoadTestSession>() != null)
			throw new InvalidOperationException("한 Play 세션에 한 번 생성할 수 있습니다. Play를 종료 후 다시 실행하세요.");
		GameContext context = GameContext.Instance;
		CargoLoadTestLayout plan = CargoLoadTestLayout.Plan(settings, context.GridService, context.AreaMgr);
		PlaceableDefinition ob = RequireDefinition(context, "OBCargoPort_00");
		PlaceableDefinition ib = RequireDefinition(context, "IBCargoPort_00");
		PlaceableDefinition wall = RequireDefinition(context, "wall_00");
		if (payload == null && context.ItemDB.OrderedItems.Count > 0) payload = context.ItemDB.OrderedItems[0];
		if (payload == null || !context.ItemDB.GetItemData(payload.ItemID, out _))
			throw new InvalidOperationException("현재 ItemDatabase에 등록된 내용물 아이템을 선택하세요.");
		if (context.BuildingFootprintService == null || context.WorkerSpawnMgr == null || context.BoxMgr == null)
			throw new InvalidOperationException("건물/작업자/캡슐 서비스가 없습니다.");
		ValidatePortFootprint(ob, outbound: true);
		ValidatePortFootprint(ib, outbound: false);

		GameObject root = new("Cargo Load Test (Play session only)");
		CargoLoadTestSession session = root.AddComponent<CargoLoadTestSession>();
		List<OutboundCargoPort> sources = new();
		List<InboundCargoPort> destinations = new();
		List<AIWorker> workers = new();
		List<Building> buildings = new();
		UnityEngine.Random.State randomState = UnityEngine.Random.state;
		BuildingFootprintPreset previousPreset = context.BuildingFootprintService.ActivePreset;
		context.GameTime.PausePreservingSpeed();
		try
		{
			UnityEngine.Random.InitState(settings.seed);
			int wallCount = 0;
			foreach (BuildingFootprintCell cell in plan.Cells) if (cell.IsWall) ++wallCount;
			int cost = checked(wallCount * wall.Cost * plan.Centers.Count + plan.Ports.Count * (ob.Cost + ib.Cost) * settings.pairCount);
			if (!context.EconomyService.CanAfford(cost))
				context.EconomyService.ApplyTransaction(new EconomyTransaction { moneyDelta = cost, reason = EconomyTransaction.Reason.DebugAdjustment });
			BuildingFootprintPreset preset = CreatePreset(settings, plan);
			RegisterTemporaryPreset(context.BuildingFootprintService, preset);
			for (int i = 0; i < plan.Centers.Count; ++i)
			{
				EditorUtility.DisplayProgressBar("Cargo Load Test", $"건물 {i + 1}/{plan.Centers.Count}", i / (float)plan.Centers.Count);
				Vector2Int center = plan.Centers[i];
				if (!context.BuildingFootprintService.TryCreateFootprint(0, new int3(center.x, 0, center.y), out string reason))
					throw new InvalidOperationException(reason);
				BuildingFootprintRecord record = context.BuildingFootprintService.RegisteredFootprints[^1];
				if (!context.BuildingMgr.TryGetBuilding(record.RuntimeBuildingId, out Building building))
					throw new InvalidOperationException("Building 등록 실패.");
				bool outbound = (i & 1) == 0;
				building.Rename($"LoadTest {i / 2 + 1} {(outbound ? "OB" : "IB")}");
				buildings.Add(building);
				foreach (CargoLoadTestLayout.Port port in plan.Ports)
				{
					Vector2Int pos = center + port.Cell;
					FacingDirection facing = outbound ? port.Facing : Opposite(port.Facing);
					PlacementContext placement = new(new int3(pos.x, 0, pos.y), facing, outbound ? ob : ib);
					if (!context.GridService.OnInstall(placement)) throw new InvalidOperationException($"포트 설치 실패: {pos}");
					GameObject installed = context.GridService.GetCell(pos.x, 0, pos.y).OccupancyObjectOnGrid;
					if (outbound) sources.Add(installed.GetComponent<OutboundCargoPort>());
					else destinations.Add(installed.GetComponent<InboundCargoPort>());
				}
			}
			for (int i = 0; i < buildings.Count; i += 2)
				if (!context.BuildingMgr.TryLinkBuildings(buildings[i], buildings[i + 1]))
					throw new InvalidOperationException("건물 쌍 연결 실패.");
			plan.ValidateReachability(context.GridService, settings.diameter, planned: false);
			Area spawnArea = context.AreaMgr.AddArea("Cargo Load Test Spawn", AreaType.WorkerSpawn, plan.SpawnBounds, 0);
			if (spawnArea == null) throw new InvalidOperationException("스폰 영역 등록 실패.");
			for (int i = 0; i < settings.workers; ++i)
			{
				EditorUtility.DisplayProgressBar("Cargo Load Test", $"작업자 {i + 1}/{settings.workers}", i / (float)settings.workers);
				WorkerArchetype archetype = CreateWorker(i, visual);
				if (!context.WorkerSpawnMgr.TrySpawnWorkerInArea(archetype, spawnArea, out AIWorker worker))
					throw new InvalidOperationException($"작업자 생성 실패: {i}/{settings.workers}");
				workers.Add(worker);
				// Start normally registers next frame; this synchronous editor batch needs that same idempotent initialization now.
				worker.InitializeForSaveLoad();
				if (!context.WorkerMgr.TrySetWorkerAssignment(worker, 0, new[] { WorkerTask.TaskType.CargoTransfer }))
					throw new InvalidOperationException("작업자의 실외 CargoTransfer 배정 실패.");
			}
			session.Configure(sources, destinations, workers, payload, JsonUtility.ToJson(settings));
			Selection.activeGameObject = root;
			return session;
		}
		catch (Exception exception)
		{
			session.FailPreparation(exception.Message);
			throw new InvalidOperationException("생성 중단: " + exception.Message + " 일부가 생성되었을 수 있습니다. Play 종료 후 다시 준비하세요.", exception);
		}
		finally
		{
			if (previousPreset != null) context.BuildingFootprintService.SetActivePreset(previousPreset);
			UnityEngine.Random.state = randomState;
			context.GameTime.ResumePreservedSpeed();
			EditorUtility.ClearProgressBar();
		}
	}

	private static WorkerArchetype CreateWorker(int index, WorkerVisualDefinition visual)
	{
		WorkerArchetype result = new();
		result.WorkerNameDefinition = new WorkerNameDefinition { WorkerFirstName = "LoadTest", WorkerLastName = (index + 1).ToString() };
		result.WorkerVisualDefinition = visual;
		result.AbilityDefinition.SetHumanIdentity(HumanType.FullTime);
		result.AbilityDefinition.abilities = WorkerAbility.CarryBox | WorkerAbility.CargoHandling;
		result.WorkerBaseStat = new WorkerBaseStatDefinition
		{
			baseMoveSpeedMultiplier = 1, minimumMoveSpeedMultiplier = 1,
			baseWorkSpeedMultiplier = 1, minimumWorkSpeedMultiplier = 1, safeHandlingWeightKg = 20,
		};
		return result;
	}

	private static PlaceableDefinition RequireDefinition(GameContext context, string id)
	{
		PlaceableDefinition definition = context.PlaceableCatalog.FindById(id);
		if (definition == null || definition.prefab == null || definition.gridFootprint == null)
			throw new InvalidOperationException("배치 정의 누락: " + id);
		return definition;
	}
	private static FacingDirection Opposite(FacingDirection facing) => facing switch
	{
		FacingDirection.North => FacingDirection.South,
		FacingDirection.East => FacingDirection.West,
		FacingDirection.South => FacingDirection.North,
		_ => FacingDirection.East,
	};
	private static void ValidatePortFootprint(PlaceableDefinition definition, bool outbound)
	{
		GridFootprint footprint = definition.gridFootprint;
		if (footprint.width != 1 || footprint.height != 3 || footprint.Pivot != new Vector2Int(0, 1) ||
			footprint.Get(0, outbound ? 0 : 2).environmentRequirement != FootprintCellEnvironmentRequirement.Indoor ||
			footprint.Get(0, outbound ? 2 : 0).environmentRequirement != FootprintCellEnvironmentRequirement.Outdoor)
			throw new InvalidOperationException("포트 Footprint의 방향/크기가 변경되었습니다: " + definition.name);
	}
	private static BuildingFootprintPreset CreatePreset(CargoLoadTestSettings settings, CargoLoadTestLayout plan)
	{
		BuildingFootprintPreset preset = CreateInstance<BuildingFootprintPreset>();
		preset.name = "CargoLoadTestCross";
		preset.InitializeCircle("cargo_load_test_cross", settings.diameter, "Cargo Load Test Cross");
		SerializedObject serialized = new(preset);
		SerializedProperty cells = serialized.FindProperty("cells");
		for (int i = 0; i < plan.Cells.Length; ++i)
			cells.GetArrayElementAtIndex(i).FindPropertyRelative("type").enumValueIndex = (int)plan.Cells[i].Type;
		serialized.ApplyModifiedPropertiesWithoutUndo();
		return preset;
	}
	private static void RegisterTemporaryPreset(BuildingFootprintService service, BuildingFootprintPreset preset)
	{
		SerializedObject serialized = new(service);
		SerializedProperty presets = serialized.FindProperty("footprintPresets");
		presets.InsertArrayElementAtIndex(presets.arraySize);
		presets.GetArrayElementAtIndex(presets.arraySize - 1).objectReferenceValue = preset;
		serialized.ApplyModifiedPropertiesWithoutUndo();
		if (!service.SetActivePreset(preset)) throw new InvalidOperationException("테스트 Footprint 등록 실패.");
	}
}
