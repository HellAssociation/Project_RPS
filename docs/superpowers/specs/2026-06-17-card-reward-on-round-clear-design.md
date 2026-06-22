# 카드 보상을 라운드 클리어 시점으로 이동 — 설계

날짜: 2026-06-17
대상 파일: `InGameManager.cs`, `NetworkManager.cs`, `Enums.cs`

## 1. 목표

카드 선택(축복/저주 투표)을 **라운드 시작 시점**에서 **라운드 클리어 보상**으로 옮긴다.
라운드(10웨이브)를 클리어하면 KO 연출이 끝난 뒤 보상으로 카드를 선택한다.
보상은 되돌릴 일이 없으므로, 패배 시 이번 라운드 카드를 제거하던 로직을 삭제한다.

라운드 클리어 판정과 화면 전환은 **호스트 권위(host-authoritative)** 로 처리해, 호스트가 클리어를
브로드캐스트하고 모든 피어가 같은 화면(KO → 보상 투표 → 스테이지 선택)을 동기화해 따라간다.

## 2. 현재 동작

```
스테이지 선택 → [카드 투표] → Versus → 10웨이브 → 클리어(KO) → 다음 스테이지 선택 ...
```

- 카드 투표는 `HostPrepareAssignmentsCoroutine`(라운드 시작) 안에서 `HostCardVoteSequenceCoroutine`로 실행.
- 라운드 클리어 분기(`BetweenWavesCoroutine`의 `isRoundClear`)를 호스트·클라가 각자 로컬로 판정·실행하고, 각자 스테이지 패널을 연다.
- 패배(라이프 남음) 시 `RemoveCurrentRoundAugments()`로 이번 라운드에 고른 카드를 제거한다.
- `MAX_ROUNDS = 10`은 선언만 되어 있고 사용처가 없다(승리 처리 없음).

## 3. 변경 후 동작

```
스테이지 선택 → Versus → 10웨이브 → 클리어 → KO 연출 → [카드 보상 투표] → 다음 스테이지 선택 ...
```

- **라운드 1은 카드 없이 시작.** 첫 보상은 라운드 1 클리어 후 지급.
- 패배(라이프 남음) 시 카드는 그대로 유지(제거 로직 삭제).
- **최종 라운드(라운드 10) 클리어 시에는 보상 없음.** 승리 패널 훅(`OnGameClear`)만 호출하고, 실제 패널 연결은 추후 작업.

## 4. 상세 설계

### 4.1 라운드 시작 카드 투표 제거

`HostPrepareAssignmentsCoroutine`에서 `HostCardVoteSequenceCoroutine()` 호출을 제거한다.
이후 라운드 시작은 오브젝트 준비 → 손가락 배정 → ready 순서만 수행한다.
`HostCardVoteSequenceCoroutine` / `HostSingleVoteCoroutine` 자체는 보상에서 재사용하므로 유지한다.

### 4.2 호스트 주도 라운드 클리어 + 보상 (신규 RPC 2개)

기존 `ServerBroadcastBossIntro` 패턴(`PayloadWriter` + `EInGameRpsMessage` 타입 바이트 → `BroadcastInGamePayload`, 호스트는 로컬에서도 이벤트 직접 호출)을 그대로 따른다.

신규 RPC / 이벤트:

| 송신(NetworkManager) | 이벤트 | 의미 |
|---|---|---|
| `ServerRoundClear()` | `OnRoundCleared` | 호스트가 라운드 클리어를 선언. 전 피어가 동기화된 클리어 시퀀스를 시작한다. |
| `ServerRewardDone()` | `OnRewardDone` | 호스트가 보상 투표 종료를 통지. 클라가 스테이지 패널을 연다. |

`Enums.cs`의 `EInGameRpsMessage`에 `RoundClear`, `RewardDone` 값을 추가하고,
`NetworkManager`의 수신 switch에 두 케이스를 추가해 각각 `OnRoundCleared` / `OnRewardDone`를 발생시킨다.

동기화된 클리어 시퀀스(전 피어 공통, `InGameManager`):

```
[호스트] HandleWaveResultReceived 에서 라운드 클리어 판정(WonWavesInRound >= WAVES_PER_ROUND)
    → 적 HP 갱신/OnEnemyHpChanged 까지는 기존대로 수행
    → ServerRoundClear() 호출 (로컬에서도 OnRoundCleared 발생)

[전 피어] HandleRoundClear → RoundClearCoroutine 시작:
    1. KO 연출: VersusPanel.PlayKoSequence() 대기 (등장→유지→페이드아웃→닫기)
    2. clearedRound == MAX_ROUNDS 이면:
         OnGameClear?.Invoke()  // 승리 훅(현재 미연결). 보상/스테이지 패널 없이 종료.
         return
    3. 라운드 진행 처리:
         _run.CurrentRound++; _run.WonWavesInRound = 0;
         OnCurrentRoundChanged?.Invoke(); OnRoundClear?.Invoke(clearedRound);  // 로컬 UI 이벤트
    4. 카드 보상 투표:
         - 호스트: HostCardVoteSequenceCoroutine() 대기 → 끝나면 ServerRewardDone()
         - 클라:   CardVotePanel 이 카드 오퍼 이벤트로 자동 표시(기존 동작) → OnRewardDone 대기
    5. 입력 리셋 후 Phase = WaitingForRoundSelect, _hostSetupStarted = false
    6. 스테이지 패널 오픈 (호스트=직접, 클라=OnRewardDone 수신 후)
```

핵심:
- 클라는 라운드 클리어를 혼자 판정하지 않고 `OnRoundCleared` 신호로 시퀀스를 시작한다.
- 스테이지 패널 전환은 `OnRewardDone`에 맞춰 들어가 항상 같은 화면을 유지한다.
- 카드 보상 투표는 증가된 `_run.CurrentRound`(다음 라운드) 기준으로 카드 가용성을 판정한다.

기존 `BetweenWavesCoroutine`은 클리어가 아닌 경로(일반 웨이브 사이, 보스 인트로, 게임오버)만 담당한다.
KO 연출은 클리어 시 `RoundClearCoroutine`에서 재생하므로, 클리어 경로가 `BetweenWavesCoroutine`의
`isRoundClear` 분기로 들어가지 않도록 라우팅한다.

### 4.3 최종 라운드 처리

- `MAX_ROUNDS`를 `RoundClearCoroutine`에서 검사한다.
- `clearedRound == MAX_ROUNDS` 이면 보상 투표와 스테이지 패널 오픈을 모두 건너뛰고 `OnGameClear?.Invoke()`만 호출한다.
- `OnGameClear`는 `OnGameOver`와 짝을 이루는 신규 이벤트로 선언하되, 구독(승리 패널)은 추후 작업으로 남긴다.

### 4.4 패배 시 카드 제거 로직 삭제 + 죽은 코드 정리

- `HandleWaveResultReceived`의 Lose 분기에서 `RemoveCurrentRoundAugments()` 호출 제거.
- `RemoveCurrentRoundAugments()` 와 그것만 호출하던 `RebuildEffects()` 제거.
- 라운드 추적용 `_acquiredCardRounds` 리스트 제거(추가/초기화 지점 포함). 제거 로직이 사라지면 읽는 곳이 없다.
- `_acquiredCards`, `OnAcquiredCardsChanged`, `ClearAcquiredAugments`(게임오버 시 전체 초기화)는 유지.

## 5. 네이밍

| 구분 | 이름 |
|---|---|
| NetworkManager 송신 | `ServerRoundClear()`, `ServerRewardDone()` |
| NetworkManager 이벤트 | `OnRoundCleared`, `OnRewardDone` |
| 메시지 enum | `EInGameRpsMessage.RoundClear`, `EInGameRpsMessage.RewardDone` |
| InGameManager 핸들러 | `HandleRoundClear()`, `HandleRewardDone()` |
| InGameManager 코루틴 | `RoundClearCoroutine()` |
| 승리 훅 이벤트 | `OnGameClear` |

`Network.OnRoundCleared`(과거형, 호스트 선언)와 기존 로컬 `InGameManager.OnRoundClear`(UI용)는 의미가 구분된다.

## 6. 영향받는 파일

- `Assets/02. Scripts/Management/InGameManager.cs` — 흐름 재구성, 핸들러/코루틴 추가, 죽은 코드 제거.
- `Assets/02. Scripts/Management/NetworkManager.cs` — RPC 2개 + 이벤트 2개 + 수신 switch 2케이스.
- `Assets/02. Scripts/Util/Enums.cs` — `EInGameRpsMessage`에 값 2개 추가.

## 7. 엣지 케이스

- **라운드 1**: 카드 없이 시작(의도된 변화).
- **패배 후 재도전**: 라운드 클리어 보상 카드는 유지.
- **게임오버(라이프 0)**: 기존대로 전체 초기화(`ClearAcquiredAugments`) 후 스테이지 패널로 복귀.
- **카드 덱 고갈**: 제시 카드가 0장이면 `HostSingleVoteCoroutine`이 즉시 빠져나오고, 호스트는 그래도 `ServerRewardDone()`을 호출해 클라가 멈추지 않게 한다.
- **최종 라운드**: 보상/스테이지 없이 `OnGameClear`만 호출.

## 8. 검증

- 라운드 1을 카드 없이 시작하는지.
- 라운드 클리어 시 KO → 카드 보상 → 스테이지 선택 순서로 진행되는지.
- 패배(라이프 남음) 후 보유 카드가 유지되는지.
- 호스트/클라가 같은 타이밍에 같은 화면(KO/보상/스테이지)을 보는지.
- 최종 라운드 클리어 시 보상 없이 `OnGameClear`가 호출되는지(로그 등으로 확인).
- 컴파일 에러/경고 없음(`read_console`).
