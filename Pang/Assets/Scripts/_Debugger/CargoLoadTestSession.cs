#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Unity.Profiling;
using UnityEngine;

// Play Mode only. Does not create tasks; real docking events enter the existing workflow.
public sealed class CargoLoadTestSession : MonoBehaviour
{
	private readonly struct Arrival
	{
		public readonly InboundCargoPort Port;
		public readonly CargoCapsule Capsule;
		public readonly int Frame;
		public Arrival(InboundCargoPort port, CargoCapsule capsule)
		{
			Port = port; Capsule = capsule; Frame = Time.frameCount;
		}
	}
	private readonly struct Supply
	{
		public readonly OutboundCargoPort Port;
		public readonly double At;
		public Supply(OutboundCargoPort port, double at) { Port = port; At = at; }
	}
	private static readonly ProfilerMarker feedMarker = new("CargoLoadTest.FeedAndCollect");
	private static readonly ProfilerMarker sampleMarker = new("CargoLoadTest.Sample");
	private readonly List<OutboundCargoPort> sources = new();
	private readonly List<InboundCargoPort> destinations = new();
	private readonly List<AIWorker> workers = new();
	private readonly Queue<Supply> supply = new();
	private readonly HashSet<OutboundCargoPort> queuedSources = new();
	private readonly Queue<Arrival> arrivals = new();
	private readonly Dictionary<CargoCapsule, OutboundCargoPort> ownedCapsules = new();
	private readonly List<string> rows = new();
	private readonly List<float> frameTimes = new();
	private readonly List<float> measurementFrames = new(16384);
	private ProfilerRecorder mainThread;
	private ProfilerRecorder gcAlloc;
	private GameContext context;
	private ItemDefinition item;
	private float interval;
	private int budget;
	private double measureAt, finishAt, nextSample, lastSample;
	private double cpuSum, feedMilliseconds;
	private long cpuSamples, gcSamples, gcBytes, supplied, picked, delivered, collected;
	private long previousSupplied, previousPicked, previousDelivered, previousCollected;
	private bool subscribed, measuring;
	private string metadata;
	private string runMetadata;
	private bool recoverFatigue;
	private double nextRecovery;
	public bool IsSupplying { get; private set; }
	public bool IsConfigured { get; private set; }
	public string State { get; private set; } = "준비 중";
	public string LastSample { get; private set; } = "";
	public int SourceCount => sources.Count;
	public int DestinationCount => destinations.Count;
	public int WorkerCount => workers.Count;
	public long Supplied => supplied;
	public long Delivered => delivered;
	public long Collected => collected;
	public int InFlight => ownedCapsules.Count;
	public bool IsMeasuring => measuring;

	public void Configure(List<OutboundCargoPort> outbound, List<InboundCargoPort> inbound,
		List<AIWorker> spawned, ItemDefinition payload, string configuration)
	{
		context = GameContext.Instance;
		sources.AddRange(outbound);
		destinations.AddRange(inbound);
		workers.AddRange(spawned);
		item = payload;
		metadata = configuration;
		foreach (OutboundCargoPort port in sources) port.OnCapsuleUndocked += OnSourceUndocked;
		foreach (InboundCargoPort port in destinations) port.OnCapsuleDocked += OnDestinationDocked;
		subscribed = true;
		IsConfigured = true;
		State = "준비 완료";
	}

	public void FailPreparation(string reason)
	{
		State = "준비 실패: " + reason;
		enabled = false;
	}

	public void StartSupply(float supplyInterval, int operationsPerFrame, float warmupSeconds, float durationSeconds, bool recoverTestWorkerFatigue = true)
	{
		if (!IsConfigured || IsSupplying || item == null || ownedCapsules.Count != 0)
			throw new InvalidOperationException("준비 완료 및 이전 운반의 회수 완료 후 시작할 수 있습니다.");
		if (Time.timeScale <= 0) throw new InvalidOperationException("게임 일시정지를 해제하세요.");
		interval = Mathf.Max(0, supplyInterval);
		budget = Mathf.Max(1, operationsPerFrame);
		recoverFatigue = recoverTestWorkerFatigue;
		nextRecovery = 0;
		runMetadata = $"supply_interval={N(interval)}; operations_per_frame={budget}; warmup_s={N(warmupSeconds)}; duration_s={N(durationSeconds)}; recover_test_fatigue={recoverFatigue}; item_id={item.ItemID}";
		supplied = picked = delivered = collected = 0;
		rows.Clear();
		frameTimes.Clear();
		measurementFrames.Clear();
		measureAt = Time.realtimeSinceStartupAsDouble + Mathf.Max(0, warmupSeconds);
		finishAt = measureAt + Mathf.Max(1, durationSeconds);
		measuring = false;
		IsSupplying = true;
		State = "워밍업";
		foreach (OutboundCargoPort port in sources) QueueSupply(port, 0);
	}

	public void StopSupply()
	{
		IsSupplying = false;
		supply.Clear();
		queuedSources.Clear();
		if (measuring) CaptureSample(Time.realtimeSinceStartupAsDouble);
		measuring = false;
		DisposeRecorders();
		State = ownedCapsules.Count == 0 ? "중지됨" : "공급 중지 · 남은 운반 회수 중";
	}

	private void OnSourceUndocked(CapsuleDock dock)
	{
		++picked;
	}

	private void OnDestinationDocked(CapsuleDock dock)
	{
		if (dock is not InboundCargoPort port || port.DockedCapsule == null || !ownedCapsules.ContainsKey(port.DockedCapsule)) return;
		++delivered;
		arrivals.Enqueue(new Arrival(port, port.DockedCapsule));
	}

	private void QueueSupply(OutboundCargoPort port, double at)
	{
		if (port != null && queuedSources.Add(port)) supply.Enqueue(new Supply(port, at));
	}

	private void Update()
	{
		if (!IsConfigured || context == null || Time.timeScale <= 0) return;
		double now = Time.realtimeSinceStartupAsDouble;
		try
		{
			if (IsSupplying && now >= finishAt)
				StopSupply();
			else if (IsSupplying && !measuring && now >= measureAt)
				BeginMeasurement(now);
			long start = System.Diagnostics.Stopwatch.GetTimestamp();
			using (feedMarker.Auto())
			{
				if (recoverFatigue && (IsSupplying || ownedCapsules.Count > 0) && now >= nextRecovery)
				{
					nextRecovery = now + 1;
					foreach (AIWorker worker in workers)
						if (worker is HumanWorker human && human.IsOperational) human.TickRecovery(100, 1);
				}
				CollectArrivals();
				if (IsSupplying) FeedSources(now);
			}
			if (measuring)
			{
				feedMilliseconds += (System.Diagnostics.Stopwatch.GetTimestamp() - start) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
				frameTimes.Add(Time.unscaledDeltaTime * 1000f);
				measurementFrames.Add(Time.unscaledDeltaTime * 1000f);
				if (mainThread.Valid && mainThread.Count > 0) { cpuSum += mainThread.LastValue / 1000000.0; ++cpuSamples; }
				if (gcAlloc.Valid && gcAlloc.Count > 0) { gcBytes += gcAlloc.LastValue; ++gcSamples; }
				if (now >= nextSample) CaptureSample(now);
			}
			if (!IsSupplying && ownedCapsules.Count == 0) State = "중지됨 · 회수 완료";
		}
		catch (Exception exception)
		{
			StopSupply();
			State = "오류: " + exception.Message;
			Debug.LogException(exception, this);
			enabled = false;
		}
	}

	private void FeedSources(double now)
	{
		int count = Mathf.Min(budget, supply.Count);
		for (int i = 0; i < count; ++i)
		{
			Supply next = supply.Dequeue();
			queuedSources.Remove(next.Port);
			if (next.Port == null) throw new InvalidOperationException("테스트 포트가 제거되었습니다. Play를 종료해 초기화하세요.");
			if (next.At > now)
			{
				QueueSupply(next.Port, next.At);
				continue;
			}
			if (next.Port.HasCapsule || context.CapsuleRelocateCoordinator.IsReserved(next.Port))
			{
				QueueSupply(next.Port, now + interval);
				continue;
			}
			if (!context.BoxMgr.GetNewBox(BoxType.Capsule, out BoxBase box))
				throw new InvalidOperationException("캡슐 풀에서 캡슐 생성에 실패했습니다.");
			if (box is not CargoCapsule capsule)
			{
				context.BoxMgr.DisableBox(box);
				throw new InvalidOperationException("Capsule 풀에 잘못된 프리팹이 설정되어 있습니다.");
			}
			if (capsule.AddItem(item.ItemID, 1) != 1)
			{
				context.BoxMgr.DisableBox(capsule);
				throw new InvalidOperationException("선택한 아이템 한 개가 캡슐에 들어가지 않습니다.");
			}
			capsule.SetLogisticsState(CapsuleLogisticsState.OB);
			ownedCapsules.Add(capsule, next.Port);
			if (!next.Port.TryDockCapsule(capsule))
			{
				ownedCapsules.Remove(capsule);
				context.BoxMgr.DisableBox(capsule);
				throw new InvalidOperationException("OB 캡슐 도킹에 실패했습니다.");
			}
			++supplied;
		}
	}

	private void CollectArrivals()
	{
		int count = Mathf.Min(Mathf.Max(1, budget), arrivals.Count);
		for (int i = 0; i < count; ++i)
		{
			Arrival arrival = arrivals.Dequeue();
			if (arrival.Port == null || arrival.Capsule == null || arrival.Port.DockedCapsule != arrival.Capsule)
				throw new InvalidOperationException("도착 캡슐이 다른 시스템에 의해 변경되었습니다. 테스트를 다시 준비하세요.");
			// Dock event happens inside Put, before task completion. Never recycle from the event callback.
			if (Time.frameCount <= arrival.Frame || context.CapsuleRelocateCoordinator.IsReserved(arrival.Port))
			{
				arrivals.Enqueue(arrival);
				continue;
			}
			if (!arrival.Port.TryUndockCapsule(out CargoCapsule capsule)) continue;
			OutboundCargoPort source = ownedCapsules[capsule];
			ownedCapsules.Remove(capsule);
			if (!context.BoxMgr.DisableBox(capsule)) throw new InvalidOperationException("캡슐 풀 반환 실패.");
			++collected;
			// One capsule per source until arrival/ownership release; old task completion cannot clear a newer source claim.
			if (IsSupplying) QueueSupply(source, Time.realtimeSinceStartupAsDouble + interval);
		}
	}

	private void BeginMeasurement(double now)
	{
		DisposeRecorders();
		mainThread = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread", 1);
		gcAlloc = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame", 1);
		previousSupplied = supplied; previousPicked = picked; previousDelivered = delivered; previousCollected = collected;
		lastSample = now; nextSample = now + 1;
		cpuSum = feedMilliseconds = 0; cpuSamples = gcSamples = gcBytes = 0;
		frameTimes.Clear();
		measuring = true;
		State = "측정 중";
	}

	private void CaptureSample(double now)
	{
		using (sampleMarker.Auto())
		{
			double elapsed = now - lastSample;
			if (elapsed <= 0 || frameTimes.Count == 0) return;
			int active = 0, blocked = 0, operational = 0, needsRecovery = 0;
			foreach (AIWorker worker in workers)
			{
				if (worker == null) continue;
				if (worker.IsOperational) ++operational;
				if (worker.CurrentTask != null) ++active;
				if (worker.IsTrafficBlocked) ++blocked;
				if (worker.NeedsRecovery()) ++needsRecovery;
			}
			frameTimes.Sort();
			float p95 = frameTimes[Mathf.Clamp(Mathf.CeilToInt(frameTimes.Count * 0.95f) - 1, 0, frameTimes.Count - 1)];
			double meanCpu = cpuSamples == 0 ? double.NaN : cpuSum / cpuSamples;
			double rate = (delivered - previousDelivered) / elapsed;
			LastSample = $"활동 {active}/{workers.Count}, 회복 필요 {needsRecovery}, 도착 {rate:F2}/s, 프레임 P95 {p95:F2} ms, Main Thread {meanCpu:F2} ms";
			rows.Add(string.Join(",", N(now - measureAt), N(Time.timeScale), workers.Count, operational, active, blocked, needsRecovery,
				N(frameTimes.Count / elapsed), N(p95), N(meanCpu), N(feedMilliseconds / frameTimes.Count), gcSamples == 0 ? "NaN" : gcBytes.ToString(CultureInfo.InvariantCulture),
				supplied - previousSupplied, picked - previousPicked, delivered - previousDelivered, collected - previousCollected,
				N(elapsed), ownedCapsules.Count, supply.Count, arrivals.Count));
			previousSupplied = supplied; previousPicked = picked; previousDelivered = delivered; previousCollected = collected;
			lastSample = now; nextSample = now + 1;
			frameTimes.Clear(); cpuSum = feedMilliseconds = 0; cpuSamples = gcSamples = gcBytes = 0;
		}
	}

	public void ExportCsv(string path)
	{
		StringBuilder text = new();
		text.AppendLine("# Editor Play Mode; Main Thread includes waits/editor overhead; NaN means unavailable.");
		text.AppendLine("# Arrivals count successful IB docking, not task completion; collection waits for reservation release.");
		text.AppendLine("# " + metadata.Replace('\n', ' '));
		text.AppendLine("# " + runMetadata);
		text.AppendLine("# " + SystemInfo.processorType + "; " + SystemInfo.systemMemorySize + " MB; Unity " + Application.unityVersion);
		text.AppendLine("elapsed_s,time_scale,workers,operational,assigned,traffic_blocked,recovery_needed,fps,frame_p95_ms,main_thread_ms,feed_collect_ms_per_frame,gc_bytes,supplied_events,pick_events,arrival_events,collected,window_s,live_capsules,supply_queue,arrival_queue");
		foreach (string row in rows) text.AppendLine(row);
		File.WriteAllText(path, text.ToString(), new UTF8Encoding(true));
	}

	// Raw frame times allow whole-run percentiles; averaging per-second P95 values is not equivalent.
	public void ExportFramesCsv(string path)
	{
		StringBuilder text = new("frame,frame_ms\n");
		for (int i = 0; i < measurementFrames.Count; ++i)
			text.Append(i).Append(',').Append(N(measurementFrames[i])).Append('\n');
		File.WriteAllText(path, text.ToString(), new UTF8Encoding(true));
	}
	private static string N(double number) => number.ToString("F4", CultureInfo.InvariantCulture);
	private void DisposeRecorders() { mainThread.Dispose(); gcAlloc.Dispose(); }
	private void OnDisable()
	{
		IsSupplying = false;
		measuring = false;
		DisposeRecorders();
		if (!subscribed) return;
		foreach (OutboundCargoPort port in sources) if (port != null) port.OnCapsuleUndocked -= OnSourceUndocked;
		foreach (InboundCargoPort port in destinations) if (port != null) port.OnCapsuleDocked -= OnDestinationDocked;
		subscribed = false;
	}
}
#endif
