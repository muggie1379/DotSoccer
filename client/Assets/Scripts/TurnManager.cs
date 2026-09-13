using UnityEngine;
using UnityEngine.InputSystem;

public class TurnManager : MonoBehaviour
{
    public const float MinTurnInterval = 0.5f;
    public const float MaxTurnInterval = 10f;

    [Tooltip("각 턴의 길이(초). 추후 사용자가 설정 가능하도록 변수로 분리. (0.5 ~ 10초)")]
    [Range(MinTurnInterval, MaxTurnInterval)]
    public float turnInterval = 1f;

    [Tooltip("전반/후반 각각의 길이(초)")]
    public float halfDuration = 30f;

    [Tooltip("경기 시작 전 / 후반 시작 전 카운트다운 길이(초)")]
    public float countdownDuration = 3f;

    [Tooltip("전반 종료, 경기 종료 배너를 띄워두는 시간(초)")]
    public float bannerDuration = 1.5f;

    public int score1 = 0;
    public int score2 = 0;

    [Tooltip("켜면 화면 좌우에 상대 플레이어의 입력 방향도 함께 표시됨 (기본은 자기 것만 보임)")]
    public bool showOpponentInput = false;

    [Tooltip("키를 뗀 뒤 이 시간(초)이 지나면 그 턴에서는 입력이 없었던 것으로 간주")]
    public float releaseTolerance = 0.2f;

    enum Phase
    {
        PreMatchCountdown,
        FirstHalfPlaying,
        HalfEndBanner,
        SecondHalfCountdown,
        SecondHalfPlaying,
        KickoffCountdown,
        MatchEnded
    }

    Phase phase = Phase.PreMatchCountdown;
    Phase kickoffReturnPhase;
    float phaseTimer;
    string bannerText = "경기 시작";

    GridManager gridManager;
    AudioSource audioSource;
    AudioClip buzzerClip;

    Vector2Int player1Pos;
    Vector2Int player2Pos;
    int ballOwner = 1; // 1 = 1P 소유, 2 = 2P 소유

    float turnTimer = 0f;
    float halfTimer = 0f;

    GUIStyle hudStyle;
    GUIStyle bannerStyle;
    GUIStyle countdownStyle;
    GUIStyle inputHudStyle;

    // 키별 이번 턴 동안의 누적 보유 시간과, 마지막으로 눌려있던 시각
    float holdW, holdA, holdS, holdD;
    float holdUp, holdDown, holdLeft, holdRight;
    float lastSeenW = -999f, lastSeenA = -999f, lastSeenS = -999f, lastSeenD = -999f;
    float lastSeenUp = -999f, lastSeenDown = -999f, lastSeenLeft = -999f, lastSeenRight = -999f;

    void Start()
    {
        gridManager = GetComponent<GridManager>();
        player1Pos = gridManager.player1Spawn;
        player2Pos = gridManager.player2Spawn;
        ballOwner = gridManager.currentHalf == 1 ? 1 : 2;

        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        buzzerClip = CreateBuzzerClip();

        phase = Phase.PreMatchCountdown;
        phaseTimer = countdownDuration;
        bannerText = "경기 시작";
    }

    void OnValidate()
    {
        turnInterval = Mathf.Clamp(turnInterval, MinTurnInterval, MaxTurnInterval);
    }

    // 추후 사용자 설정 UI에서 턴 길이를 바꿀 때 호출 (0.5~10초로 자동 제한)
    public void SetTurnInterval(float seconds)
    {
        turnInterval = Mathf.Clamp(seconds, MinTurnInterval, MaxTurnInterval);
    }

    void Update()
    {
        switch (phase)
        {
            case Phase.PreMatchCountdown:
                UpdateCountdown(Phase.FirstHalfPlaying, true);
                break;

            case Phase.FirstHalfPlaying:
                UpdatePlaying();
                break;

            case Phase.HalfEndBanner:
                UpdateBanner(Phase.SecondHalfCountdown, countdownDuration, "후반 시작");
                break;

            case Phase.SecondHalfCountdown:
                UpdateCountdown(Phase.SecondHalfPlaying, true);
                break;

            case Phase.SecondHalfPlaying:
                UpdatePlaying();
                break;

            case Phase.KickoffCountdown:
                UpdateCountdown(kickoffReturnPhase, false);
                break;

            case Phase.MatchEnded:
                break;
        }
    }

    void UpdateCountdown(Phase nextPhase, bool resetHalfTimer)
    {
        phaseTimer -= Time.deltaTime;
        if (phaseTimer <= 0f)
        {
            phase = nextPhase;
            ResetTurnAccumulators();
            if (resetHalfTimer) halfTimer = 0f;
        }
    }

    void UpdateBanner(Phase nextPhase, float nextPhaseTimer, string nextBannerText)
    {
        phaseTimer -= Time.deltaTime;
        if (phaseTimer <= 0f)
        {
            // 전반 종료 배너가 끝나면 후반으로 전환 + 공수교대
            gridManager.StartNextHalf();
            player1Pos = gridManager.player1Spawn;
            player2Pos = gridManager.player2Spawn;
            ballOwner = gridManager.currentHalf == 1 ? 1 : 2;
            ApplyVisualPositions();

            phase = nextPhase;
            phaseTimer = nextPhaseTimer;
            bannerText = nextBannerText;
        }
    }

    void UpdatePlaying()
    {
        AccumulateInput();

        turnTimer += Time.deltaTime;
        halfTimer += Time.deltaTime;

        if (turnTimer >= turnInterval)
        {
            ResolveTurn();
            turnTimer = 0f;
        }

        if (halfTimer >= halfDuration)
        {
            EndHalf();
        }
    }

    void ResetTurnAccumulators()
    {
        turnTimer = 0f;
        holdW = 0f; holdA = 0f; holdS = 0f; holdD = 0f;
        holdUp = 0f; holdDown = 0f; holdLeft = 0f; holdRight = 0f;
    }

    void EndHalf()
    {
        halfTimer = 0f;
        ResetTurnAccumulators();

        if (gridManager.currentHalf >= 2)
        {
            phase = Phase.MatchEnded;
            bannerText = string.Format("경기 종료\n최종 스코어  1P {0} : {1} 2P", score1, score2);
            return;
        }

        phase = Phase.HalfEndBanner;
        phaseTimer = bannerDuration;
        bannerText = "전반 종료";
    }

    void OnTouchdown(int scorer)
    {
        if (scorer == 1) score1++;
        else score2++;

        StartKickoff(scorer);
        bannerText = string.Format("{0}P 득점! 현재 점수 {1}:{2}", scorer, score1, score2);
    }

    void OnOwnGoal(int culprit)
    {
        int scorer = culprit == 1 ? 2 : 1;
        if (scorer == 1) score1++;
        else score2++;

        StartKickoff(scorer);
        bannerText = string.Format("{0}P 자책골! 현재 점수 {1}:{2}", culprit, score1, score2);
    }

    // 득점 팀 기준으로 공수교대 + 스폰 복귀 + 킥오프 카운트다운 시작
    void StartKickoff(int scorer)
    {
        // 실점한 팀이 공을 소유하고 양쪽 모두 스폰 위치로 복귀
        ballOwner = scorer == 1 ? 2 : 1;
        player1Pos = gridManager.player1Spawn;
        player2Pos = gridManager.player2Spawn;

        // 킥오프 전 3초 카운트다운 (하프 타이머는 계속 흐름)
        kickoffReturnPhase = gridManager.currentHalf == 1 ? Phase.FirstHalfPlaying : Phase.SecondHalfPlaying;
        phase = Phase.KickoffCountdown;
        phaseTimer = countdownDuration;
    }

    void AccumulateInput()
    {
        Keyboard kb = Keyboard.current;
        if (kb == null) return;

        float dt = Time.deltaTime;
        float t = Time.time;

        if (kb.wKey.isPressed) { holdW += dt; lastSeenW = t; }
        if (kb.aKey.isPressed) { holdA += dt; lastSeenA = t; }
        if (kb.sKey.isPressed) { holdS += dt; lastSeenS = t; }
        if (kb.dKey.isPressed) { holdD += dt; lastSeenD = t; }

        if (kb.upArrowKey.isPressed) { holdUp += dt; lastSeenUp = t; }
        if (kb.downArrowKey.isPressed) { holdDown += dt; lastSeenDown = t; }
        if (kb.leftArrowKey.isPressed) { holdLeft += dt; lastSeenLeft = t; }
        if (kb.rightArrowKey.isPressed) { holdRight += dt; lastSeenRight = t; }
    }

    // 키를 뗀 지 releaseTolerance를 넘었으면 이번 턴에 쌓인 양은 무시(0)하고, 아니면 누적된 보유 시간을 그대로 반영
    float EffectiveHold(float hold, float lastSeen)
    {
        return (Time.time - lastSeen <= releaseTolerance) ? hold : 0f;
    }

    int SignZero(float v)
    {
        if (v > 0f) return 1;
        if (v < 0f) return -1;
        return 0;
    }

    Vector2Int FinalizeDirection1()
    {
        float x = EffectiveHold(holdD, lastSeenD) - EffectiveHold(holdA, lastSeenA);
        float y = EffectiveHold(holdW, lastSeenW) - EffectiveHold(holdS, lastSeenS);
        return new Vector2Int(SignZero(x), SignZero(y));
    }

    Vector2Int FinalizeDirection2()
    {
        float x = EffectiveHold(holdRight, lastSeenRight) - EffectiveHold(holdLeft, lastSeenLeft);
        float y = EffectiveHold(holdUp, lastSeenUp) - EffectiveHold(holdDown, lastSeenDown);
        return new Vector2Int(SignZero(x), SignZero(y));
    }

    Vector2Int GetInstantDirection1()
    {
        Keyboard kb = Keyboard.current;
        if (kb == null) return Vector2Int.zero;
        int x = 0, y = 0;
        if (kb.dKey.isPressed) x += 1;
        if (kb.aKey.isPressed) x -= 1;
        if (kb.wKey.isPressed) y += 1;
        if (kb.sKey.isPressed) y -= 1;
        return new Vector2Int(x, y);
    }

    Vector2Int GetInstantDirection2()
    {
        Keyboard kb = Keyboard.current;
        if (kb == null) return Vector2Int.zero;
        int x = 0, y = 0;
        if (kb.rightArrowKey.isPressed) x += 1;
        if (kb.leftArrowKey.isPressed) x -= 1;
        if (kb.upArrowKey.isPressed) y += 1;
        if (kb.downArrowKey.isPressed) y -= 1;
        return new Vector2Int(x, y);
    }

    string DirectionToArrow(Vector2Int dir)
    {
        if (dir.x == 0 && dir.y == 0) return "·";
        if (dir.x == 0 && dir.y > 0) return "↑";
        if (dir.x == 0 && dir.y < 0) return "↓";
        if (dir.x > 0 && dir.y == 0) return "→";
        if (dir.x < 0 && dir.y == 0) return "←";
        if (dir.x > 0 && dir.y > 0) return "↗";
        if (dir.x > 0 && dir.y < 0) return "↘";
        if (dir.x < 0 && dir.y > 0) return "↖";
        return "↙";
    }

    void ResolveTurn()
    {
        Vector2Int dir1 = FinalizeDirection1();
        Vector2Int dir2 = FinalizeDirection2();
        ResetTurnAccumulators();

        player1Pos = ClampToGrid(player1Pos + dir1);
        player2Pos = ClampToGrid(player2Pos + dir2);

        // 공을 가진 플레이어의 칸에 상대가 진입하면 소유권 전환 (스틸)
        if (player1Pos == player2Pos)
        {
            ballOwner = ballOwner == 1 ? 2 : 1;
        }

        // 터치다운/자책골 판정: 공을 가진 플레이어가 골대(골대 높이 범위)에 도달
        if (ballOwner == 1)
        {
            if (player1Pos.x == gridManager.gridWidth - 1 && gridManager.IsInGoalRow(player1Pos.y))
            {
                OnTouchdown(1); // 1P가 상대편 골대에 도달
            }
            else if (player1Pos.x == 0 && gridManager.IsInGoalRow(player1Pos.y))
            {
                OnOwnGoal(1); // 1P가 자기 골대로 들어감
            }
        }
        else
        {
            if (player2Pos.x == 0 && gridManager.IsInGoalRow(player2Pos.y))
            {
                OnTouchdown(2); // 2P가 상대편 골대에 도달
            }
            else if (player2Pos.x == gridManager.gridWidth - 1 && gridManager.IsInGoalRow(player2Pos.y))
            {
                OnOwnGoal(2); // 2P가 자기 골대로 들어감
            }
        }

        ApplyVisualPositions();
        PlayBuzzer();
    }

    void PlayBuzzer()
    {
        if (audioSource != null && buzzerClip != null)
        {
            audioSource.PlayOneShot(buzzerClip);
        }
    }

    AudioClip CreateBuzzerClip()
    {
        const int sampleRate = 44100;
        const float duration = 0.12f;
        const float frequency = 880f;

        int sampleCount = Mathf.RoundToInt(sampleRate * duration);
        AudioClip clip = AudioClip.Create("Buzzer", sampleCount, 1, sampleRate, false);

        float[] samples = new float[sampleCount];
        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / sampleRate;
            float envelope = Mathf.Exp(-8f * t);
            samples[i] = Mathf.Sin(2f * Mathf.PI * frequency * t) * envelope;
        }
        clip.SetData(samples, 0);
        return clip;
    }

    Vector2Int ClampToGrid(Vector2Int pos)
    {
        int x = Mathf.Clamp(pos.x, 0, gridManager.gridWidth - 1);
        int y = Mathf.Clamp(pos.y, 0, gridManager.gridHeight - 1);
        return new Vector2Int(x, y);
    }

    void ApplyVisualPositions()
    {
        gridManager.MovePlayer(1, player1Pos);
        gridManager.MovePlayer(2, player2Pos);
        gridManager.MoveBall(ballOwner == 1 ? player1Pos : player2Pos);
    }

    void OnGUI()
    {
        if (hudStyle == null)
        {
            hudStyle = new GUIStyle(GUI.skin.label);
            hudStyle.fontSize = 24;

            bannerStyle = new GUIStyle(GUI.skin.label);
            bannerStyle.fontSize = 48;
            bannerStyle.alignment = TextAnchor.MiddleCenter;

            countdownStyle = new GUIStyle(GUI.skin.label);
            countdownStyle.fontSize = 96;
            countdownStyle.alignment = TextAnchor.MiddleCenter;

            inputHudStyle = new GUIStyle(GUI.skin.label);
            inputHudStyle.fontSize = 22;
        }

        DrawInputHud();

        string halfLabel = phase == Phase.MatchEnded ? "경기 종료" :
            (gridManager.currentHalf == 1 ? "전반" : "후반");
        float remaining = (phase == Phase.FirstHalfPlaying || phase == Phase.SecondHalfPlaying || phase == Phase.KickoffCountdown)
            ? Mathf.Max(0f, halfDuration - halfTimer) : halfDuration;

        string hudText = string.Format("{0}   1P {1} : {2} 2P   남은 시간 {3:0.0}s",
            halfLabel, score1, score2, remaining);
        GUI.Label(new Rect(20, 20, 800, 40), hudText, hudStyle);

        switch (phase)
        {
            case Phase.PreMatchCountdown:
            case Phase.SecondHalfCountdown:
            case Phase.KickoffCountdown:
                GUI.Label(new Rect(0, Screen.height / 2f - 140, Screen.width, 60), bannerText, bannerStyle);
                GUI.Label(new Rect(0, Screen.height / 2f - 70, Screen.width, 140), Mathf.CeilToInt(phaseTimer).ToString(), countdownStyle);
                break;

            case Phase.HalfEndBanner:
                GUI.Label(new Rect(0, Screen.height / 2f - 30, Screen.width, 60), bannerText, bannerStyle);
                break;

            case Phase.MatchEnded:
                GUI.Label(new Rect(0, Screen.height / 2f - 60, Screen.width, 120), bannerText, bannerStyle);
                break;
        }
    }

    void DrawInputHud()
    {
        Vector2Int instant1 = GetInstantDirection1();
        Vector2Int instant2 = GetInstantDirection2();
        Vector2Int next1 = FinalizeDirection1();
        Vector2Int next2 = FinalizeDirection2();

        // 좌측: 1P 자신의 입력은 항상 보임. 옵션을 켜면 2P(상대) 정보도 같이 보임
        string leftText = string.Format("1P 입력 중: {0}   다음 턴: {1}", DirectionToArrow(instant1), DirectionToArrow(next1));
        if (showOpponentInput)
        {
            leftText += string.Format("\n(상대) 2P 입력 중: {0}   다음 턴: {1}", DirectionToArrow(instant2), DirectionToArrow(next2));
        }
        GUI.Label(new Rect(20, 70, 500, 80), leftText, inputHudStyle);

        // 우측: 2P 자신의 입력은 항상 보임. 옵션을 켜면 1P(상대) 정보도 같이 보임
        string rightText = string.Format("2P 입력 중: {0}   다음 턴: {1}", DirectionToArrow(instant2), DirectionToArrow(next2));
        if (showOpponentInput)
        {
            rightText += string.Format("\n(상대) 1P 입력 중: {0}   다음 턴: {1}", DirectionToArrow(instant1), DirectionToArrow(next1));
        }

        GUIStyle rightStyle = new GUIStyle(inputHudStyle);
        rightStyle.alignment = TextAnchor.UpperRight;
        GUI.Label(new Rect(Screen.width - 520, 70, 500, 80), rightText, rightStyle);
    }
}
