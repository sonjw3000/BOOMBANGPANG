
public interface IBoundManager
{
	public CargoPortService CargoPorts { get; }

	public void OnTaskCompleted(WorkerTask task);
}