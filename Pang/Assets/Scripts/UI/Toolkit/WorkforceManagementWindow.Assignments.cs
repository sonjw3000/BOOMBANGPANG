using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace UniverseLogistics.UI.Toolkit
{
	public sealed partial class WorkforceManagementWindow
	{
		private readonly struct WorkforceAssignmentRoleKey : IEquatable<WorkforceAssignmentRoleKey>
		{
			public uint BuildingId { get; }
			public WorkforceRole Role { get; }

			public WorkforceAssignmentRoleKey(uint buildingId, WorkforceRole role)
			{
				BuildingId = buildingId;
				Role = role;
			}

			public bool Equals(WorkforceAssignmentRoleKey other)
			{
				return BuildingId == other.BuildingId && Role == other.Role;
			}

			public override bool Equals(object obj)
			{
				return obj is WorkforceAssignmentRoleKey other && Equals(other);
			}

			public override int GetHashCode()
			{
				return HashCode.Combine(BuildingId, (int)Role);
			}
		}

		private Button assignmentsButton;
		private VisualElement assignmentsTab;
		private Label assignmentUnassignedCount;
		private ScrollView assignmentUnassignedList;
		private Label assignmentUnassignedEmpty;
		private ScrollView assignmentTree;
		private Label assignmentTreeEmpty;
		private BuildingManager buildingManager;
		private readonly List<AIWorker> assignmentUnassignedBuffer = new();
		private readonly List<WorkforceRoleWorkerEntry> assignmentRoleWorkerBuffer = new();
		private readonly HashSet<WorkforceAssignmentRoleKey> expandedAssignmentRoles = new();
		private readonly HashSet<uint> collapsedAssignmentScopes = new();
		private bool assignmentsRefreshPending = true;

		private void InitializeAssignmentsView(VisualElement content)
		{
			assignmentsButton = content.Q<Button>("workforce-assignments-button");
			assignmentsTab = content.Q<VisualElement>("workforce-assignments-tab");
			assignmentUnassignedCount = content.Q<Label>("workforce-unassigned-count");
			assignmentUnassignedList = content.Q<ScrollView>("workforce-unassigned-list");
			assignmentUnassignedEmpty = content.Q<Label>("workforce-unassigned-empty");
			assignmentTree = content.Q<ScrollView>("workforce-assignment-tree");
			assignmentTreeEmpty = content.Q<Label>("workforce-assignment-tree-empty");
			InitializeAssignmentDragView(content);
		}

		private bool HasRequiredAssignmentsView()
		{
			return assignmentsButton != null &&
				assignmentsTab != null &&
				assignmentUnassignedCount != null &&
				assignmentUnassignedList != null &&
				assignmentUnassignedEmpty != null &&
				assignmentTree != null &&
				assignmentTreeEmpty != null &&
				HasRequiredAssignmentDragView();
		}

		private void RequestAssignmentsRefresh()
		{
			assignmentsRefreshPending = true;
		}

		private void LateUpdate()
		{
			if (assignmentsRefreshPending &&
				initialized &&
				selectedTab == WorkforceTab.Assignments &&
				window?.IsOpen == true &&
				IsAssignmentDragInteractionActive == false)
			{
				RefreshAssignments();
			}

			ProcessPendingRosterRefresh();
			ProcessPendingRosterSummary();
		}

		private void RefreshAssignments()
		{
			if (HasRequiredAssignmentsView() == false)
				return;
			if (IsAssignmentDragInteractionActive)
			{
				assignmentsRefreshPending = true;
				return;
			}

			assignmentsRefreshPending = false;
			PruneAssignmentScopeState();
			Vector2 unassignedScrollOffset = assignmentUnassignedList.scrollOffset;
			Vector2 treeScrollOffset = assignmentTree.scrollOffset;
			assignmentUnassignedList.Clear();
			ClearAssignmentRoleDropTargets();
			assignmentUnassignedBuffer.Clear();
			workerManager?.GetOperationalUnassignedWorkers(assignmentUnassignedBuffer);
			for (int i = 0; i < assignmentUnassignedBuffer.Count; ++i)
			{
				AIWorker worker = assignmentUnassignedBuffer[i];
				if (worker != null)
				{
					assignmentUnassignedList.Add(CreateAssignmentWorkerRow(
						worker,
						null,
						0,
						WorkforceRole.Undefined));
				}
			}

			assignmentUnassignedCount.text = $"{assignmentUnassignedBuffer.Count} WORKERS";
			assignmentUnassignedEmpty.style.display = assignmentUnassignedBuffer.Count > 0
				? DisplayStyle.None
				: DisplayStyle.Flex;
			assignmentUnassignedList.scrollOffset = unassignedScrollOffset;

			assignmentTree.Clear();
			AddAssignmentScope("PUBLIC LOGISTICS", "PUBLIC TASKS", 0);
			IReadOnlyList<Building> buildings = buildingManager?.RegisteredBuildings;
			if (buildings != null)
			{
				for (int i = 0; i < buildings.Count; ++i)
				{
					Building building = buildings[i];
					if (building == null)
						continue;

					AddAssignmentScope(
						building.DisplayName,
						"BUILDING TASKS",
						building.RuntimeBuildingId);
				}
			}

			assignmentTree.scrollOffset = treeScrollOffset;
			assignmentTreeEmpty.style.display = assignmentTree.contentContainer.childCount > 0
				? DisplayStyle.None
				: DisplayStyle.Flex;
			RefreshPendingAssignmentFeedback();
		}

		private void AddAssignmentScope(
			string displayName,
			string scopeType,
			uint buildingId)
		{
			IReadOnlyList<WorkforceRole> roles = WorkforceRoleCatalog.GetRoles(buildingId);
			bool collapsed = collapsedAssignmentScopes.Contains(buildingId);
			VisualElement group = new();
			group.AddToClassList("workforce-assignment-group");
			group.EnableInClassList("workforce-assignment-group--collapsed", collapsed);
			group.userData = buildingId;

			VisualElement header = new();
			header.AddToClassList("workforce-assignment-group__header");
			Button toggle = new(() => ToggleAssignmentScope(buildingId));
			toggle.text = collapsed ? ">" : "v";
			toggle.tooltip = collapsed ? "Expand workforce roles" : "Collapse workforce roles";
			toggle.AddToClassList("workforce-assignment-group__toggle");
			VisualElement identity = new();
			identity.AddToClassList("workforce-assignment-group__identity");
			Label name = new(displayName);
			name.AddToClassList("workforce-assignment-group__name");
			Label type = new(scopeType);
			type.AddToClassList("workforce-assignment-group__type");
			Label summary = new();
			summary.AddToClassList("workforce-assignment-group__summary");
			identity.Add(name);
			identity.Add(type);
			header.Add(toggle);
			header.Add(identity);
			header.Add(summary);
			group.Add(header);

			int activeRoleCount = 0;
			if (collapsed)
			{
				for (int i = 0; i < roles.Count; ++i)
				{
					if (GetAssignmentRoleOperationalCount(buildingId, roles[i]) > 0)
						++activeRoleCount;
				}
			}
			else
			{
				for (int i = 0; i < roles.Count; ++i)
				{
					VisualElement role = CreateAssignmentRole(
						buildingId,
						roles[i],
						out int roleCount);
					if (roleCount > 0)
						++activeRoleCount;
					group.Add(role);
				}
			}

			summary.text = $"{activeRoleCount} / {roles.Count} ACTIVE ROLES";
			if (collapsed == false && roles.Count == 0)
			{
				Label empty = new("No workforce roles");
				empty.AddToClassList("workforce-assignment-group__empty");
				group.Add(empty);
			}

			assignmentTree.Add(group);
		}

		private VisualElement CreateAssignmentRole(
			uint buildingId,
			WorkforceRole role,
			out int count)
		{
			WorkforceRoleCatalog.TryGetDefinition(role, out WorkforceRoleDefinition definition);
			WorkforceRoleSummary roleSummary = default;
			bool hasSummary = workerManager?.TryGetWorkforceRoleSummary(
				buildingId,
				role,
				out roleSummary) == true;
			count = hasSummary ? roleSummary.OperationalCount : 0;
			int displayedCount = count;
			WorkforceAssignmentRoleKey key = new(buildingId, role);
			bool expanded = displayedCount > 0 && expandedAssignmentRoles.Contains(key);

			VisualElement container = new();
			VisualElement roleRow = new();
			roleRow.AddToClassList("workforce-assignment-role");
			roleRow.userData = role;
			roleRow.EnableInClassList(
				"workforce-assignment-role--partial",
				hasSummary && roleSummary.PartialCount > 0);
			RegisterAssignmentRoleDropTarget(roleRow, buildingId, role);

			Button toggle = new(() => ToggleAssignmentRole(buildingId, role, displayedCount));
			toggle.text = expanded ? "v" : ">";
			toggle.AddToClassList("workforce-assignment-role__toggle");
			toggle.SetEnabled(displayedCount > 0);
			Label roleName = new(definition?.DisplayName ?? role.ToString());
			roleName.AddToClassList("workforce-assignment-role__name");
			Label roleCount = new(displayedCount.ToString());
			roleCount.AddToClassList("workforce-assignment-role__count");
			roleRow.Add(toggle);
			roleRow.Add(roleName);
			roleRow.Add(roleCount);
			container.Add(roleRow);

			if (expanded)
			{
				VisualElement workers = new();
				workers.AddToClassList("workforce-assignment-role-workers");
				assignmentRoleWorkerBuffer.Clear();
				if (workerManager?.TryGetWorkforceRoleWorkers(
						buildingId,
						role,
						assignmentRoleWorkerBuffer) == true)
				{
					for (int i = 0; i < assignmentRoleWorkerBuffer.Count; ++i)
					{
						WorkforceRoleWorkerEntry entry = assignmentRoleWorkerBuffer[i];
						if (entry.Worker != null)
						{
							workers.Add(CreateAssignmentWorkerRow(
								entry.Worker,
								entry.AssignmentState,
								buildingId,
								role));
						}
					}
				}

				container.Add(workers);
			}

			return container;
		}

		private int GetAssignmentRoleOperationalCount(uint buildingId, WorkforceRole role)
		{
			return workerManager?.TryGetWorkforceRoleSummary(
				buildingId,
				role,
				out WorkforceRoleSummary summary) == true
				? summary.OperationalCount
				: 0;
		}

		private void ToggleAssignmentScope(uint buildingId)
		{
			if (IsAssignmentDragInteractionActive)
				return;

			if (collapsedAssignmentScopes.Remove(buildingId) == false)
				collapsedAssignmentScopes.Add(buildingId);
			RefreshAssignments();
		}

		private void ToggleAssignmentRole(uint buildingId, WorkforceRole role, int count)
		{
			if (count <= 0)
				return;

			WorkforceAssignmentRoleKey key = new(buildingId, role);
			if (expandedAssignmentRoles.Remove(key) == false)
				expandedAssignmentRoles.Add(key);
			RefreshAssignments();
		}

		private TemplateContainer CreateAssignmentWorkerRow(
			AIWorker worker,
			WorkforceRoleAssignmentState? assignmentState,
			uint sourceBuildingId,
			WorkforceRole sourceRole)
		{
			TemplateContainer row = rosterRowTemplate.CloneTree();
			VisualElement root = row.Q<VisualElement>(className: "workforce-worker-row");
			root.AddToClassList("workforce-assignment-worker-row");
			root.userData = worker;
			root.EnableInClassList(
				"workforce-assignment-worker-row--partial",
				assignmentState == WorkforceRoleAssignmentState.Partial);
			row.Q<Label>("worker-row-name").text = $"{worker.Name}  #{worker.WorkerID}";
			row.Q<Label>("worker-row-kind").text = GetWorkerKind(worker);
			row.Q<Label>("worker-row-status").text = worker.EffectiveStatusAction.ToString();
			row.Q<Label>("worker-row-condition").style.display = DisplayStyle.None;
			row.Q<Label>("worker-row-wear").style.display = DisplayStyle.None;
			Label assignment = row.Q<Label>("worker-row-building");
			assignment.text = assignmentState.HasValue
				? assignmentState.Value.ToString().ToUpperInvariant()
				: "UNASSIGNED";
			root.RegisterCallback<PointerDownEvent>(evt =>
				OnAssignmentWorkerPointerDown(
					evt,
					root,
					worker,
					sourceBuildingId,
					sourceRole));
			return row;
		}

		private void OnBuildingsChanged()
		{
			RequestAssignmentsRefresh();
			PruneAssignmentScopeState();
			if (IsAssignmentDragInteractionActive)
				CancelAssignmentDrag();

			if (selectedBuilding != null &&
				(buildingManager == null ||
					buildingManager.TryGetBuilding(
						selectedBuilding.RuntimeBuildingId,
						out Building registeredBuilding) == false ||
					ReferenceEquals(registeredBuilding, selectedBuilding) == false))
			{
				selectedBuilding = null;
			}

			RequestRosterRebuild();
			ProcessPendingRosterRefresh();
		}

		private void PruneAssignmentScopeState()
		{
			collapsedAssignmentScopes.RemoveWhere(IsMissingAssignmentBuildingScope);
			expandedAssignmentRoles.RemoveWhere(key =>
				key.BuildingId != 0 && IsMissingAssignmentBuildingScope(key.BuildingId));
		}

		private bool IsMissingAssignmentBuildingScope(uint buildingId)
		{
			return buildingId != 0 &&
				(buildingManager == null ||
					buildingManager.TryGetBuilding(buildingId, out _) == false);
		}
	}
}
