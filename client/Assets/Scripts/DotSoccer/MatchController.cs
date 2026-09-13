using UnityEngine;
using UnityEngine.InputSystem;

namespace DotSoccer
{
    // Orchestrates one local hotseat match: owns the MatchState, drives the
    // turn/half/kickoff phase machine, reads keyboard input for both local
    // players, calls the pure TurnResolver each turn, and pushes the result
    // to GridView. When the game moves online, this class splits in two:
    // the phase machine + TurnResolver calls move to the server, and only
    // input capture + applying received state stays on the client.
    public class MatchController : MonoBehaviour
    {
        const float MinTurnInterval = 0.5f;
        const float MaxTurnInterval = 10f;

        [SerializeField] GameConfig config = new GameConfig();
        [SerializeField] GridView view;

        [Tooltip("각 턴의 길이(초). 0.5~10초")]
        [Range(MinTurnInterval, MaxTurnInterval)]
        public float turnInterval = 1f;

        [Tooltip("전반/후반 각각의 길이(초)")]
        public float halfDuration = 30f;

        [Tooltip("경기 시작 전 / 후반 시작 전 카운트다운 길이(초)")]
        public float countdownDuration = 3f;

        [Tooltip("전반 종료, 경기 종료 배너를 띄워두는 시간(초)")]
        public float bannerDuration = 1.5f;

        [Tooltip("키를 뗀 뒤 이 시간(초)이 지나면 그 턴에서는 입력이 없었던 것으로 간주")]
        public float releaseTolerance = 0.2f;

        [Tooltip("켜면 화면 좌우에 상대 플레이어의 입력 방향도 함께 표시됨")]
        public bool showOpponentInput = false;

        enum Phase
        {
            PreMatchCountdown,
            FirstHalfPlaying,
            HalfEndBanner,
            SecondHalfCountdown,
            SecondHalfPlaying,
            KickoffCountdown,
            MatchEnded,
        }

        readonly MatchState state = new MatchState();
        readonly DirectionalInputAccumulator input1 = new DirectionalInputAccumulator();
        readonly DirectionalInputAccumulator input2 = new DirectionalInputAccumulator();

        Phase phase = Phase.PreMatchCountdown;
        Phase kickoffReturnPhase;
        float phaseTimer;
        string bannerText = "경기 시작";

        float turnTimer;
        float halfTimer;

        AudioSource audioSource;
        AudioClip buzzerClip;

        GUIStyle hudStyle, bannerStyle, countdownStyle, inputHudStyle;

        void OnValidate()
        {
            turnInterval = Mathf.Clamp(turnInterval, MinTurnInterval, MaxTurnInterval);
        }

        void Start()
        {
            if (view == null) view = GetComponent<GridView>();

            state.Player1Pos = config.Player1Spawn;
            state.Player2Pos = config.Player2Spawn;
            state.BallOwner = state.CurrentHalf == 1 ? 1 : 2;

            view.Config = config;
            view.BuildField();
            view.SpawnEntities(state.Player1Pos, state.Player2Pos, state.BallPos);

            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            buzzerClip = CreateBuzzerClip();

            phase = Phase.PreMatchCountdown;
            phaseTimer = countdownDuration;
            bannerText = "경기 시작";
        }

        void Update()
        {
            switch (phase)
            {
                case Phase.PreMatchCountdown:
                    UpdateCountdown(Phase.FirstHalfPlaying, resetHalfTimer: true);
                    break;
                case Phase.FirstHalfPlaying:
                case Phase.SecondHalfPlaying:
                    UpdatePlaying();
                    break;
                case Phase.HalfEndBanner:
                    UpdateBanner();
                    break;
                case Phase.SecondHalfCountdown:
                    UpdateCountdown(Phase.SecondHalfPlaying, resetHalfTimer: true);
                    break;
                case Phase.KickoffCountdown:
                    UpdateCountdown(kickoffReturnPhase, resetHalfTimer: false);
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
                input1.Reset();
                input2.Reset();
                turnTimer = 0f;
                if (resetHalfTimer) halfTimer = 0f;
            }
        }

        void UpdateBanner()
        {
            phaseTimer -= Time.deltaTime;
            if (phaseTimer <= 0f)
            {
                StartSecondHalf();
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

        void StartSecondHalf()
        {
            state.CurrentHalf = 2;
            state.Player1Pos = config.Player1Spawn;
            state.Player2Pos = config.Player2Spawn;
            state.BallOwner = 2;
            view.ApplyPositions(state.Player1Pos, state.Player2Pos, state.BallPos);

            phase = Phase.SecondHalfCountdown;
            phaseTimer = countdownDuration;
            bannerText = "후반 시작";
        }

        void EndHalf()
        {
            halfTimer = 0f;
            turnTimer = 0f;

            if (state.CurrentHalf >= 2)
            {
                phase = Phase.MatchEnded;
                bannerText = $"경기 종료\n최종 스코어  1P {state.Score1} : {state.Score2} 2P";
                return;
            }

            phase = Phase.HalfEndBanner;
            phaseTimer = bannerDuration;
            bannerText = "전반 종료";
        }

        void AccumulateInput()
        {
            Keyboard kb = Keyboard.current;
            if (kb == null) return;

            float dt = Time.deltaTime;
            float now = Time.time;

            input1.Accumulate(kb.wKey.isPressed, kb.sKey.isPressed, kb.aKey.isPressed, kb.dKey.isPressed, dt, now);
            input2.Accumulate(kb.upArrowKey.isPressed, kb.downArrowKey.isPressed, kb.leftArrowKey.isPressed, kb.rightArrowKey.isPressed, dt, now);
        }

        void ResolveTurn()
        {
            Vector2Int dir1 = input1.FinalizeDirection(Time.time, releaseTolerance);
            Vector2Int dir2 = input2.FinalizeDirection(Time.time, releaseTolerance);
            input1.Reset();
            input2.Reset();

            TurnOutcome outcome = TurnResolver.Resolve(state, dir1, dir2, config);
            view.ApplyPositions(state.Player1Pos, state.Player2Pos, state.BallPos);
            PlayBuzzer();

            if (outcome.Goal == GoalEvent.Touchdown)
            {
                state.Score1 += outcome.GoalPlayer == 1 ? 1 : 0;
                state.Score2 += outcome.GoalPlayer == 2 ? 1 : 0;
                bannerText = $"{outcome.GoalPlayer}P 득점! 현재 점수 {state.Score1}:{state.Score2}";
                StartKickoff(scorer: outcome.GoalPlayer);
            }
            else if (outcome.Goal == GoalEvent.OwnGoal)
            {
                int scorer = outcome.GoalPlayer == 1 ? 2 : 1;
                state.Score1 += scorer == 1 ? 1 : 0;
                state.Score2 += scorer == 2 ? 1 : 0;
                bannerText = $"{outcome.GoalPlayer}P 자책골! 현재 점수 {state.Score1}:{state.Score2}";
                StartKickoff(scorer);
            }
        }

        void StartKickoff(int scorer)
        {
            // 실점한 팀이 공을 소유하고 양쪽 모두 스폰 위치로 복귀
            state.BallOwner = scorer == 1 ? 2 : 1;
            state.Player1Pos = config.Player1Spawn;
            state.Player2Pos = config.Player2Spawn;
            view.ApplyPositions(state.Player1Pos, state.Player2Pos, state.BallPos);

            kickoffReturnPhase = state.CurrentHalf == 1 ? Phase.FirstHalfPlaying : Phase.SecondHalfPlaying;
            phase = Phase.KickoffCountdown;
            phaseTimer = countdownDuration;
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

        void OnGUI()
        {
            if (hudStyle == null)
            {
                hudStyle = new GUIStyle(GUI.skin.label) { fontSize = 24 };
                bannerStyle = new GUIStyle(GUI.skin.label) { fontSize = 48, alignment = TextAnchor.MiddleCenter };
                countdownStyle = new GUIStyle(GUI.skin.label) { fontSize = 96, alignment = TextAnchor.MiddleCenter };
                inputHudStyle = new GUIStyle(GUI.skin.label) { fontSize = 22 };
            }

            DrawInputHud();

            string halfLabel = phase == Phase.MatchEnded ? "경기 종료" : (state.CurrentHalf == 1 ? "전반" : "후반");
            bool clockRunning = phase is Phase.FirstHalfPlaying or Phase.SecondHalfPlaying or Phase.KickoffCountdown;
            float remaining = clockRunning ? Mathf.Max(0f, halfDuration - halfTimer) : halfDuration;

            string hudText = $"{halfLabel}   1P {state.Score1} : {state.Score2} 2P   남은 시간 {remaining:0.0}s";
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
            Keyboard kb = Keyboard.current;
            Vector2Int instant1 = kb == null ? Vector2Int.zero
                : DirectionalInputAccumulator.InstantDirection(kb.wKey.isPressed, kb.sKey.isPressed, kb.aKey.isPressed, kb.dKey.isPressed);
            Vector2Int instant2 = kb == null ? Vector2Int.zero
                : DirectionalInputAccumulator.InstantDirection(kb.upArrowKey.isPressed, kb.downArrowKey.isPressed, kb.leftArrowKey.isPressed, kb.rightArrowKey.isPressed);
            Vector2Int next1 = input1.FinalizeDirection(Time.time, releaseTolerance);
            Vector2Int next2 = input2.FinalizeDirection(Time.time, releaseTolerance);

            string leftText = $"1P 입력 중: {DirectionToArrow(instant1)}   다음 턴: {DirectionToArrow(next1)}";
            if (showOpponentInput)
                leftText += $"\n(상대) 2P 입력 중: {DirectionToArrow(instant2)}   다음 턴: {DirectionToArrow(next2)}";
            GUI.Label(new Rect(20, 70, 500, 80), leftText, inputHudStyle);

            string rightText = $"2P 입력 중: {DirectionToArrow(instant2)}   다음 턴: {DirectionToArrow(next2)}";
            if (showOpponentInput)
                rightText += $"\n(상대) 1P 입력 중: {DirectionToArrow(instant1)}   다음 턴: {DirectionToArrow(next1)}";

            GUIStyle rightStyle = new GUIStyle(inputHudStyle) { alignment = TextAnchor.UpperRight };
            GUI.Label(new Rect(Screen.width - 520, 70, 500, 80), rightText, rightStyle);
        }

        static string DirectionToArrow(Vector2Int dir)
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
    }
}
