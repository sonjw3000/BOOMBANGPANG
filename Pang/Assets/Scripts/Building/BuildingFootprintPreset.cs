using UnityEngine;

public enum BuildingFootprintCellType : byte
{
	None = 0,
	Interior = 1,
	Wall = 2,
}

[System.Serializable]
public struct BuildingFootprintCell
{
	[SerializeField] private BuildingFootprintCellType type;

	public BuildingFootprintCellType Type => type;
	public bool IsOwned => type != BuildingFootprintCellType.None;
	public bool IsWall => type == BuildingFootprintCellType.Wall;

	public BuildingFootprintCell(BuildingFootprintCellType type)
	{
		this.type = type;
	}
}

[CreateAssetMenu(menuName = "Building/Footprint Preset")]
public sealed class BuildingFootprintPreset : ScriptableObject
{
	public const int MinimumDiameter = 3;

	[SerializeField] private string presetId = "circle_15";
	[SerializeField] private string displayName = "Diameter 15";
	[SerializeField, Min(MinimumDiameter)] private int width = 15;
	[SerializeField, Min(MinimumDiameter)] private int height = 15;
	[SerializeField] private Vector2Int pivot = new(7, 7);
	[SerializeField] private BuildingFootprintCell[] cells;

	public string PresetId => presetId;
	public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? $"Diameter {width}" : displayName;
	public int Diameter => width;
	public int Width => width;
	public int Height => height;
	public Vector2Int Pivot => pivot;
	public bool IsValid =>
		string.IsNullOrWhiteSpace(presetId) == false &&
		width >= MinimumDiameter &&
		width == height &&
		(width % 2) == 1 &&
		pivot == new Vector2Int(width / 2, height / 2) &&
		cells != null &&
		cells.Length == width * height;

	public BuildingFootprintCell Get(int x, int z)
	{
		if (x < 0 || x >= width || z < 0 || z >= height || cells == null || cells.Length != width * height)
			return default;

		return cells[(z * width) + x];
	}

	public RectInt GetBounds(Vector2Int center)
	{
		return new RectInt(center.x - pivot.x, center.y - pivot.y, width, height);
	}

	public void InitializeCircle(string id, int diameter, string name = null)
	{
		if (string.IsNullOrWhiteSpace(id))
			throw new System.ArgumentException("A building footprint preset ID is required.", nameof(id));

		if (diameter < MinimumDiameter || (diameter % 2) == 0)
			throw new System.ArgumentOutOfRangeException(nameof(diameter), "Building footprint diameter must be an odd number of at least 3.");

		presetId = id;
		displayName = string.IsNullOrWhiteSpace(name) ? $"Diameter {diameter}" : name;
		width = diameter;
		height = diameter;
		pivot = new Vector2Int(diameter / 2, diameter / 2);
		cells = new BuildingFootprintCell[width * height];

		float radius = diameter * 0.5f;
		float radiusSquared = radius * radius;
		bool[] owned = new bool[cells.Length];

		for (int z = 0; z < height; ++z)
		{
			for (int x = 0; x < width; ++x)
			{
				int offsetX = x - pivot.x;
				int offsetZ = z - pivot.y;
				owned[(z * width) + x] = (offsetX * offsetX) + (offsetZ * offsetZ) <= radiusSquared;
			}
		}

		for (int z = 0; z < height; ++z)
		{
			for (int x = 0; x < width; ++x)
			{
				int index = (z * width) + x;
				if (owned[index] == false)
				{
					cells[index] = new BuildingFootprintCell(BuildingFootprintCellType.None);
					continue;
				}

				bool isWall =
					IsOwned(owned, x - 1, z) == false ||
					IsOwned(owned, x + 1, z) == false ||
					IsOwned(owned, x, z - 1) == false ||
					IsOwned(owned, x, z + 1) == false;

				cells[index] = new BuildingFootprintCell(
					isWall ? BuildingFootprintCellType.Wall : BuildingFootprintCellType.Interior);
			}
		}
	}

	[ContextMenu("Regenerate Circle From Current Diameter")]
	private void RegenerateCircle()
	{
		InitializeCircle(presetId, width, displayName);
	}

	private bool IsOwned(bool[] owned, int x, int z)
	{
		return x >= 0 && x < width && z >= 0 && z < height && owned[(z * width) + x];
	}
}
