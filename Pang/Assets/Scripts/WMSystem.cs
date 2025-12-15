using UnityEngine;


// 인게임 매니저들의 허브
// 현재는 boxbuffer만 존재

public class WMSystem : MonoBehaviour
{
	private BoxPoolService boxPoolRegistry = new();

	private CargoPortService cargoPoolService = new();

	public BoxPoolService BoxPoolMgr => boxPoolRegistry;
	public CargoPortService CargoPorts => cargoPoolService;

}