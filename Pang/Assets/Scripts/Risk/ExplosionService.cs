using UnityEngine;

public sealed class ExplosionService : MonoBehaviour
{
	public void ReportTrigger(in ItemDamageIncidentTrigger trigger)
	{
		ItemDamageChange damage = trigger.DamageChange;
		GameContext.Instance.HudEventManager?.Publish(
			HudEventType.Error,
			$"EXPLOSION item:{damage.ItemId} @({trigger.OriginCell.x},{trigger.OriginCell.y},{trigger.OriginCell.z}) " +
			$"R:{trigger.Radius} S:{trigger.Severity} D:{damage.PreviousDamage}>{damage.CurrentDamage} {damage.Cause}",
			this);
	}
}
