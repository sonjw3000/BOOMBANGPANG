using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UIElements;

namespace UniverseLogistics.UI.Toolkit
{
	public sealed class DebugControlWindow : MonoBehaviour
	{
		private const int ExplosionTabIndex = 0;
		private const int DamageTabIndex = 1;
		private const int WorkerTabIndex = 2;
		private const string ExplosionTabName = "debug-explosion-tab";
		private const string DamageTabName = "debug-damage-tab";
		private const string WorkerTabName = "debug-worker-tab";

		private static readonly string[] TabNames =
		{
			ExplosionTabName,
			DamageTabName,
			WorkerTabName,
		};

		private UIWindow window;
		private VisualTreeAsset contentTemplate;
		private IntegerField explosionRadiusField;
		private IntegerField explosionSeverityField;
		private FloatField damageAmountField;
		private Label explosionMessage;
		private Label damageMessage;
		private Label workerMessage;
		private InteractionContext interaction;
		private bool initialized;
		private bool started;

		public void Configure(UIWindow targetWindow, VisualTreeAsset targetContentTemplate)
		{
			window = targetWindow;
			contentTemplate = targetContentTemplate;
		}

		private void OnEnable()
		{
			InitializeView();
			if (started)
				BindServices();
		}

		private void Start()
		{
			started = true;
			BindServices();
		}

		private void Update()
		{
#if UNITY_EDITOR || DEVELOPMENT_BUILD
			if (Input.GetKeyDown(KeyCode.F1))
				Toggle();
#endif
		}

		private void OnDisable()
		{
			UnbindServices();
		}

		private bool InitializeView()
		{
			if (initialized)
				return true;

			if (window == null || contentTemplate == null || window.Initialize() == false)
			{
				Debug.LogError("[DebugControl] Window or content template is missing.", this);
				return false;
			}

			TemplateContainer explosionContent = CreateTabContent(ExplosionTabName);
			TemplateContainer damageContent = CreateTabContent(DamageTabName);
			TemplateContainer workerContent = CreateTabContent(WorkerTabName);
			if (explosionContent == null || damageContent == null || workerContent == null)
			{
				Debug.LogError("[DebugControl] Required tab roots are missing.", this);
				return false;
			}

			explosionRadiusField = explosionContent.Q<IntegerField>("debug-explosion-radius");
			explosionSeverityField = explosionContent.Q<IntegerField>("debug-explosion-severity");
			damageAmountField = damageContent.Q<FloatField>("debug-damage-amount");
			explosionMessage = explosionContent.Q<Label>("debug-explosion-message");
			damageMessage = damageContent.Q<Label>("debug-damage-message");
			workerMessage = workerContent.Q<Label>("debug-worker-message");
			if (explosionRadiusField == null || explosionSeverityField == null || damageAmountField == null ||
				explosionMessage == null || damageMessage == null || workerMessage == null)
			{
				Debug.LogError("[DebugControl] Required controls are missing.", this);
				return false;
			}

			explosionRadiusField.RegisterValueChangedCallback(evt =>
				explosionRadiusField.SetValueWithoutNotify(Mathf.Max(0, evt.newValue)));
			explosionSeverityField.RegisterValueChangedCallback(evt =>
				explosionSeverityField.SetValueWithoutNotify(Mathf.Clamp(evt.newValue, 1, 100)));
			damageAmountField.RegisterValueChangedCallback(evt =>
			{
				float value = float.IsNaN(evt.newValue) || float.IsInfinity(evt.newValue)
					? 1.0f
					: Mathf.Max(0.0f, evt.newValue);
				damageAmountField.SetValueWithoutNotify(value);
			});

			window.SetTitle("Debug Controls");
			window.ClearTabs();
			window.AddTab("Explosion", explosionContent);
			window.AddTab("Damage", damageContent);
			window.AddTab("Worker", workerContent);
			window.SelectTab(ExplosionTabIndex);
			initialized = true;
			return true;
		}

		private TemplateContainer CreateTabContent(string selectedTabName)
		{
			TemplateContainer content = contentTemplate.CloneTree();
			if (content.Q<VisualElement>(selectedTabName) == null)
				return null;

			for (int i = 0; i < TabNames.Length; ++i)
			{
				if (TabNames[i] == selectedTabName)
					continue;

				content.Q<VisualElement>(TabNames[i])?.RemoveFromHierarchy();
			}

			return content;
		}

		private void Toggle()
		{
			if (InitializeView() == false)
				return;

			if (window.IsOpen)
			{
				window.Close();
				return;
			}

			BindServices();
			window.Open();
		}

		private void BindServices()
		{
			UnbindServices();
			if (GameContext.HasInstance == false)
				return;

			interaction = GameContext.Instance.InteractionCtx;
			if (interaction != null)
				interaction.OnHandlePriorityLeftClick += HandleWorldClick;
		}

		private void UnbindServices()
		{
			if (interaction != null)
				interaction.OnHandlePriorityLeftClick -= HandleWorldClick;

			interaction = null;
		}

		private bool HandleWorldClick(int3 position)
		{
			if (window == null || window.IsOpen == false)
				return false;

			switch (window.SelectedTabIndex)
			{
				case ExplosionTabIndex:
					TriggerExplosion(in position);
					break;

				case DamageTabIndex:
					ApplyDamage(in position);
					break;

				case WorkerTabIndex:
					KnockoutWorker(in position);
					break;
			}

			return true;
		}

		private void TriggerExplosion(in int3 position)
		{
			int radius = Mathf.Max(0, explosionRadiusField.value);
			int severity = Mathf.Clamp(explosionSeverityField.value, 1, 100);
			ExplosionService explosion = GameContext.HasInstance ? GameContext.Instance.ExplosionSvc : null;
			if (explosion == null || explosion.TryEnqueueDebugExplosion(in position, radius, severity) == false)
			{
				Report(explosionMessage,
					$"Explosion request failed at {FormatPosition(in position)}.",
					LogType.Warning);
				return;
			}

			Report(explosionMessage,
				$"Explosion queued at {FormatPosition(in position)}. Radius {radius}, severity {severity}.");
		}

		private void ApplyDamage(in int3 position)
		{
			if (TryResolveTarget(in position, out GameObject target) == false)
			{
				Report(damageMessage, $"No GridPlaceable at {FormatPosition(in position)}.", LogType.Warning);
				return;
			}

			if (target.TryGetComponent<IHealth>(out var health) == false)
			{
				Report(damageMessage, $"{target.name} does not support damage.", LogType.Warning);
				return;
			}

			float amount = damageAmountField.value;
			if (float.IsNaN(amount) || float.IsInfinity(amount) || amount <= 0.0f)
			{
				Report(damageMessage, "Damage must be greater than zero.", LogType.Warning);
				return;
			}

			string targetName = target.name;
			float previousHealth = health.Health;
			float applied = health.ApplyDamage(amount);
			float currentHealth = health.Health;
			bool destroyed = false;
			if (currentHealth <= 0.0f && target.TryGetComponent<IFacility>(out var facility))
			{
				DestroyContext destroyContext = new(DestroyContext.Destroycause.Damage);
				destroyed = GameContext.Instance.FacilityMgr?.DestroyFacility(facility, in destroyContext) == true;
			}

			string suffix = destroyed ? " Destroyed." : string.Empty;
			Report(damageMessage,
				$"{targetName} damaged by {applied:0.##}: {previousHealth:0.##} -> {currentHealth:0.##}.{suffix}");
		}

		private void KnockoutWorker(in int3 position)
		{
			if (TryResolveTarget(in position, out GameObject target) == false ||
				target.TryGetComponent<AIWorker>(out var worker) == false)
			{
				Report(workerMessage, $"No worker at {FormatPosition(in position)}.", LogType.Warning);
				return;
			}

			string workerName = worker.Name;
			string taskName = worker.CurrentTask != null ? worker.CurrentTask.GetType().Name : "None";
			WorkerOperationalState previousState = worker.OperationalState;
			if (worker.EnterIncapacitatedState(WorkerOperationalState.Knockout) == false)
			{
				Report(workerMessage,
					$"{workerName} knockout rejected. Current state: {worker.OperationalState}.",
					LogType.Warning);
				return;
			}

			Report(workerMessage,
				$"{workerName}: {previousState} -> Knockout. Returned task: {taskName}.");
		}

		private static bool TryResolveTarget(in int3 position, out GameObject target)
		{
			target = null;
			if (GameContext.HasInstance == false)
				return false;

			GridService gridService = GameContext.Instance.GridService;
			GridCell cell = gridService?.GetCell(position);
			if (cell == null)
				return false;

			target = cell.ObjectOnGrid != null
				? cell.ObjectOnGrid
				: cell.OccupancyObjectOnGrid;
			return target != null && target.TryGetComponent<IGridPlaceable>(out _);
		}

		private static string FormatPosition(in int3 position)
		{
			return $"({position.x},{position.y},{position.z})";
		}

		private static void Report(Label messageLabel, string message, LogType logType = LogType.Log)
		{
			if (messageLabel != null)
				messageLabel.text = message;

			switch (logType)
			{
				case LogType.Warning:
					Debug.LogWarning($"[DebugControl] {message}");
					break;

				case LogType.Error:
					Debug.LogError($"[DebugControl] {message}");
					break;

				default:
					Debug.Log($"[DebugControl] {message}");
					break;
			}
		}
	}
}
