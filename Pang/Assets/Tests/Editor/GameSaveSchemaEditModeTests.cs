using System;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class GameSaveSchemaEditModeTests
{
	[Test]
	public void TryReadSaveData_RejectsPreviousSchemaAndAcceptsCurrentSchema()
	{
		string path = Path.Combine(
			Path.GetTempPath(),
			$"pang-save-schema-{Guid.NewGuid():N}.json");

		try
		{
			GameSaveData previous = new()
			{
				Version = GameSaveData.CurrentVersion - 1,
			};
			File.WriteAllText(path, JsonUtility.ToJson(previous));
			LogAssert.Expect(
				LogType.Warning,
				new Regex(@"\[Save\] Unsupported save version"));

			Assert.That(GameSaveService.TryReadSaveData(path, out GameSaveData rejected), Is.False);
			Assert.That(rejected, Is.Null);

			GameSaveData current = new();
			File.WriteAllText(path, JsonUtility.ToJson(current));

			Assert.That(GameSaveService.TryReadSaveData(path, out GameSaveData accepted), Is.True);
			Assert.That(accepted, Is.Not.Null);
			Assert.That(accepted.Version, Is.EqualTo(GameSaveData.CurrentVersion));
		}
		finally
		{
			if (File.Exists(path))
				File.Delete(path);
		}
	}
}
