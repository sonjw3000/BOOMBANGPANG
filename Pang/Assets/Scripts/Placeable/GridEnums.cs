

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

public enum FacingDirection
{
	North = 0,
	East,
	South,
	West,

}

public static class FacingDirectionExtantion
{
	public static FacingDirection Rotate90CW(this FacingDirection dir)
	{
		return (FacingDirection)(((int)dir + 1) % 4);
	}
}

[System.Flags]
public enum GridFlags
{
	None = 0,
	BlockPlacement = 1 << 0,
	BlockMovement = 1 << 1,
	Interaction = 1 << 2,

	// 동적 장애물 (작업자, 이동 선반 등)
	DynamicObstacle = 1 << 3,
}

[System.Flags]
public enum InteractionKind : byte
{
	None = 0,
	Pick = 1 << 0,
	Put = 1 << 1,
	Work = 1 << 2,
	Charge = 1 << 3,
}

public enum CargoPortType : byte
{
	None = 0,
	Inbound,
	Outbound
}
