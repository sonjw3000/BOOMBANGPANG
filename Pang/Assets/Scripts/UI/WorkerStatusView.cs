using UnityEngine;
using System.Collections.Generic;

public class WorkerStatusView : MonoBehaviour
{
	[System.Serializable]
	public struct StatusSprite
	{
		public WorkerStatusAction action;
		public Sprite sprite;
	}

	[SerializeField] private SpriteRenderer spriteRenderer;
	[SerializeField] private List<StatusSprite> statusSprites;
	[SerializeField] private float heightOffset = 1.5f;

	private AIWorker worker;
	private readonly Dictionary<WorkerStatusAction, Sprite> spriteMap = new();
	private InteractionContext boundInteraction;
	private bool workerActionBound;

	private void Awake()
	{
		EnsureRuntimeReferences();
	}

	private void OnEnable()
	{
		EnsureRuntimeReferences();
		BindEvents();
		RefreshStatus();
	}

	private void Start()
	{
		BindEvents();
		RefreshStatus();
	}

	private void OnDisable()
	{
		UnbindEvents();
	}

	private void EnsureRuntimeReferences()
	{
		if (worker == null)
			worker = GetComponentInParent<AIWorker>();

		if (spriteRenderer == null)
			spriteRenderer = GetComponent<SpriteRenderer>();

		spriteMap.Clear();
		if (statusSprites == null)
			return;

		foreach (StatusSprite statusSprite in statusSprites)
			spriteMap[statusSprite.action] = statusSprite.sprite;
	}

	private void BindEvents()
	{
		if (worker != null && workerActionBound == false)
		{
			worker.OnActionChanged += HandleActionChanged;
			workerActionBound = true;
		}

		if (boundInteraction != null || GameContext.HasInstance == false)
			return;

		boundInteraction = GameContext.Instance.InteractionCtx;
		if (boundInteraction != null)
			boundInteraction.OnItemSelected += HandleSelectionChanged;
	}

	private void UnbindEvents()
	{
		if (worker != null && workerActionBound)
			worker.OnActionChanged -= HandleActionChanged;

		workerActionBound = false;
		if (boundInteraction != null)
			boundInteraction.OnItemSelected -= HandleSelectionChanged;

		boundInteraction = null;
	}

	private void RefreshStatus()
	{
		if (worker != null)
			HandleActionChanged(worker.WorkerState.Action);
		else
			UpdateVisibility();
	}

	private void HandleActionChanged(WorkerStatusAction action)
	{
		if (spriteMap.TryGetValue(action, out var sprite))
		{
			spriteRenderer.sprite = sprite;
		}
		else if (action == WorkerStatusAction.BlockedByCasualty &&
			spriteMap.TryGetValue(WorkerStatusAction.TrafficBlock, out sprite))
		{
			spriteRenderer.sprite = sprite;
		}
		else
		{
			spriteRenderer.sprite = null;
		}
		UpdateVisibility();
	}

	private void HandleSelectionChanged(GameObject selected)
	{
		UpdateVisibility();
	}

	private void UpdateVisibility()
	{
		if (worker == null || spriteRenderer == null) return;

		WorkerStatusAction action = worker.WorkerState.Action;
		if (action == WorkerStatusAction.None)
		{
			spriteRenderer.enabled = false;
			return;
		}

		bool isAlwaysVisible = IsAlwaysVisible(action);

		GameObject selected = boundInteraction?.SelectedObject;
		bool isSelected = selected != null && (selected == worker.gameObject || selected.transform.IsChildOf(worker.transform));

		if (isAlwaysVisible)
		{
			spriteRenderer.enabled = true;
		}
		else
		{
			// "선택" icons are shown only when selected
			spriteRenderer.enabled = isSelected;
		}

		// Ensure scale is visible (sometimes sprites can be too small in 3D)
		transform.localScale = Vector3.one * 0.5f; // Adjust based on scene scale if needed
	}

	private bool IsAlwaysVisible(WorkerStatusAction action)
	{
		return action == WorkerStatusAction.WaitingForItems ||
			   action == WorkerStatusAction.TrafficBlock ||
			   action == WorkerStatusAction.BlockedByCasualty ||
			   action == WorkerStatusAction.WaitingForTargetBuilding ||
			   action == WorkerStatusAction.HandlingMistake ||
			   action == WorkerStatusAction.Collapse ||
			   action == WorkerStatusAction.Knockout ||
			   action == WorkerStatusAction.Death ||
			   action == WorkerStatusAction.Malfunction;
	}

	private void LateUpdate()
	{
		if (worker != null)
		{
			Transform statusSlot = worker.StatusSlot;
			transform.position = statusSlot != null
				? statusSlot.position
				: worker.transform.position + Vector3.up * heightOffset;

			// Billboarding
			if (Camera.main != null)
			{
				transform.rotation = Camera.main.transform.rotation;
			}
		}
	}
}
