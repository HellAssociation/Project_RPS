# 카드 보상 라운드 클리어 이동 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 카드 투표(축복/저주)를 라운드 시작에서 라운드 클리어 보상으로 옮기고, 호스트가 클리어를 브로드캐스트해 전 피어가 같은 화면을 동기화한다.

**Architecture:** 호스트가 라운드 클리어 시 신규 RPC(`ServerRoundClear`)를 송신 → 전 피어가 `RoundClearCoroutine`(KO → 카드 보상 투표 → 스테이지 선택)을 동기 실행. 보상 종료는 `ServerRewardDone`로 통지해 스테이지 패널 전환을 맞춘다. 최종 라운드는 보상 없이 `OnGameClear` 훅만 호출한다.

**Tech Stack:** Unity (C#), Photon Fusion 네트워킹(`NetworkManager` 커스텀 RPS 페이로드), DOTween, 코루틴 기반 흐름.

## Global Constraints

- 주석은 기본적으로 작성하지 않는다. 꼭 필요하면 영어 한 줄로만(프로젝트 `CLAUDE.md`).
- 신규 함수/이벤트 이름은 짧게 유지한다(사용자 요청).
- 경로는 `Assets/` 기준, 슬래시(`/`) 사용.
- 자동화 테스트 하니스가 없는 런타임/네트워킹/UI 흐름이므로, 각 작업의 검증은 **컴파일 통과(`read_console`)** + **플레이 모드 수동 확인**으로 한다. 가짜 단위 테스트를 만들지 않는다.
- 컴파일 확인: 스크립트 수정 후 `editor_state` 리소스의 `isCompiling`이 false가 될 때까지 대기 → `read_console`로 에러/경고 0 확인.

---

### Task 1: 네트워크 신호 추가 (Enums + NetworkManager)

호스트가 라운드 클리어/보상 종료를 알리는 RPC 2개와 이벤트 2개를 추가한다. 이 시점엔 아직 구독자가 없어 동작 변화는 없고, 컴파일만 통과하면 된다.

**Files:**
- Modify: `Assets/02. Scripts/Util/Enums.cs:156-165`
- Modify: `Assets/02. Scripts/Management/NetworkManager.cs` (이벤트 선언 `620-626`, 메서드 추가 `~815` 부근, 수신 switch `991-994` 부근)

**Interfaces:**
- Produces:
  - `NetworkManager.ServerRoundClear()` — 호스트만 동작. 클라에 `RoundClear` 페이로드 송신 + 로컬 `OnRoundCleared` 발생.
  - `NetworkManager.ServerRewardDone()` — 호스트만 동작. 클라에 `RewardDone` 페이로드 송신 + 로컬 `OnRewardDone` 발생.
  - `event Action NetworkManager.OnRoundCleared`
  - `event Action NetworkManager.OnRewardDone`
  - `EInGameRpsMessage.RoundClear = 8`, `EInGameRpsMessage.RewardDone = 9`

- [ ] **Step 1: enum 값 2개 추가**

`Assets/02. Scripts/Util/Enums.cs`의 `EInGameRpsMessage`를 다음으로 교체:

```csharp
    public enum EInGameRpsMessage : byte
    {
        StartRound = 1,
        RoundResult = 2,
        StageSelected = 3,
        CardsApplied = 4,
        CardOffer = 5,
        CardVoteResult = 6,
        BossIntro = 7,
        RoundClear = 8,
        RewardDone = 9,
    }
```

- [ ] **Step 2: NetworkManager 이벤트 2개 추가**

`Assets/02. Scripts/Management/NetworkManager.cs`의 `OnBossIntro` 선언(626줄 부근) 바로 아래에 추가:

```csharp
    public event Action OnRoundCleared;
    public event Action OnRewardDone;
```

- [ ] **Step 3: 송신 메서드 2개 추가**

`ServerBroadcastBossIntro`(804-815줄) 바로 아래에 추가:

```csharp
    /// <summary>Host announces a round (10-wave) clear.</summary>
    public void ServerRoundClear()
    {
        if (!IsServerHost) return;

        byte[] payload = new PayloadWriter()
            .WriteByte((byte)EInGameRpsMessage.RoundClear)
            .ToArray();

        BroadcastInGamePayload(payload);
        OnRoundCleared?.Invoke();
    }

    /// <summary>Host signals the clear reward vote is finished.</summary>
    public void ServerRewardDone()
    {
        if (!IsServerHost) return;

        byte[] payload = new PayloadWriter()
            .WriteByte((byte)EInGameRpsMessage.RewardDone)
            .ToArray();

        BroadcastInGamePayload(payload);
        OnRewardDone?.Invoke();
    }
```

- [ ] **Step 4: 수신 switch 케이스 2개 추가**

`HandleInGameRpsData`의 switch에서 `BossIntro` 케이스(991-994줄) 바로 아래에 추가:

```csharp
            case EInGameRpsMessage.RoundClear:
                OnRoundCleared?.Invoke();
                break;

            case EInGameRpsMessage.RewardDone:
                OnRewardDone?.Invoke();
                break;
```

- [ ] **Step 5: 컴파일 확인**

Unity로 전환되어 도메인 리로드가 끝날 때까지 대기 후 콘솔 확인.
- `editor_state` 리소스: `isCompiling == false` 확인
- `read_console`(types: error, warning): InGameManager/NetworkManager/Enums 관련 에러 0
- 기대: 에러/경고 없음 (이벤트는 아직 미사용이지만 C# 이벤트는 미사용 경고 없음)

- [ ] **Step 6: 커밋**

```bash
git add "Assets/02. Scripts/Util/Enums.cs" "Assets/02. Scripts/Management/NetworkManager.cs"
git commit -m "[Add] : 라운드 클리어/보상 종료 네트워크 신호"
```

---

### Task 2: 패배 시 카드 제거 로직 + 죽은 코드 삭제 (InGameManager)

보상은 되돌릴 일이 없으므로, 패배(라이프 남음) 시 이번 라운드 카드를 제거하던 로직과 그 전용 코드를 삭제한다. 독립적인 변경이며 보상 이동(Task 3) 전에 깔끔히 정리한다.

**Files:**
- Modify: `Assets/02. Scripts/Management/InGameManager.cs` (필드 `30`, `BeginInGameSetup` `146-147`, Lose 분기 `347-350`, `HandleCardsApplied` `535`, `RemoveCurrentRoundAugments` `542-559`, `RebuildEffects` `561-569`, `ClearAcquiredAugments` `573-574`)

**Interfaces:**
- Consumes: 없음
- Produces: `_acquiredCardRounds` / `RemoveCurrentRoundAugments` / `RebuildEffects` 제거됨(이후 작업에서 참조 금지). `_acquiredCards`, `OnAcquiredCardsChanged`, `ClearAcquiredAugments`는 유지.

- [ ] **Step 1: Lose 분기에서 제거 호출 삭제**

`HandleWaveResultReceived`의 Lose 분기(339-351줄)를 다음으로 교체:

```csharp
            case EOutcome.Lose:
                _run.Lives -= ENEMY_DAMAGE;
                OnLivesChanged?.Invoke(_run.Lives);
                if (_run.Lives <= 0)
                {
                    Phase = EInGamePhase.GameOver;
                    isGameOver = true;
                }
                break;
```

- [ ] **Step 2: `_acquiredCardRounds` 필드 제거**

29-30줄에서 `_acquiredCardRounds` 선언 줄을 삭제:

```csharp
    readonly List<CardRef> _acquiredCards = new();
```
(즉 `readonly List<int> _acquiredCardRounds = new();` 한 줄 삭제)

- [ ] **Step 3: `BeginInGameSetup`에서 참조 제거**

146-147줄을 다음으로 교체:

```csharp
        _acquiredCards.Clear();
```
(`_acquiredCardRounds.Clear();` 줄 삭제)

- [ ] **Step 4: `HandleCardsApplied`에서 참조 제거**

`HandleCardsApplied`의 루프(531-536줄)를 다음으로 교체:

```csharp
        for (int i = 0; i < cards.Count; i++)
        {
            CardSystem.Apply(_run, cards[i], _effects);
            _acquiredCards.Add(cards[i]);
        }
```
(`_acquiredCardRounds.Add(_run.CurrentRound);` 줄 삭제)

- [ ] **Step 5: `RemoveCurrentRoundAugments`와 `RebuildEffects` 메서드 삭제**

541-569줄의 두 메서드를 통째로 삭제:

```csharp
    // Removes the augments chosen during the current round and rebuilds the remaining effects.
    void RemoveCurrentRoundAugments() { ... }

    void RebuildEffects() { ... }
```

- [ ] **Step 6: `ClearAcquiredAugments`에서 참조 제거**

`ClearAcquiredAugments`(571-576줄)를 다음으로 교체:

```csharp
    void ClearAcquiredAugments()
    {
        _acquiredCards.Clear();
        OnAcquiredCardsChanged?.Invoke();
    }
```
(`_acquiredCardRounds.Clear();` 줄 삭제)

- [ ] **Step 7: 컴파일 확인**

`editor_state`(`isCompiling == false`) → `read_console`(error/warning).
- 기대: 에러/경고 없음. `_acquiredCardRounds` / `RemoveCurrentRoundAugments` / `RebuildEffects`에 대한 미해결 참조가 없어야 함.

- [ ] **Step 8: 커밋**

```bash
git add "Assets/02. Scripts/Management/InGameManager.cs"
git commit -m "[Mod] : 패배 시 카드 제거 로직 삭제"
```

---

### Task 3: 카드 투표를 호스트 주도 라운드 클리어 보상으로 이동 (InGameManager)

라운드 시작 투표를 제거하고, 클리어 시 호스트가 `ServerRoundClear`를 송신해 전 피어가 `RoundClearCoroutine`(KO → 보상 투표 → 스테이지 선택)을 동기 실행하도록 재구성한다. 최종 라운드는 보상 없이 `OnGameClear`만 호출한다.

**Files:**
- Modify: `Assets/02. Scripts/Management/InGameManager.cs` (이벤트 선언 `82-84`, 필드 `46-49`, `OnEnable/OnDisable` `86-108`, `HostPrepareAssignmentsCoroutine` `203-211`, `HandleWaveResultReceived` `312-357`, `BetweenWavesCoroutine` `359-437`, 신규 핸들러/코루틴 추가)

**Interfaces:**
- Consumes: `NetworkManager.ServerRoundClear()`, `NetworkManager.ServerRewardDone()`, `NetworkManager.OnRoundCleared`, `NetworkManager.OnRewardDone` (Task 1).
- Produces: `event Action InGameManager.OnGameClear` (승리 패널 연결용, 현재 미구독).

- [ ] **Step 1: `OnGameClear` 이벤트 선언**

82-84줄의 이벤트 블록에 `OnGameClear` 추가(`OnGameOver` 아래):

```csharp
    public event Action<int> OnRoundClear;
    public event Action OnGameOver;
    public event Action OnGameClear;
    public event Action OnAcquiredCardsChanged;
```

- [ ] **Step 2: `_rewardDone` 필드 추가**

46-49줄의 코루틴 필드 블록에 추가:

```csharp
    Coroutine _waveCoroutine;
    Coroutine _hostSetupCoroutine;
    Coroutine _betweenWaveCoroutine;
    bool _hostSetupStarted;
    bool _rewardDone;
```

- [ ] **Step 3: 네트워크 이벤트 구독/해제**

`OnEnable`의 `Network.OnCardsApplied += HandleCardsApplied;`(95줄) 아래에 추가:

```csharp
        Network.OnRoundCleared += HandleRoundClear;
        Network.OnRewardDone += HandleRewardDone;
```

`OnDisable`의 `Network.OnCardsApplied -= HandleCardsApplied;`(107줄) 아래에 추가:

```csharp
        Network.OnRoundCleared -= HandleRoundClear;
        Network.OnRewardDone -= HandleRewardDone;
```

- [ ] **Step 4: 라운드 시작 투표 제거**

`HostPrepareAssignmentsCoroutine`(203-211줄)를 다음으로 교체:

```csharp
    // Host ritual shared by initial setup and post-round-clear: ensure objects -> assign fingers -> ready.
    IEnumerator HostPrepareAssignmentsCoroutine()
    {
        yield return Players.ServerEnsurePlayerObjectsCoroutine();
        Network.ServerInitializeFingerAssignments();
        yield return null;
        ApplyAssignmentsReady();
    }
```
(`yield return HostCardVoteSequenceCoroutine();` 제거. `HostCardVoteSequenceCoroutine` / `HostSingleVoteCoroutine`는 보상에서 재사용하므로 유지.)

- [ ] **Step 5: 클리어 경로를 호스트 주도로 라우팅**

`HandleWaveResultReceived`(312-357줄)를 다음으로 교체:

```csharp
    void HandleWaveResultReceived(EHandPosition handPosition)
    {
        LastHandPosition = handPosition;

        EOutcome outcome = OutcomeResolver.Resolve(handPosition, _enemyHandPosition);
        Phase = EInGamePhase.WaveComplete;
        OnOutcomeDetermined?.Invoke(outcome);
        OnWaveJudged?.Invoke(handPosition);

        bool isRoundClear = false;
        bool isGameOver = false;
        bool enemyKilled = false;

        switch (outcome)
        {
            case EOutcome.Win:
                _run.EnemyHp -= _run.PlayerDamage;
                OnEnemyHpChanged?.Invoke(Mathf.Max(_run.EnemyHp, 0), _run.EnemyMaxHp);
                if (_run.EnemyHp <= 0)
                {
                    enemyKilled = true;
                    _run.WonWavesInRound++;
                }
                if (_run.WonWavesInRound >= WAVES_PER_ROUND)
                    isRoundClear = true;
                break;

            case EOutcome.Lose:
                _run.Lives -= ENEMY_DAMAGE;
                OnLivesChanged?.Invoke(_run.Lives);
                if (_run.Lives <= 0)
                {
                    Phase = EInGamePhase.GameOver;
                    isGameOver = true;
                }
                break;
        }

        if (isRoundClear)
        {
            if (Network.IsServerHost)
                Network.ServerRoundClear();
            return;
        }

        if (_betweenWaveCoroutine != null)
            StopCoroutine(_betweenWaveCoroutine);
        _betweenWaveCoroutine = StartCoroutine(BetweenWavesCoroutine(isGameOver, enemyKilled));
    }
```

- [ ] **Step 6: `BetweenWavesCoroutine`에서 클리어 분기 제거**

`BetweenWavesCoroutine`(359-437줄)를 다음으로 교체(시그니처에서 `isRoundClear` 제거, isRoundClear 분기 삭제, 보스 refill 조건에서 `!isRoundClear` 제거):

```csharp
    IEnumerator BetweenWavesCoroutine(bool isGameOver, bool enemyKilled)
    {
        yield return WAIT_OUTCOME;
        OnWaveResultShown?.Invoke();

        if (enemyKilled && _versusPanel != null)
            yield return _versusPanel.PlayKoSequence();

        // Boss (every round's 10th enemy) appears the moment the 9th enemy is killed and the next HP bar fills.
        bool bossAppearing = false;

        if (!isGameOver && _run.EnemyHp <= 0)
        {
            bossAppearing = _run.WonWavesInRound == WAVES_PER_ROUND - 1;
            _run.EnemyHp = _run.EnemyMaxHp;
            OnEnemyHpChanged?.Invoke(_run.EnemyHp, _run.EnemyMaxHp);
        }

        if (isGameOver)
        {
            Input.ResetFingerState();
            OnLocalFingerExtendedChanged?.Invoke(false);
            OnLocalFingerMaskChanged?.Invoke(EFingerType.None);
            OnGameOver?.Invoke();

            _run.Reset();
            _effects.Clear();
            _lastEffectRound = 0;
            ClearAcquiredAugments();
            _hostSetupStarted = false;
            Phase = EInGamePhase.WaitingForRoundSelect;
            OnLivesChanged?.Invoke(_run.Lives);
            OnCurrentRoundChanged?.Invoke(_run.CurrentRound);

            if (_stagePanel != null) _stagePanel.OpenPanel();
            _betweenWaveCoroutine = null;
            yield break;
        }

        if (!Network.IsServerHost)
        {
            _betweenWaveCoroutine = null;
            yield break;
        }

        yield return WAIT_SLIDE;

        if (bossAppearing)
        {
            Phase = EInGamePhase.BossIntro;
            ApplyBossRule(_run.CurrentRound);
            Network.ServerBroadcastBossIntro(_run.CurrentRound);
            yield return WAIT_BOSS_INTRO;
        }

        yield return WaitVersusThenStartRound();
        _betweenWaveCoroutine = null;
    }
```

- [ ] **Step 7: 클리어 핸들러 + 코루틴 추가**

`BetweenWavesCoroutine` 바로 아래에 추가:

```csharp
    void HandleRoundClear()
    {
        _rewardDone = false;
        if (_betweenWaveCoroutine != null)
            StopCoroutine(_betweenWaveCoroutine);
        _betweenWaveCoroutine = StartCoroutine(RoundClearCoroutine());
    }

    void HandleRewardDone()
    {
        _rewardDone = true;
    }

    IEnumerator RoundClearCoroutine()
    {
        yield return WAIT_OUTCOME;
        OnWaveResultShown?.Invoke();

        if (_versusPanel != null)
            yield return _versusPanel.PlayKoSequence();

        int clearedRound = _run.CurrentRound;
        if (clearedRound >= MAX_ROUNDS)
        {
            OnGameClear?.Invoke();
            _betweenWaveCoroutine = null;
            yield break;
        }

        _run.CurrentRound++;
        _run.WonWavesInRound = 0;
        OnCurrentRoundChanged?.Invoke(_run.CurrentRound);
        OnRoundClear?.Invoke(clearedRound);

        yield return WAIT_SLIDE;

        if (Network.IsServerHost)
        {
            yield return HostCardVoteSequenceCoroutine();
            Network.ServerRewardDone();
        }
        else
        {
            yield return new WaitUntil(() => _rewardDone);
        }

        Input.ResetFingerState();
        OnLocalFingerExtendedChanged?.Invoke(false);
        OnLocalFingerMaskChanged?.Invoke(EFingerType.None);

        Phase = EInGamePhase.WaitingForRoundSelect;
        _hostSetupStarted = false;
        if (_stagePanel != null) _stagePanel.OpenPanel();
        _betweenWaveCoroutine = null;
    }
```

- [ ] **Step 8: 컴파일 확인**

`editor_state`(`isCompiling == false`) → `read_console`(error/warning).
- 기대: 에러/경고 없음. `HostCardVoteSequenceCoroutine`/`HostSingleVoteCoroutine`는 `RoundClearCoroutine`에서 사용되므로 미사용 경고 없음.

- [ ] **Step 9: 플레이 모드 수동 검증**

`Game_PvE_1v1` 씬을 플레이하고 확인:
- 라운드 1이 카드 투표 없이 바로 시작되는가.
- 라운드(10웨이브) 클리어 시 KO 연출 → 카드 보상 투표 → 스테이지 선택 순서로 진행되는가.
- 패배(라이프 남음) 후에도 보유 카드가 그대로 유지되는가.
- (가능 시) 최종 라운드 클리어에서 보상/스테이지 없이 `OnGameClear`가 호출되는가(임시 로그 또는 디버거로 확인).

- [ ] **Step 10: 커밋**

```bash
git add "Assets/02. Scripts/Management/InGameManager.cs"
git commit -m "[Mod] : 카드 보상을 라운드 클리어 시점으로 이동"
```

---

## Self-Review

**1. Spec coverage**
- 4.1 라운드 시작 투표 제거 → Task 3 Step 4 ✓
- 4.2 호스트 주도 클리어 + 보상(RPC 2개) → Task 1 + Task 3 Step 5/7 ✓
- 4.3 최종 라운드 보상 없음 + `OnGameClear` → Task 3 Step 1/7(`clearedRound >= MAX_ROUNDS`) ✓
- 4.4 패배 제거 로직 + 죽은 코드 정리 → Task 2 전체 ✓
- 엣지(덱 고갈 시에도 `ServerRewardDone` 호출): `RoundClearCoroutine`에서 호스트는 `HostCardVoteSequenceCoroutine` 종료 직후 무조건 `ServerRewardDone()` 호출 → 충족 ✓

**2. Placeholder scan:** 모든 코드 스텝에 실제 코드 포함, TBD/TODO 없음 ✓

**3. Type consistency:** `ServerRoundClear`/`ServerRewardDone`/`OnRoundCleared`/`OnRewardDone`/`OnGameClear`/`HandleRoundClear`/`HandleRewardDone`/`RoundClearCoroutine`/`_rewardDone` 이름이 전 작업에서 일치 ✓. `EInGameRpsMessage.RoundClear`(=8)/`RewardDone`(=9) 송신·수신 일치 ✓.
