
public interface IBoundService
{
	public CargoPortManager CargoPorts { get; }

	public void OnTaskCompleted(WorkerTask task);
}
