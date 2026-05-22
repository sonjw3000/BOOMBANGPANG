using UnityEngine;

public sealed class PlaceableOverrideReport
{
	public GameObject TargetObject { get; }
	public PlaceableDefinition TargetDefinition { get; }
	public PlaceableDefinition OverridingDefinition { get; }
	public GameObject OverridingObject { get; }

	public PlaceableOverrideReport(
		GameObject targetObject,
		PlaceableDefinition targetDefinition,
		PlaceableDefinition overridingDefinition,
		GameObject overridingObject)
	{
		TargetObject = targetObject;
		TargetDefinition = targetDefinition;
		OverridingDefinition = overridingDefinition;
		OverridingObject = overridingObject;
	}
}
