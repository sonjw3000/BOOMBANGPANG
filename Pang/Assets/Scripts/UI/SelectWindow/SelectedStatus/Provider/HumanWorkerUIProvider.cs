public class HumanWorkerUIProvider : WorkerUIProviderBase<HumanWorker>
{
	protected override string ResourceLabel => "Fatigue";
	protected override float ResourceValue => currentTarget != null ? currentTarget.Fatigue : 0.0f;
}
