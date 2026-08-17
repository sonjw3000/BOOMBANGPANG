using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace UniverseLogistics.UI.Toolkit
{
	public sealed partial class WorkforceManagementWindow
	{
		private const string AssignmentRoleDropValidClass = "workforce-assignment-role--drop-valid";
		private const string AssignmentRoleDropInvalidClass = "workforce-assignment-role--drop-invalid";
		private const string AssignmentRoleDropHoverClass = "workforce-assignment-role--drop-hover";
		private const string AssignmentRoleDropHoverInvalidClass = "workforce-assignment-role--drop-hover-invalid";
		private const string UnassignedDropValidClass = "workforce-unassigned-column--drop-valid";
		private const string UnassignedDropInvalidClass = "workforce-unassigned-column--drop-invalid";
		private const string UnassignedDropHoverClass = "workforce-unassigned-column--drop-hover";
		private const string UnassignedDropHoverInvalidClass = "workforce-unassigned-column--drop-hover-invalid";
		private const string AssignmentFeedbackSuccessClass = "workforce-assignment-drag-status--success";
		private const string AssignmentFeedbackPendingClass = "workforce-assignment-drag-status--pending";
		private const string AssignmentFeedbackErrorClass = "workforce-assignment-drag-status--error";
		private const string AssignmentDragInstruction =
			"Drag an unassigned worker to a role, or an assigned worker to Unassigned.";

		private enum AssignmentDragFeedback
		{
			Neutral,
			Success,
			Pending,
			Error,
		}

		private sealed class AssignmentRoleDropTarget
		{
			public VisualElement Row { get; }
			public uint BuildingId { get; }
			public WorkforceRole Role { get; }

			public AssignmentRoleDropTarget(
				VisualElement row,
				uint buildingId,
				WorkforceRole role)
			{
				Row = row;
				BuildingId = buildingId;
				Role = role;
			}
		}

		private VisualElement assignmentUnassignedDropTarget;
		private Label assignmentDragStatus;
		private readonly List<AssignmentRoleDropTarget> assignmentRoleDropTargets = new();
		private AIWorker assignmentDragWorker;
		private VisualElement assignmentDragSourceRow;
		private uint assignmentDragSourceBuildingId;
		private WorkforceRole assignmentDragSourceRole = WorkforceRole.Undefined;
		private Vector2 assignmentDragPointerStart;
		private int assignmentDragPointerId = -1;
		private bool assignmentDragStarted;
		private bool endingAssignmentPointerCapture;
		private AIWorker assignmentPendingFeedbackWorker;
		private uint assignmentPendingFeedbackBuildingId;
		private WorkforceRole assignmentPendingFeedbackRole = WorkforceRole.Undefined;
		private bool hasAssignmentPendingFeedback;

		private bool IsAssignmentDragInteractionActive =>
			assignmentDragPointerId >= 0 || assignmentDragStarted;

		private void InitializeAssignmentDragView(VisualElement content)
		{
			assignmentUnassignedDropTarget =
				content.Q<VisualElement>("workforce-unassigned-drop-target");
			assignmentDragStatus = content.Q<Label>("workforce-assignment-drag-status");
			if (assignmentsTab == null)
				return;

			assignmentsTab.focusable = true;
			assignmentsTab.RegisterCallback<PointerMoveEvent>(OnAssignmentPointerMove);
			assignmentsTab.RegisterCallback<PointerUpEvent>(OnAssignmentPointerUp);
			assignmentsTab.RegisterCallback<PointerCancelEvent>(OnAssignmentPointerCancel);
			assignmentsTab.RegisterCallback<PointerCaptureOutEvent>(OnAssignmentPointerCaptureOut);
			assignmentsTab.RegisterCallback<KeyDownEvent>(OnAssignmentKeyDown);
			SetAssignmentDragFeedback(AssignmentDragInstruction, AssignmentDragFeedback.Neutral);
		}

		private bool HasRequiredAssignmentDragView()
		{
			return assignmentUnassignedDropTarget != null && assignmentDragStatus != null;
		}

		private void UnbindAssignmentDragControls()
		{
			CancelAssignmentDrag();
			if (assignmentsTab == null)
				return;

			assignmentsTab.UnregisterCallback<PointerMoveEvent>(OnAssignmentPointerMove);
			assignmentsTab.UnregisterCallback<PointerUpEvent>(OnAssignmentPointerUp);
			assignmentsTab.UnregisterCallback<PointerCancelEvent>(OnAssignmentPointerCancel);
			assignmentsTab.UnregisterCallback<PointerCaptureOutEvent>(OnAssignmentPointerCaptureOut);
			assignmentsTab.UnregisterCallback<KeyDownEvent>(OnAssignmentKeyDown);
		}

		private void RegisterAssignmentRoleDropTarget(
			VisualElement row,
			uint buildingId,
			WorkforceRole role)
		{
			if (row != null)
				assignmentRoleDropTargets.Add(new AssignmentRoleDropTarget(row, buildingId, role));
		}

		private void ClearAssignmentRoleDropTargets()
		{
			ClearAssignmentDropTargetVisuals();
			assignmentRoleDropTargets.Clear();
		}

		private void OnAssignmentWorkerPointerDown(
			PointerDownEvent evt,
			VisualElement row,
			AIWorker worker,
			uint sourceBuildingId,
			WorkforceRole sourceRole)
		{
			if (evt.button != 0 ||
				worker == null ||
				worker.IsOperational == false ||
				assignmentDragPointerId >= 0 ||
				assignmentDragStarted ||
				assignmentsTab == null)
			{
				return;
			}

			assignmentDragWorker = worker;
			assignmentDragSourceRow = row;
			assignmentDragSourceBuildingId = sourceBuildingId;
			assignmentDragSourceRole = sourceRole;
			assignmentDragPointerStart = evt.position;
			assignmentDragPointerId = evt.pointerId;
			assignmentsTab.Focus();
			assignmentsTab.CapturePointer(assignmentDragPointerId);
			evt.StopPropagation();
		}

		private void OnAssignmentPointerMove(PointerMoveEvent evt)
		{
			if (evt.pointerId != assignmentDragPointerId || assignmentDragWorker == null)
				return;

			if (assignmentDragStarted == false)
			{
				Vector2 current = evt.position;
				if ((current - assignmentDragPointerStart).sqrMagnitude <
					DragThreshold * DragThreshold)
				{
					return;
				}

				if (BeginAssignmentDrag(
						assignmentDragWorker,
						assignmentDragSourceBuildingId,
						assignmentDragSourceRole) == false)
				{
					SetAssignmentDragFeedback(
						"This worker is no longer available for reassignment.",
						AssignmentDragFeedback.Error);
					CancelAssignmentDrag();
					return;
				}
			}

			VisualElement hit = assignmentsTab.panel?.Pick(evt.position);
			RefreshAssignmentDropTargetVisuals(hit);
			evt.StopPropagation();
		}

		private void OnAssignmentPointerUp(PointerUpEvent evt)
		{
			if (evt.pointerId != assignmentDragPointerId)
				return;

			VisualElement hit = assignmentsTab.panel?.Pick(evt.position);
			AssignmentRoleDropTarget roleTarget = FindAssignmentRoleDropTarget(hit);
			bool overUnassigned = IsInsideAssignmentUnassignedTarget(hit);
			bool wasDragging = assignmentDragStarted;
			bool roleSource = assignmentDragSourceRole != WorkforceRole.Undefined;
			ReleaseAssignmentPointerCapture();

			bool accepted = false;
			if (wasDragging && roleTarget != null)
			{
				accepted = TryDropAssignmentOnRole(roleTarget.BuildingId, roleTarget.Role);
				if (accepted == false)
				{
					string message = roleSource
						? "Direct role-to-role reassignment is available in the next step. Drop on Unassigned first."
						: $"{GetAssignmentRoleDisplayName(roleTarget.Role)} cannot accept this worker.";
					SetAssignmentDragFeedback(message, AssignmentDragFeedback.Error);
				}
			}
			else if (wasDragging && overUnassigned)
			{
				accepted = TryDropAssignmentOnUnassigned();
				if (accepted == false)
				{
					SetAssignmentDragFeedback(
						roleSource
							? "This worker cannot be unassigned right now."
							: "This worker is already unassigned.",
						AssignmentDragFeedback.Error);
				}
			}
			else if (wasDragging)
			{
				SetAssignmentDragFeedback(
					"Drop canceled because there was no valid target.",
					AssignmentDragFeedback.Error);
			}

			ClearAssignmentDragState();
			if (accepted)
			{
				RefreshAssignments();
			}
			else
			{
				FlushAssignmentsRefreshAfterDrag();
			}

			evt.StopPropagation();
		}

		private void OnAssignmentPointerCancel(PointerCancelEvent evt)
		{
			if (evt.pointerId != assignmentDragPointerId)
				return;

			CancelAssignmentDrag();
			evt.StopPropagation();
		}

		private void OnAssignmentPointerCaptureOut(PointerCaptureOutEvent evt)
		{
			if (endingAssignmentPointerCapture || evt.pointerId != assignmentDragPointerId)
				return;

			CancelAssignmentDrag();
		}

		private void OnAssignmentKeyDown(KeyDownEvent evt)
		{
			if (evt.keyCode != KeyCode.Escape || IsAssignmentDragInteractionActive == false)
				return;

			CancelAssignmentDrag();
			evt.StopPropagation();
		}

		private bool BeginAssignmentDrag(
			AIWorker worker,
			uint sourceBuildingId,
			WorkforceRole sourceRole)
		{
			if (workerManager == null ||
				IsAssignmentDragSourceCurrent(worker, sourceBuildingId, sourceRole) == false)
				return false;

			bool unassignedSource = sourceRole == WorkforceRole.Undefined;
			if (unassignedSource)
			{
				if (workerManager.CanRequestWorkerUnassignment(worker) == false)
					return false;
			}
			else if (workerManager.CanRequestWorkerUnassignment(worker) == false)
			{
				return false;
			}

			assignmentModeController?.EndMode();
			ClearPendingAssignmentFeedbackTracking();
			assignmentDragWorker = worker;
			assignmentDragSourceBuildingId = sourceBuildingId;
			assignmentDragSourceRole = sourceRole;
			assignmentDragStarted = true;
			assignmentDragSourceRow?.EnableInClassList(DraggingRowClass, true);
			RefreshAssignmentDropTargetVisuals();
			SetAssignmentDragFeedback(
				unassignedSource
					? $"Drop Worker #{worker.WorkerID} on a highlighted role."
					: $"Drop Worker #{worker.WorkerID} on Unassigned to remove the current assignment.",
				AssignmentDragFeedback.Neutral);
			return true;
		}

		private bool CanDropAssignmentOnRole(uint buildingId, WorkforceRole role)
		{
			return assignmentDragStarted &&
				assignmentDragWorker != null &&
				assignmentDragSourceRole == WorkforceRole.Undefined &&
				IsAssignmentDragSourceCurrent(
					assignmentDragWorker,
					assignmentDragSourceBuildingId,
					assignmentDragSourceRole) &&
				workerManager?.CanRequestWorkerRoleAssignment(
					assignmentDragWorker,
					buildingId,
					role) == true;
		}

		private bool TryDropAssignmentOnRole(uint buildingId, WorkforceRole role)
		{
			if (CanDropAssignmentOnRole(buildingId, role) == false)
				return false;

			AIWorker worker = assignmentDragWorker;
			if (workerManager.TryRequestWorkerRoleAssignment(worker, buildingId, role) == false)
				return false;

			SetAssignmentDropAcceptedFeedback(
				worker,
				buildingId,
				role,
				worker.HasPendingAssignment);
			RequestAssignmentsRefresh();
			return true;
		}

		private bool CanDropAssignmentOnUnassigned()
		{
			return assignmentDragStarted &&
				assignmentDragWorker != null &&
				assignmentDragSourceRole != WorkforceRole.Undefined &&
				IsAssignmentDragSourceCurrent(
					assignmentDragWorker,
					assignmentDragSourceBuildingId,
					assignmentDragSourceRole) &&
				workerManager?.CanRequestWorkerUnassignment(assignmentDragWorker) == true;
		}

		private bool TryDropAssignmentOnUnassigned()
		{
			if (CanDropAssignmentOnUnassigned() == false)
				return false;

			AIWorker worker = assignmentDragWorker;
			if (workerManager.TryRequestWorkerUnassignment(worker) == false)
				return false;

			SetAssignmentDropAcceptedFeedback(
				worker,
				0,
				WorkforceRole.Undefined,
				worker.HasPendingAssignment);
			RequestAssignmentsRefresh();
			return true;
		}

		private void CancelAssignmentDrag()
		{
			bool showCanceled = assignmentDragStarted;
			ReleaseAssignmentPointerCapture();
			ClearAssignmentDragState();
			if (showCanceled)
			{
				SetAssignmentDragFeedback(
					"Assignment drag canceled.",
					AssignmentDragFeedback.Neutral);
			}

			FlushAssignmentsRefreshAfterDrag();
		}

		private void ReleaseAssignmentPointerCapture()
		{
			if (assignmentDragPointerId < 0)
				return;

			int pointerId = assignmentDragPointerId;
			assignmentDragPointerId = -1;
			endingAssignmentPointerCapture = true;
			if (assignmentsTab != null && assignmentsTab.HasPointerCapture(pointerId))
				assignmentsTab.ReleasePointer(pointerId);
			endingAssignmentPointerCapture = false;
		}

		private void ClearAssignmentDragState()
		{
			assignmentDragSourceRow?.EnableInClassList(DraggingRowClass, false);
			ClearAssignmentDropTargetVisuals();
			assignmentDragWorker = null;
			assignmentDragSourceRow = null;
			assignmentDragSourceBuildingId = 0;
			assignmentDragSourceRole = WorkforceRole.Undefined;
			assignmentDragPointerStart = default;
			assignmentDragStarted = false;
		}

		private void RefreshAssignmentDropTargetVisuals(VisualElement hit = null)
		{
			for (int i = 0; i < assignmentRoleDropTargets.Count; ++i)
			{
				AssignmentRoleDropTarget target = assignmentRoleDropTargets[i];
				bool valid = CanDropAssignmentOnRole(target.BuildingId, target.Role);
				bool hovered = IsInsideElement(hit, target.Row);
				target.Row.EnableInClassList(AssignmentRoleDropValidClass, valid);
				target.Row.EnableInClassList(AssignmentRoleDropInvalidClass, valid == false);
				target.Row.EnableInClassList(AssignmentRoleDropHoverClass, hovered && valid);
				target.Row.EnableInClassList(
					AssignmentRoleDropHoverInvalidClass,
					hovered && valid == false);
			}

			bool unassignedValid = CanDropAssignmentOnUnassigned();
			bool unassignedHovered = IsInsideAssignmentUnassignedTarget(hit);
			assignmentUnassignedDropTarget?.EnableInClassList(
				UnassignedDropValidClass,
				unassignedValid);
			assignmentUnassignedDropTarget?.EnableInClassList(
				UnassignedDropInvalidClass,
				unassignedValid == false);
			assignmentUnassignedDropTarget?.EnableInClassList(
				UnassignedDropHoverClass,
				unassignedHovered && unassignedValid);
			assignmentUnassignedDropTarget?.EnableInClassList(
				UnassignedDropHoverInvalidClass,
				unassignedHovered && unassignedValid == false);

			UpdateAssignmentDragHoverFeedback(hit);
		}

		private void ClearAssignmentDropTargetVisuals()
		{
			for (int i = 0; i < assignmentRoleDropTargets.Count; ++i)
			{
				VisualElement row = assignmentRoleDropTargets[i].Row;
				row?.RemoveFromClassList(AssignmentRoleDropValidClass);
				row?.RemoveFromClassList(AssignmentRoleDropInvalidClass);
				row?.RemoveFromClassList(AssignmentRoleDropHoverClass);
				row?.RemoveFromClassList(AssignmentRoleDropHoverInvalidClass);
			}

			assignmentUnassignedDropTarget?.RemoveFromClassList(UnassignedDropValidClass);
			assignmentUnassignedDropTarget?.RemoveFromClassList(UnassignedDropInvalidClass);
			assignmentUnassignedDropTarget?.RemoveFromClassList(UnassignedDropHoverClass);
			assignmentUnassignedDropTarget?.RemoveFromClassList(UnassignedDropHoverInvalidClass);
		}

		private void UpdateAssignmentDragHoverFeedback(VisualElement hit)
		{
			if (assignmentDragStarted == false || assignmentDragWorker == null)
				return;

			AssignmentRoleDropTarget roleTarget = FindAssignmentRoleDropTarget(hit);
			if (roleTarget != null)
			{
				if (CanDropAssignmentOnRole(roleTarget.BuildingId, roleTarget.Role))
				{
					SetAssignmentDragFeedback(
						$"Assign Worker #{assignmentDragWorker.WorkerID} to {GetAssignmentRoleDisplayName(roleTarget.Role)}.",
						AssignmentDragFeedback.Neutral);
				}
				else
				{
					SetAssignmentDragFeedback(
						assignmentDragSourceRole != WorkforceRole.Undefined
							? "Direct role-to-role reassignment is available in the next step."
							: $"Worker #{assignmentDragWorker.WorkerID} is not eligible for {GetAssignmentRoleDisplayName(roleTarget.Role)}.",
						AssignmentDragFeedback.Error);
				}
				return;
			}

			if (IsInsideAssignmentUnassignedTarget(hit))
			{
				SetAssignmentDragFeedback(
					CanDropAssignmentOnUnassigned()
						? $"Remove the current assignment from Worker #{assignmentDragWorker.WorkerID}."
						: $"Worker #{assignmentDragWorker.WorkerID} is already unassigned.",
					CanDropAssignmentOnUnassigned()
						? AssignmentDragFeedback.Neutral
						: AssignmentDragFeedback.Error);
				return;
			}

			SetAssignmentDragFeedback(
				assignmentDragSourceRole == WorkforceRole.Undefined
					? $"Drop Worker #{assignmentDragWorker.WorkerID} on a highlighted role."
					: $"Drop Worker #{assignmentDragWorker.WorkerID} on Unassigned.",
				AssignmentDragFeedback.Neutral);
		}

		private AssignmentRoleDropTarget FindAssignmentRoleDropTarget(VisualElement hit)
		{
			for (int i = 0; i < assignmentRoleDropTargets.Count; ++i)
			{
				AssignmentRoleDropTarget target = assignmentRoleDropTargets[i];
				if (IsInsideElement(hit, target.Row))
					return target;
			}

			return null;
		}

		private bool IsInsideAssignmentUnassignedTarget(VisualElement hit)
		{
			return IsInsideElement(hit, assignmentUnassignedDropTarget);
		}

		private static bool IsInsideElement(VisualElement hit, VisualElement target)
		{
			return hit != null && target != null && (hit == target || target.Contains(hit));
		}

		private static bool IsAssignmentDragSourceCurrent(
			AIWorker worker,
			uint sourceBuildingId,
			WorkforceRole sourceRole)
		{
			if (worker == null || worker.IsOperational == false)
				return false;
			if (sourceRole == WorkforceRole.Undefined)
				return worker.AssignedTaskTypes == null || worker.AssignedTaskTypes.Count == 0;

			return worker.PrimaryBuildingId == sourceBuildingId &&
				WorkforceRoleCatalog.GetAssignmentState(sourceRole, worker.AssignedTaskTypes) !=
					WorkforceRoleAssignmentState.None;
		}

		private void SetAssignmentDropAcceptedFeedback(
			AIWorker worker,
			uint buildingId,
			WorkforceRole role,
			bool pending)
		{
			if (worker == null)
				return;

			if (pending)
			{
				assignmentPendingFeedbackWorker = worker;
				assignmentPendingFeedbackBuildingId = buildingId;
				assignmentPendingFeedbackRole = role;
				hasAssignmentPendingFeedback = true;
			}
			else
			{
				ClearPendingAssignmentFeedbackTracking();
			}

			bool unassigned = role == WorkforceRole.Undefined;
			string action = unassigned
				? "be unassigned"
				: $"move to {GetAssignmentRoleDisplayName(role)}";
			SetAssignmentDragFeedback(
				pending
					? $"Worker #{worker.WorkerID} will {action} after the current task."
					: unassigned
						? $"Worker #{worker.WorkerID} is now unassigned."
						: $"Worker #{worker.WorkerID} assigned to {GetAssignmentRoleDisplayName(role)}.",
				pending ? AssignmentDragFeedback.Pending : AssignmentDragFeedback.Success);
		}

		private void RefreshPendingAssignmentFeedback()
		{
			if (hasAssignmentPendingFeedback == false)
				return;

			AIWorker worker = assignmentPendingFeedbackWorker;
			if (worker == null)
			{
				ClearPendingAssignmentFeedbackTracking();
				SetAssignmentDragFeedback(
					"The queued worker assignment could not be completed.",
					AssignmentDragFeedback.Error);
				return;
			}

			bool targetAvailable = assignmentPendingFeedbackRole == WorkforceRole.Undefined
				? workerManager?.CanRequestWorkerUnassignment(worker) == true
				: workerManager?.CanRequestWorkerRoleAssignment(
					worker,
					assignmentPendingFeedbackBuildingId,
					assignmentPendingFeedbackRole) == true;
			if (targetAvailable == false)
			{
				ClearPendingAssignmentFeedbackTracking();
				SetAssignmentDragFeedback(
					$"Worker #{worker.WorkerID}'s queued assignment could not be completed.",
					AssignmentDragFeedback.Error);
				return;
			}

			if (worker.HasPendingAssignment)
			{
				if (IsPendingFeedbackTarget(worker, pending: true))
					return;

				ClearPendingAssignmentFeedbackTracking();
				SetAssignmentDragFeedback(
					$"Worker #{worker.WorkerID}'s queued assignment was replaced.",
					AssignmentDragFeedback.Error);
				return;
			}

			uint buildingId = assignmentPendingFeedbackBuildingId;
			WorkforceRole role = assignmentPendingFeedbackRole;
			bool applied = IsPendingFeedbackTarget(worker, pending: false);
			ClearPendingAssignmentFeedbackTracking();
			if (applied)
			{
				SetAssignmentDropAcceptedFeedback(worker, buildingId, role, pending: false);
				return;
			}

			SetAssignmentDragFeedback(
				$"Worker #{worker.WorkerID}'s queued assignment was not applied.",
				AssignmentDragFeedback.Error);
		}

		private bool IsPendingFeedbackTarget(AIWorker worker, bool pending)
		{
			if (worker == null)
				return false;

			uint buildingId = pending
				? worker.PendingPrimaryBuildingId
				: worker.PrimaryBuildingId;
			IReadOnlyList<WorkerTask.TaskType> taskTypes = pending
				? worker.PendingAssignedTaskTypes
				: worker.AssignedTaskTypes;
			if (assignmentPendingFeedbackRole == WorkforceRole.Undefined)
			{
				return buildingId == 0 && (taskTypes == null || taskTypes.Count == 0);
			}

			if (buildingId != assignmentPendingFeedbackBuildingId ||
				WorkforceRoleCatalog.TryGetDefinition(
					assignmentPendingFeedbackRole,
					out WorkforceRoleDefinition definition) == false ||
				taskTypes == null ||
				taskTypes.Count != definition.TaskTypes.Count)
			{
				return false;
			}

			for (int i = 0; i < definition.TaskTypes.Count; ++i)
			{
				if (ContainsAssignmentTaskType(taskTypes, definition.TaskTypes[i]) == false)
					return false;
			}

			return true;
		}

		private static bool ContainsAssignmentTaskType(
			IReadOnlyList<WorkerTask.TaskType> taskTypes,
			WorkerTask.TaskType target)
		{
			for (int i = 0; i < taskTypes.Count; ++i)
			{
				if (taskTypes[i] == target)
					return true;
			}

			return false;
		}

		private void ClearPendingAssignmentFeedbackTracking()
		{
			assignmentPendingFeedbackWorker = null;
			assignmentPendingFeedbackBuildingId = 0;
			assignmentPendingFeedbackRole = WorkforceRole.Undefined;
			hasAssignmentPendingFeedback = false;
		}

		private void SetAssignmentDragFeedback(
			string message,
			AssignmentDragFeedback feedback)
		{
			if (assignmentDragStatus == null)
				return;

			assignmentDragStatus.text = string.IsNullOrWhiteSpace(message)
				? AssignmentDragInstruction
				: message;
			assignmentDragStatus.EnableInClassList(
				AssignmentFeedbackSuccessClass,
				feedback == AssignmentDragFeedback.Success);
			assignmentDragStatus.EnableInClassList(
				AssignmentFeedbackPendingClass,
				feedback == AssignmentDragFeedback.Pending);
			assignmentDragStatus.EnableInClassList(
				AssignmentFeedbackErrorClass,
				feedback == AssignmentDragFeedback.Error);
		}

		private static string GetAssignmentRoleDisplayName(WorkforceRole role)
		{
			return WorkforceRoleCatalog.TryGetDefinition(
				role,
				out WorkforceRoleDefinition definition)
				? definition.DisplayName
				: role.ToString();
		}

		private void FlushAssignmentsRefreshAfterDrag()
		{
			if (assignmentsRefreshPending &&
				initialized &&
				selectedTab == WorkforceTab.Assignments &&
				window?.IsOpen == true)
			{
				RefreshAssignments();
			}
		}
	}
}
