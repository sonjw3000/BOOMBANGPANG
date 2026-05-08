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
	private Dictionary<WorkerStatusAction, Sprite> spriteMap = new();

	private void Awake()
	{
		worker = GetComponentInParent<AIWorker>();
		foreach (var ss in statusSprites)
		{
			spriteMap[ss.action] = ss.sprite;
		}

		if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
	}

	private void Start()
	{
		if (worker != null)
		{
			worker.OnActionChanged += HandleActionChanged;
			HandleActionChanged(worker.WorkerState.Action);
		}

		if (GameContext.Instance != null && GameContext.Instance.InteractionCtx != null)
		{
			GameContext.Instance.InteractionCtx.OnItemSelected += HandleSelectionChanged;
		}

		UpdateVisibility();
	}

	private void OnDestroy()
	{
		if (worker != null) worker.OnActionChanged -= HandleActionChanged;
		if (GameContext.Instance != null && GameContext.Instance.InteractionCtx != null)
		{
			GameContext.Instance.InteractionCtx.OnItemSelected -= HandleSelectionChanged;
		}
	}

	private void HandleActionChanged(WorkerStatusAction action)
	{
		if (spriteMap.TryGetValue(action, out var sprite))
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
        
	    GameObject selected = GameContext.Instance.InteractionCtx.SelectedObject;
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
			   action == WorkerStatusAction.WaitingForTargetBuilding ||
			   action == WorkerStatusAction.HandlingMistake ||
			   action == WorkerStatusAction.Collapse;
	}

	private void LateUpdate()
	{
		if (worker != null)
		{
			// Update position to be above worker
			transform.position = worker.transform.position + Vector3.up * heightOffset;

			// Billboarding
			if (Camera.main != null)
			{
				transform.rotation = Camera.main.transform.rotation;
			}
		}
	}
}
