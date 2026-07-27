public sealed class CorrosionService
{
	public void ReportTrigger(in ItemDamageIncidentTrigger trigger)
	{
		ItemDamageChange damage = trigger.DamageChange;
		GameContext.Instance.HudEventManager?.Publish(
			HudEventType.Warning,
			$"CORROSION item:{damage.ItemId} @({trigger.OriginCell.x},{trigger.OriginCell.y},{trigger.OriginCell.z}) " +
			$"R:{trigger.Radius} S:{trigger.Severity} D:{damage.PreviousDamage:0.##}>{damage.CurrentDamage:0.##} {damage.Cause}");
	}
}
