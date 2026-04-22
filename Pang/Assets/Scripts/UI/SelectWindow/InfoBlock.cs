using UnityEngine;

public abstract class InfoBlock
{
	public Sprite LineIcon = null;

	public abstract string GetContent();
};

public class KeyValueBlock : InfoBlock
{
	public readonly string Key;
	public readonly string Value;

	public KeyValueBlock(string key, string value)
	{
		Key = key;
		Value = value;
	}

	public override string GetContent()
	{
		return $"{Key}: {Value}";
	}
}

public class ProgressBlock : InfoBlock
{
	public readonly string Label;
	public readonly float Normalized;
	public readonly string Text;

	public ProgressBlock(string label, float normalized01, string text)
	{
		Label = label;
		Normalized = normalized01;
		Text = text;
	}

	public override string GetContent()
	{
		return $"{Label}: [{new string('#', (int)(Normalized * 10)).PadRight(10, '-')}]{Text}";
	}
}
