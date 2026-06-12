using UnityEngine;

public class CargoPort : 
	ShelfBase
{
	// ib/ob 구분
	// 런타임에 수정되면 안된다
	[SerializeField] private bool isInbound = true;

	private bool inputReady = true;

	public bool InputReady => inputReady;
	public override WorkerStatusTarget BuildingTarget => WorkerStatusTarget.CargoPort;
	public bool IsInbound => isInbound;

	public void SetInputReady(bool ready)
	{
		inputReady = ready;
	}

	public CargoPortSaveData CaptureState()
	{
		return new CargoPortSaveData
		{
			InputReady = inputReady,
		};
	}

	public void RestoreState(CargoPortSaveData data)
	{
		if (data == null)
			return;

		inputReady = data.InputReady;
	}

}
