using TMPro;
using UnityEngine;
using static Unity.Collections.AllocatorManager;

public enum InfoBlockType
{
	KeyValue,
	Progress,
}

public abstract class InfoBlock
{
	public Sprite LineIcon = null;
	protected GameObject linkedObject = null;

	private InfoBlockType type;

	public InfoBlock(InfoBlockType type)
	{
		this.type = type;
	}

	public InfoBlockType InfoType => type;


	public void SetGameObject(GameObject go)
	{
		linkedObject = go;
		OnSetup();
	}

	public abstract string GetContent();
	public virtual void OnUpdate() { }
	protected abstract void OnSetup();
};

public class KeyValueBlock : InfoBlock
{
	public readonly string Key;
	public string Value;

	private TextMeshProUGUI textMesh;
	public KeyValueBlock(string key, string value) : base(InfoBlockType.KeyValue)
	{
		Key = key;
		Value = value;
	}

	public override string GetContent()
	{
		return $"{Key}: {Value}";
	}

	public override void OnUpdate()
	{
		if (textMesh != null)
			textMesh.text = GetContent();
	}

	protected override void OnSetup()
	{
		textMesh = linkedObject.GetComponent<TextMeshProUGUI>();
		textMesh.text = GetContent();
	}

	public void UpdateValue(string newValue)
	{
		Value = newValue;
		OnUpdate();
	}
}

public class ProgressBlock : InfoBlock
{
	public readonly string Label;
	public readonly float Normalized;
	public readonly string Text;

	public ProgressBlock(string label, float normalized01, string text) : base(InfoBlockType.Progress)
	{
		Label = label;
		Normalized = normalized01;
		Text = text;
	}

	public override string GetContent()
	{
		return $"{Label}: [{new string('#', (int)(Normalized * 10)).PadRight(10, '-')}]{Text}";
	}

	protected override void OnSetup() { }
}
