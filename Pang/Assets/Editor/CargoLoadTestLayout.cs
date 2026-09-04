using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

[Serializable]
public sealed class CargoLoadTestSettings
{
	public int pairCount = 2;
	public int diameter = 21;
	public int armWidth = 9;
	public int holeSize = 3;
	public int gap = 4;
	public int workers = 20;
	public int seed = 12345;
	public int maxAttempts = 4000;
	public RectInt bounds = new(2, 2, 96, 96);
	public float supplyInterval = 0.25f;
	public int operationsPerFrame = 32;
	public float warmupSeconds = 10;
	public float measureSeconds = 60;
	public bool recoverTestWorkerFatigue = true;
}

// Plans without mutating the world. Conservative bounding-box spacing preserves exterior corridors.
public sealed class CargoLoadTestLayout
{
	public readonly struct Port
	{
		public readonly Vector2Int Cell;
		public readonly Vector2Int Outside;
		public readonly FacingDirection Facing;
		public Port(Vector2Int cell, Vector2Int outside, FacingDirection facing)
		{
			Cell = cell;
			Outside = outside;
			Facing = facing;
		}
	}

	private static readonly Vector2Int[] directions =
	{
		Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left,
	};
	private static readonly FacingDirection[] facings =
	{
		FacingDirection.North, FacingDirection.East, FacingDirection.South, FacingDirection.West,
	};
	public readonly List<Vector2Int> Centers = new();
	public readonly List<Port> Ports = new();
	public BuildingFootprintCell[] Cells { get; private set; }
	public RectInt SpawnBounds { get; private set; }
	public Vector2Int SpawnPoint => SpawnBounds.min;
	public int ReachablePortCount { get; private set; }

	public static CargoLoadTestLayout Plan(CargoLoadTestSettings settings, GridService grid, AreaManager areas)
	{
		ValidateSettings(settings);
		RectInt area = settings.bounds;
		int3 map = grid.MapSize;
		if (area.xMin < 0 || area.yMin < 0 || area.xMax > map.x || area.yMax > map.z)
			throw new InvalidOperationException("배치 영역이 현재 맵을 벗어납니다. 맵 크기에 맞게 영역을 줄이세요.");
		CargoLoadTestLayout plan = new();
		plan.BuildShape(settings);
		int spawnWidth = Mathf.Max(2, Mathf.CeilToInt(Mathf.Sqrt(Mathf.Max(1, settings.workers))));
		int spawnHeight = Mathf.Max(2, Mathf.CeilToInt(Mathf.Max(1, settings.workers) / (float)spawnWidth));
		plan.SpawnBounds = new RectInt(area.xMin + 1, area.yMin + 1, spawnWidth, spawnHeight);
		if (!Contains(area, Expand(plan.SpawnBounds, 1)) || !IsEmpty(plan.SpawnBounds, grid, areas))
			throw new InvalidOperationException("배치 영역 좌측 하단에 작업자 스폰 공간이 부족하거나 기존 시설/Area가 있습니다.");

		System.Random random = new(settings.seed);
		List<RectInt> occupied = new() { Expand(plan.SpawnBounds, settings.gap) };
		int size = settings.diameter;
		int half = size / 2;
		int minX = area.xMin + half + settings.gap;
		int minZ = area.yMin + half + settings.gap;
		int maxX = area.xMax - half - settings.gap;
		int maxZ = area.yMax - half - settings.gap;
		if (minX >= maxX || minZ >= maxZ)
			throw new InvalidOperationException("배치 영역이 건물 크기와 통로 간격보다 작습니다.");
		for (int attempt = 0; attempt < settings.maxAttempts && plan.Centers.Count < settings.pairCount * 2; ++attempt)
		{
			Vector2Int center = new(random.Next(minX, maxX), random.Next(minZ, maxZ));
			RectInt rect = new(center.x - half, center.y - half, size, size);
			bool collision = false;
			foreach (RectInt existing in occupied)
				if (rect.Overlaps(existing)) { collision = true; break; }
			if (collision || !IsEmpty(Expand(rect, settings.gap), grid, areas))
				continue;
			plan.Centers.Add(center);
			occupied.Add(Expand(rect, settings.gap));
		}
		if (plan.Centers.Count != settings.pairCount * 2)
			throw new InvalidOperationException($"공간 부족: 요청 {settings.pairCount}쌍, 후보 {plan.Centers.Count}/ {settings.pairCount * 2}개. 영역을 넓히거나 개수/크기를 줄이세요. 실제 배치는 시작하지 않았습니다.");
		plan.ValidateReachability(grid, settings.diameter, planned: true);
		return plan;
	}

	public static void ValidateSettings(CargoLoadTestSettings s)
	{
		if (s.pairCount < 1 || s.pairCount > 1000 || s.workers < 0 || s.workers > 10000 || s.maxAttempts < 1)
			throw new ArgumentException("쌍 개수는 1~1000, 작업자는 0~10000, 시도 횟수는 1 이상이어야 합니다.");
		if (s.diameter < 11 || s.diameter > 201 || (s.diameter & 1) == 0 || (s.armWidth & 1) == 0 ||
			(s.holeSize & 1) == 0 || s.holeSize < 1 || s.armWidth < s.holeSize + 6 || s.diameter < s.armWidth + 4)
			throw new ArgumentException("크기는 홀수: 전체 11~201, 팔 너비 ≥ 중앙 빈 공간 + 6, 전체 크기 ≥ 팔 너비 + 4.");
		if (s.gap < 3 || s.bounds.width <= 0 || s.bounds.height <= 0 || s.operationsPerFrame < 1 ||
			s.supplyInterval < 0 || s.warmupSeconds < 0 || s.measureSeconds < 1)
			throw new ArgumentException("건물 간격은 3 이상이며 배치 영역, 처리량, 측정 시간을 확인하세요.");
	}

	private void BuildShape(CargoLoadTestSettings s)
	{
		int size = s.diameter;
		int half = size / 2;
		bool[] owned = new bool[size * size];
		Cells = new BuildingFootprintCell[owned.Length];
		for (int z = 0; z < size; ++z)
			for (int x = 0; x < size; ++x)
			{
				int dx = Mathf.Abs(x - half), dz = Mathf.Abs(z - half);
				owned[z * size + x] = (dx <= s.armWidth / 2 || dz <= s.armWidth / 2) &&
					!(dx <= s.holeSize / 2 && dz <= s.holeSize / 2);
			}
		bool IsOwned(Vector2Int p) => p.x >= 0 && p.y >= 0 && p.x < size && p.y < size && owned[p.y * size + p.x];
		for (int z = 0; z < size; ++z)
			for (int x = 0; x < size; ++x)
			{
				Vector2Int p = new(x, z);
				if (!IsOwned(p)) continue;
				bool wall = false;
				foreach (Vector2Int d in directions) wall |= !IsOwned(p + d);
				Cells[z * size + x] = new BuildingFootprintCell(wall ? BuildingFootprintCellType.Wall : BuildingFootprintCellType.Interior);
			}
		for (int z = 0; z < size; ++z)
			for (int x = 0; x < size; ++x)
			{
				if (!Cells[z * size + x].IsWall) continue;
				Vector2Int p = new(x, z);
				for (int i = 0; i < directions.Length; ++i)
				{
					Vector2Int outside = p + directions[i], inside = p - directions[i];
					bool centerHole = Mathf.Abs(outside.x - half) <= s.holeSize / 2 && Mathf.Abs(outside.y - half) <= s.holeSize / 2;
					if (IsOwned(outside) || centerHole || !IsOwned(inside) || Cells[inside.y * size + inside.x].IsWall) continue;
					Ports.Add(new Port(p - new Vector2Int(half, half), outside - new Vector2Int(half, half), facings[i]));
					break;
				}
			}
		if (Ports.Count == 0) throw new InvalidOperationException("외부에 설치 가능한 포트가 없습니다.");
	}

	public void ValidateReachability(GridService grid, int diameter, bool planned)
	{
		int width = grid.MapSize.x, height = grid.MapSize.z;
		bool[] blocked = new bool[width * height];
		for (int z = 0; z < height; ++z)
			for (int x = 0; x < width; ++x)
			{
				GridCell cell = grid.GetCell(x, 0, z);
				// Interaction cells retain the port's occupancy reference but are walkable.
				blocked[z * width + x] = cell == null || cell.BuildingId != 0 || cell.IsBlocked ||
					((cell.Flags & GridFlags.DynamicObstacle) != 0 && cell.OccupancyWorker == null);
			}
		if (planned)
			foreach (Vector2Int center in Centers)
				for (int z = 0; z < diameter; ++z)
					for (int x = 0; x < diameter; ++x)
						if (Cells[z * diameter + x].IsOwned)
							blocked[(center.y + z - diameter / 2) * width + center.x + x - diameter / 2] = true;
		bool[] reached = Flood(width, height, blocked, SpawnPoint);
		ReachablePortCount = 0;
		foreach (Vector2Int center in Centers)
			foreach (Port port in Ports)
			{
				Vector2Int p = center + port.Outside;
				if (p.x < 0 || p.y < 0 || p.x >= width || p.y >= height || !reached[p.y * width + p.x])
					throw new InvalidOperationException($"접근 불가 포트: {p}. 배치 영역 또는 Seed를 바꾸세요.");
				++ReachablePortCount;
			}
		for (int z = SpawnBounds.yMin; z < SpawnBounds.yMax; ++z)
			for (int x = SpawnBounds.xMin; x < SpawnBounds.xMax; ++x)
				if (!reached[z * width + x]) throw new InvalidOperationException("스폰 공간이 통행 영역에서 분리되어 있습니다.");
	}

	private static bool[] Flood(int width, int height, bool[] blocked, Vector2Int start)
	{
		bool[] seen = new bool[blocked.Length];
		Queue<Vector2Int> queue = new();
		if (blocked[start.y * width + start.x]) return seen;
		seen[start.y * width + start.x] = true;
		queue.Enqueue(start);
		while (queue.Count > 0)
		{
			Vector2Int p = queue.Dequeue();
			foreach (Vector2Int d in directions)
			{
				Vector2Int next = p + d;
				if (next.x < 0 || next.y < 0 || next.x >= width || next.y >= height) continue;
				int index = next.y * width + next.x;
				if (seen[index] || blocked[index]) continue;
				seen[index] = true;
				queue.Enqueue(next);
			}
		}
		return seen;
	}

	private static bool IsEmpty(RectInt rect, GridService grid, AreaManager areas)
	{
		foreach (Area area in areas.RegisteredAreas)
			if (area.Floor == 0 && rect.Overlaps(area.Bounds)) return false;
		for (int z = rect.yMin; z < rect.yMax; ++z)
			for (int x = rect.xMin; x < rect.xMax; ++x)
			{
				GridCell cell = grid.GetCell(x, 0, z);
				if (cell == null || cell.BuildingId != 0 || !cell.CanPlaceObject || cell.IsBlocked ||
					cell.OccupancyObjectOnGrid != null || cell.OccupancyWorker != null) return false;
			}
		return true;
	}
	private static RectInt Expand(RectInt r, int amount) => new(r.x - amount, r.y - amount, r.width + amount * 2, r.height + amount * 2);
	private static bool Contains(RectInt outer, RectInt inner) => inner.xMin >= outer.xMin && inner.yMin >= outer.yMin && inner.xMax <= outer.xMax && inner.yMax <= outer.yMax;
}
