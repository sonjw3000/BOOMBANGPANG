using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

public readonly struct GameSaveFileSummary
{
	public GameSaveFileSummary(string filePath, string saveName, string savedAtText, int version, int money, float reputation, bool isLoadable, string statusText)
	{
		FilePath = filePath;
		SaveName = saveName;
		SavedAtText = savedAtText;
		Version = version;
		Money = money;
		Reputation = reputation;
		IsLoadable = isLoadable;
		StatusText = statusText;
	}

	public string FilePath { get; }
	public string SaveName { get; }
	public string SavedAtText { get; }
	public int Version { get; }
	public int Money { get; }
	public float Reputation { get; }
	public bool IsLoadable { get; }
	public string StatusText { get; }
}

public static class GameSaveFileCatalog
{
	public static IReadOnlyList<GameSaveFileSummary> EnumerateAllJsonFiles()
	{
		return GameSaveService.EnumerateJsonSaveFiles()
			.Select(CreateSummary)
			.OrderByDescending(summary => File.GetLastWriteTimeUtc(summary.FilePath))
			.ToArray();
	}

	private static GameSaveFileSummary CreateSummary(string filePath)
	{
		string saveName = Path.GetFileNameWithoutExtension(filePath);

		if (GameSaveService.TryReadSaveData(filePath, out GameSaveData data) == false)
		{
			return new GameSaveFileSummary(
				filePath,
				saveName,
				"-",
				0,
				0,
				0f,
				false,
				"로드할 수 없는 파일");
		}

		return new GameSaveFileSummary(
			filePath,
			saveName,
			FormatSavedAt(data.SavedAtUtc),
			data.Version,
			data.Economy != null ? data.Economy.Money : 0,
			data.Economy != null ? data.Economy.Reputation : 0f,
			true,
			"로드 가능");
	}

	private static string FormatSavedAt(string savedAtUtc)
	{
		if (string.IsNullOrWhiteSpace(savedAtUtc))
			return "-";

		if (DateTime.TryParse(savedAtUtc, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTime parsed) == false)
			return savedAtUtc;

		return parsed.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
	}
}
