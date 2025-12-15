# 코드 리뷰 요약

## 전체 인상
- 주요 시스템(GameContext, Worker/Task 매니저 등)이 MonoBehaviour 싱글톤/매니저 형태로 구성되어 있으며, Task 파이프라인과 워커 할당 로직이 분리되어 있습니다.
- 여러 곳에 TODO와 임시 주석이 남아 있어 앞으로의 설계 방향은 드러나지만, 현재 상태에서는 안전성·일관성 면에서 보강이 필요합니다.

## 장점
- `GameContext`가 리소스와 워크플로 매니저를 직렬화 필드로 관리하여 씬 간 공유 지점을 명확히 합니다. 【F:Assets/Scripts/GameContext.cs†L25-L52】
- `WorkerManager`가 작업 타입별로 아이들 워커 큐를 분리해 빠른 할당을 의도하고 있습니다. 【F:Assets/Scripts/AI/WorkerManager.cs†L17-L124】
- `TaskManager`가 태스크 타입별 큐를 갖고 디스패치 루프를 단순하게 유지합니다. 【F:Assets/Scripts/Task/TaskManager.cs†L10-L49】

## 개선이 필요한 부분
- **싱글톤 중복 처리**: `GameContext` 중복 시 `Destroy(this)`만 호출해 GameObject가 남고, 경고 메시지 오타도 있습니다. 일반적으로 `Destroy(gameObject)` 사용이 안전합니다. 【F:Assets/Scripts/GameContext.cs†L54-L67】
- **MonoBehaviour 생성자 사용**: `TaskManager`가 생성자에서 큐를 초기화하지만 Unity에서는 호출되지 않으므로 `Awake`/`OnEnable`로 옮겨야 합니다. 【F:Assets/Scripts/Task/TaskManager.cs†L15-L23】
- **필드 네이밍/일관성**: `InboundWorkflowManager` 등에서 `zoneSize`, `inboundBufferZone` 같은 필드가 있지만 초기화/검증 로직이 없고, 변수명 규칙도 혼재되어 있습니다. 【F:Assets/Scripts/Task/InboundWorkflowManager.cs†L13-L31】
- **사용되지 않는/미완성 로직**: `InboundWorkflowManager`의 `goalPos` 계산값이 사용되지 않고, 실제 작업 생성도 간단한 큐 추가에 머물러 있습니다. 【F:Assets/Scripts/Task/InboundWorkflowManager.cs†L22-L31】
- **상태 관리 누락**: `WorkerManager`의 `globalBlackboard`가 어디서 세팅되는지 불분명하며 `Update`에서 null 가능성이 있습니다. 【F:Assets/Scripts/AI/WorkerManager.cs†L27-L142】
- **인코딩 문제**: 다수 파일 헤더 주석이 깨져 있어(예: `GameContext`, `InboundWorkflowManager`, `WMSystem`) 가독성이 떨어집니다. 【F:Assets/Scripts/GameContext.cs†L4-L7】【F:Assets/Scripts/Task/InboundWorkflowManager.cs†L1-L10】【F:Assets/Scripts/WMSystem.cs†L1-L6】

## 권장 조치
- 싱글톤 충돌 시 `Destroy(gameObject)`로 정리하고, 초기화 실패 시 명시적 예외나 씬 전환 방지를 고려합니다.
- MonoBehaviour 초기화 코드는 `Awake`로 이동하고, 에디터 직렬화 필드 초기화 유무를 검사하는 어서션/로그를 추가합니다.
- 네이밍 컨벤션(PascalCase 필드/프로퍼티, camelCase 지역 변수)을 정리하고 일괄 적용합니다.
- `WorkerManager`의 `globalBlackboard`를 외부에서 주입하거나 `Awake`에서 생성하도록 명확히 합니다.
- TODO가 많은 구간(입출고 워크플로, 작업자 능력 검증)에 대해 단계별 작업 목록을 작성해 우선순위를 정리합니다.
- 깨진 주석을 UTF-8로 정리하고 필요 시 한국어/영어 중 한 언어로 통일합니다.
