

// 카테고리화 하기 위함
public enum PlaceableDefinitionType
{
	// 벽 등 기타
	Obstacle,

	// 선반 등 기타
	Shelf,

	// 작업자
	Worker,

	// CargoPort(IB, OB 등)
	CargoPort,

	// 로켓 발사 관련
	LaunchStation,
	Other
}

[System.Flags]
public enum GridFlags
{
	None = 0,
	Blockplacement = 1 << 0,
	Interaction = 1 << 1,
	BlockMovement = 1 << 2,

	// 동적 장애물 (작업자, 이동 선반 등)
	DynamicObstacle = 1 << 3,
}

public enum InteractionKind : byte
{
	None = 0,
	Unload,
	Pick,
	Store,
	Load,
	Charge,
}

public enum CargoPortType : byte
{
	None = 0,
	Inbound,
	Outbound
}
