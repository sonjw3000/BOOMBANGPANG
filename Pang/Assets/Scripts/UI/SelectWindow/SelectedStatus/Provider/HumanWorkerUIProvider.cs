public class HumanWorkerUIProvider : WorkerUIProviderBase<HumanWorker>
{
	protected override string ResourceLabel => "Fatigue";
	protected override float ResourceValue => currentTarget != null ? currentTarget.Fatigue : 0.0f;
	protected override string ExtraProfileLabel => "Incidents";
	protected override string ExtraProfileDisplay => currentTarget != null ? currentTarget.IncidentCount.ToString() : "0";
	protected override string DebugProfileLabel => "Unsafe Exposure";
	protected override string DebugProfileDisplay => currentTarget != null ? $"{currentTarget.UnsafeExposure:0.00}" : "0.00";
}
