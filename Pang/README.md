# Universe Logistics

> 달의 복수 물류 건물을 연결하고, 입고부터 정산까지 발생하는 병목을 찾아 안정화하는 물류 경영 시뮬레이션입니다.

<!-- 대표 게임플레이 GIF 추가 예정 -->

> 🚧 **현재 개발 중인 프로젝트입니다.** 핵심 물류 루프와 주요 운영 시스템을 구현했으며, 현재는 전체 흐름의 안정화와 UI·정보 전달 개선을 진행하고 있습니다.

## 게임 소개

인류가 태양계로 진출하기 시작한 근미래, 외딴 정착지는 달의 물류 허브를 거치는 불안정한 장거리 보급망에 의존하고 있습니다.

플레이어는 허브 운영자가 되어 건물을 배치하고, 건물 안에 필요한 시설을 설치하며, 화물 처리 Rule과 작업자를 설정합니다. 한 건물 안에서 전체 공정을 처리할 수도 있고, 보관·포장·출고 기능을 여러 건물에 나눈 뒤 `CargoPort`로 연결할 수도 있습니다.

화물은 화면 속 공간을 실제로 이동합니다. 작업 수요, 대기 중인 Task, 작업자 동선, 가득 찬 목적지와 끊긴 경로가 처리량을 바꿉니다. 혼잡과 지연은 제거해야 할 잡음이 아니라 플레이어가 원인을 찾고 해결해야 할 핵심 게임플레이입니다.

## 핵심 플레이 루프

```text
입고 → 보관 → 주문 → 피킹 → 포장 → 출고 → 정산
```

| 단계 | 플레이어가 관리하는 흐름 |
|---|---|
| 입고 | 로켓 도착, 착륙 결과, 화물 하역과 Inbound CargoPort 반입 |
| 보관 | 라벨링, Capsule 이동, Rule에 맞는 Buffer·선반으로 적치 |
| 주문 | 계약 수락, 주문 생성, 품목별 수량과 납기 추적 |
| 피킹 | 주문 Manifest에 따른 재고 예약과 피킹 작업 |
| 포장 | Packing Station 투입, 포장, 완성 화물 반출 |
| 출고 | Building 적재 임계치와 Outbound CargoPort Rule에 따른 Capsule 출고 |
| 정산 | 납기와 완료 결과에 따른 수익, 평판과 페널티 반영 |

## 주요 시스템

### 1. 물류 허브 건설과 Rule 기반 라우팅

- 하나의 범용 `Building`에 시설과 운영 Rule을 조합해 보관·포장·출고 역할을 구성합니다.
- 선반, Packing Station, Capsule Buffer, Inbound·Outbound CargoPort, Airlock, 휴게·충전 시설, 전력 및 발사 시설을 배치합니다.
- `CargoPort`는 외부 운송과 건물 간 물류를 연결하는 인터페이스입니다.
- Building 연결, 작업 범위, Capsule 출고 임계치를 운영 정책으로 설정합니다.
- `FacilityRule`은 품목 조건과 처리 단계를 기준으로 시설이 받을 수 있는 화물을 결정합니다.

화물의 공정 단계는 다음 계약을 공유합니다.

```text
Empty → Unlabeled → Labeled → Picked → Packed → LaunchReady
```

### 2. 눈에 보이는 작업 흐름과 작업자 운영

- 입고 하역, 라벨링, 보관, 피킹, 포장 투입·반출, 출고 정렬, Capsule 재배치와 폐기물 수거 Task가 물류 상태에서 생성됩니다.
- 각 Workflow·Planner·Coordinator가 필요한 자원을 예약해 Task를 만들고, `TaskManager`가 준비·배정·진행·반송 상태를 관리합니다.
- 인간과 로봇 작업자는 능력과 담당 Building·업무에 따라 Task를 배정받습니다.
- 인간의 피로와 휴식, 로봇의 배터리와 충전이 작업 지속성과 처리량에 영향을 줍니다.
- 작업자는 그리드 경로, 상호작용 지점, 이동 예약, Airlock과 교통 충돌 조정을 거쳐 실제 작업을 수행합니다.

### 3. 계약과 운영 성장

- 계약, 주문, 납기, 정산, 자금과 평판을 관리합니다.
- 선행 조건, 연구 대기열, 비용과 기간을 관리하며 새로운 시설·정책과 작업자 행동을 순차적으로 해금합니다.
- 회사 상태와 시설·운영 조건을 평가하는 라이선스 시스템이 포함되어 있습니다.

> **연구 효과 예시** — `Human Recognition` 연구 전에는 자율 로봇이 경로를 점유한 인간과 충돌해 작업 중단과 의료 사고를 일으킬 수 있습니다. 연구 후에는 인간과의 충돌 경로를 사용하지 않으며, `Traffic Control`과 함께 대기·재탐색·양보를 통해 교통 충돌을 처리합니다.

### 4. 화물 상태와 운영 위험

- 전력과 건물 온도가 화물 온도, 신선도와 손상에 영향을 주며 품질이 낮아진 화물은 폐기물과 손실을 만듭니다.
- 로켓의 경착륙과 충돌, 화물 손상, 인간·로봇 작업자 사고와 의료·수리 대응이 운영을 방해할 수 있습니다.

### 5. 병목을 읽는 운영 정보

- Selection Card와 Detail Window에서 Building, 시설, 작업자와 화물 상태를 확인하고 설정합니다.
- Logistics HUD와 Workflow Monitor에서 공정별 수요, 대기, 진행, 반송과 차단 상태를 Building 단위로 추적합니다.
- 연결 상태와 작업 범위를 확인하는 그리드 오버레이를 제공합니다.

## 게임 진행

현재 시나리오는 기본 주문 처리에서 시작해 연구와 콜드체인 운영으로 이어지는 목표를 순서대로 제공합니다.

1. 첫 주문을 완료합니다.
2. 주문 3건을 납기 내 완료합니다.
3. `Temperature Monitoring`과 `Thermal Operations`를 연구합니다.
4. `Traffic Control`과 `Human Recognition`을 연구합니다.
5. `Lunar Produce Cold Chain` 주문을 납기 내 완료하고 평판 50을 달성합니다.

목표가 진행되면서 연구, 교통 제어, 온도와 품질 관리가 기존 물류 흐름에 차례로 더해집니다.

## 구현 구조

중요한 상태가 여러 객체의 숨은 부수 효과로 변경되지 않도록, 상태 소유자와 Task 생성 경로를 분리했습니다.

```text
상태 변경
→ 이벤트 / Dirty 대상 수집
→ 관련 Building·Dock 재평가
→ Workflow / Planner / CapsuleRelocateCoordinator
→ TaskManager
→ 행동 트리 Worker
→ 물리 상태·운영 지표·UI 갱신
```

- `Building`은 물리 공간과 Building 단위 운영 정책을 소유합니다.
- `Facility`는 건물 안에 설치되는 실제 작업 기능입니다.
- `FacilityRule`은 시설의 처리 조건과 허용 범위를 표현합니다.
- `CapsuleRelocateCoordinator`는 Capsule의 Rule 매칭, 상태 정규화와 재배치를 담당합니다.
- Workflow와 Planner는 필요한 일을 감지하고 자원을 예약하며, `TaskManager`는 큐와 배정 상태를 관리하고, Worker는 행동 트리로 공간상의 작업을 실행합니다.
- 이벤트와 Dirty 재평가는 변경된 물류 대상만 다시 판단하고, 온도·위험·마모 같은 시간 기반 처리는 중앙 Simulation Tick에서 갱신합니다.

## 실행 방법

### 요구 사항

- Unity `6000.5.3f1`
- Git

### 프로젝트 실행

1. 저장소를 복제합니다.
2. Unity Hub에서 저장소 루트를 프로젝트로 추가합니다.
3. Unity `6000.5.3f1`로 프로젝트를 엽니다.
4. `Assets/Scenes/TitleScene.unity`를 열고 Play Mode를 시작합니다.
5. 타이틀 화면에서 새 게임을 시작합니다.

## 기본 조작

| 입력 | 동작 |
|---|---|
| `W` `A` `S` `D` | 카메라 이동 |
| 마우스 오른쪽 드래그 | 카메라 회전 |
| 마우스 휠 | 확대·축소 |
| `Esc` | 일시정지 메뉴 |

## 기술 정보

- Unity 6 / C#
- Universal Render Pipeline
- UI Toolkit 기반 HUD·관리 창·상세 정보 UI
- `GameContext` 중심의 서비스 접근과 상태 소유권 분리
- Workflow → TaskManager → 행동 트리 Worker 파이프라인
- 그리드 기반 건설, 경로 탐색, 이동 예약과 교통 충돌 조정
- 이벤트·Dirty 재평가와 중앙 Simulation Tick을 함께 사용하는 시뮬레이션 구조
- Building, 시설, 작업자, 화물, 주문, 연구, 경제, 시나리오와 진행 중 Task를 포함하는 저장·불러오기

## 문서

- [Project Identity](docs/project/identity.md)
- [Design Philosophy](docs/project/design_philosophy.md)
- [Current Gameplay Loop](docs/current/gameplay_loop.md)
- [Current Hub Structure](docs/current/hub_structure.md)
- [Current Systems](docs/current/system.md)
- [Architecture](docs/architecture/architecture.md)
