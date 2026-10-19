using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Platformer.UI
{
    /// <summary>
    /// 레벨 씬의 UI 전부: HUD(체력 칸·섹터 이름·시간·데이터 칩·사망), 알림(토스트), 섹터 인트로, 조작 안내, 봉쇄 표시,
    /// 일시정지 메뉴, 설정·조작법, 섹터 클리어 결과. GameFlow 가 레벨이 열릴 때 만든다 (씬과 함께 사라진다).
    /// </summary>
    public sealed class GameUI : MonoBehaviour
    {
        public static GameUI Instance { get; private set; }

        GameFlow flow;
        Canvas canvas;
        CanvasGroup hud;
        float hudVisibility = 1f;

        // HUD
        CyberGraphic healthBar;
        Text healthText;
        Text timeText;
        Text chipText;
        Text deathText;
        CyberGraphic chipIcon;
        float healthTrail = 1f;
        float trailHoldUntil;
        float healthFlash;
        float chipFlash;

        // 알림
        CanvasGroup toast;
        CyberGraphic toastPanel;
        Text toastTitle;
        Text toastMessage;
        TextDecoder toastDecoder;
        float toastUntil = -1f;
        readonly Queue<(string Title, string Message, Color Color)> toastQueue = new();

        CanvasGroup intro;
        Text introCode;
        Text introTitle;
        TextDecoder introCodeDecoder;
        TextGlitch introGlitch;
        float introUntil = -1f;
        bool introShown;

        CanvasGroup hint;
        Text hintText;
        HintZone activeHint;

        CanvasGroup lockdown;
        Text lockdownText;

        // 메뉴
        UIScreen pauseScreen;
        UIScreen settingsScreen;
        UIScreen controlsScreen;
        UIScreen clearScreen;
        Text pauseStats;
        CyberButton clearPrimary;
        CyberButton clearRetry;
        CyberButton clearTitle;
        readonly List<Text> clearLines = new();
        Text clearHeading;
        Text clearSub;
        UIScreen settingsReturn; // 설정에서 돌아갈 화면
        UIScreen controlsReturn; // 조작법에서 돌아갈 화면

        public static GameUI Create(GameFlow owner)
        {
            var go = new GameObject("[GameUI]");
            var ui = go.AddComponent<GameUI>();
            ui.flow = owner;
            ui.Build();
            return ui;
        }

        void Awake() => Instance = this;

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        void Start()
        {
            if (flow.Player != null && flow.Player.Health != null)
                flow.Player.Health.Damaged += _ =>
                {
                    healthFlash = 1f;
                    trailHoldUntil = Time.unscaledTime + 0.45f;
                };
            if (!flow.IsTransitioning)
                ShowIntro();
        }

        // ================= 조립 =================

        void Build()
        {
            canvas = UIFactory.CreateCanvas("Canvas", 50, transform);
            Transform root = canvas.transform;

            RectTransform hudRoot = UIFactory.Node("HUD", root).Stretch();
            hud = hudRoot.gameObject.AddComponent<CanvasGroup>();
            hud.blocksRaycasts = false;
            BuildHealthPanel(hudRoot);
            BuildStatsPanel(hudRoot);
            BuildToast(root);
            BuildIntro(root);
            BuildHint(root);
            BuildLockdown(root);

            pauseScreen = BuildPause(root);
            settingsScreen = SettingsScreens.BuildSettings(root, () => SwitchTo(settingsReturn), () => OpenControlsFrom(settingsScreen));
            controlsScreen = SettingsScreens.BuildControls(root, () => SwitchTo(controlsReturn));
            clearScreen = BuildClear(root);
            foreach (UIScreen screen in new[] { pauseScreen, settingsScreen, controlsScreen, clearScreen })
                screen.CloseImmediate();
        }

        void BuildHealthPanel(Transform parent)
        {
            CyberGraphic panel = UIFactory.Shape(parent, "Health Panel", CyberGraphic.Shape.Panel, new Color(UIStyle.Cyan.r, UIStyle.Cyan.g, UIStyle.Cyan.b, 0.6f), 16f, 2f);
            panel.rectTransform.Place(new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(36f, -30f), new Vector2(500f, 116f));
            RectTransform body = panel.rectTransform;

            string code = flow.Level != null ? flow.Level.Code : "SECTOR --";
            string title = flow.Level != null ? flow.Level.Title : "";
            Text sector = UIFactory.Label(body, "Sector", $"{code}  <color=#{UIStyle.Hex(UIStyle.Foreground * 0.8f)}>{title}</color>", UIStyle.Mono, 21, UIStyle.Cyan);
            sector.rectTransform.Place(new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -12f), new Vector2(460f, 30f));
            UIFactory.Glitch(sector, 1.5f, 0f, 9f);

            Text label = UIFactory.Label(body, "HP Label", "SYNC", UIStyle.Mono, 20, UIStyle.Dim);
            label.rectTransform.Place(new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(24f, -70f), new Vector2(80f, 30f));
            healthBar = UIFactory.Shape(body, "Health", CyberGraphic.Shape.Segments, UIStyle.Cyan, 0f, 0f);
            healthBar.Skew = 0.55f;
            healthBar.rectTransform.Place(new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(98f, -70f), new Vector2(300f, 30f));
            healthText = UIFactory.Label(body, "HP Value", "", UIStyle.Mono, 22, UIStyle.Foreground, TextAnchor.MiddleRight);
            healthText.rectTransform.Place(new Vector2(1f, 1f), new Vector2(1f, 0.5f), new Vector2(-24f, -70f), new Vector2(80f, 30f));
        }

        void BuildStatsPanel(Transform parent)
        {
            CyberGraphic panel = UIFactory.Shape(parent, "Stats Panel", CyberGraphic.Shape.Panel, new Color(UIStyle.Magenta.r, UIStyle.Magenta.g, UIStyle.Magenta.b, 0.55f), 16f, 2f);
            panel.rectTransform.Place(new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-36f, -30f), new Vector2(360f, 116f));
            RectTransform body = panel.rectTransform;

            timeText = UIFactory.Label(body, "Time", "00:00.00", UIStyle.Mono, 36, Color.white, TextAnchor.MiddleRight);
            timeText.rectTransform.Place(new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-24f, -8f), new Vector2(320f, 46f));
            Text timeLabel = UIFactory.Label(body, "Time Label", "TIME", UIStyle.Mono, 18, UIStyle.Dim);
            timeLabel.rectTransform.Place(new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -20f), new Vector2(80f, 24f));

            chipIcon = UIFactory.Shape(body, "Chip Icon", CyberGraphic.Shape.Diamond, UIStyle.Amber, 0f, 2f);
            chipIcon.rectTransform.Place(new Vector2(0f, 0f), new Vector2(0f, 0.5f), new Vector2(26f, 30f), new Vector2(22f, 26f));
            chipIcon.Padding = 2f;
            chipText = UIFactory.Label(body, "Chips", "0/0", UIStyle.Mono, 24, UIStyle.Amber);
            chipText.rectTransform.Place(new Vector2(0f, 0f), new Vector2(0f, 0.5f), new Vector2(58f, 30f), new Vector2(120f, 30f));
            deathText = UIFactory.Label(body, "Deaths", "", UIStyle.Mono, 22, UIStyle.Dim, TextAnchor.MiddleRight);
            deathText.rectTransform.Place(new Vector2(1f, 0f), new Vector2(1f, 0.5f), new Vector2(-24f, 30f), new Vector2(180f, 30f));
        }

        void BuildToast(Transform parent)
        {
            RectTransform node = UIFactory.Node("Toast", parent).Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -170f), new Vector2(720f, 92f));
            toast = node.gameObject.AddComponent<CanvasGroup>();
            toast.alpha = 0f;
            toast.blocksRaycasts = false;
            toastPanel = UIFactory.Shape(node, "Panel", CyberGraphic.Shape.Panel, UIStyle.Cyan, 18f, 2f);
            toastPanel.rectTransform.Stretch();
            toastTitle = UIFactory.Label(node, "Title", "", UIStyle.Mono, 22, UIStyle.Cyan, TextAnchor.UpperCenter);
            toastTitle.rectTransform.Stretch(top: 12f);
            UIFactory.Glitch(toastTitle, 2f, 0.05f);
            toastDecoder = UIFactory.Decoder(toastTitle);
            toastMessage = UIFactory.Label(node, "Message", "", UIStyle.Body, 26, UIStyle.Foreground, TextAnchor.LowerCenter);
            toastMessage.rectTransform.Stretch(bottom: 14f);
        }

        void BuildIntro(Transform parent)
        {
            RectTransform node = UIFactory.Node("Intro", parent).Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 170f), new Vector2(1400f, 260f));
            intro = node.gameObject.AddComponent<CanvasGroup>();
            intro.alpha = 0f;
            intro.blocksRaycasts = false;
            CyberGraphic glow = UIFactory.Shape(node, "Glow", CyberGraphic.Shape.Glow, new Color(0.2f, 0.05f, 0.3f, 0.7f), 0f, 0f);
            glow.rectTransform.Stretch(-100f, -100f, -40f, -40f);
            glow.Padding = 0f;
            introCode = UIFactory.Label(node, "Code", "", UIStyle.Heading, 124, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            introCode.rectTransform.Stretch(bottom: 80f);
            introGlitch = UIFactory.Glitch(introCode, 4f, 0.1f, 1.2f);
            introCodeDecoder = UIFactory.Decoder(introCode);
            introTitle = UIFactory.Label(node, "Title", "", UIStyle.Body, 38, UIStyle.Magenta, TextAnchor.LowerCenter, FontStyle.Bold);
            introTitle.rectTransform.Stretch(bottom: 20f);
            CyberGraphic line = UIFactory.Shape(node, "Line", CyberGraphic.Shape.Line, UIStyle.Cyan, 0f, 0f);
            line.rectTransform.Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f), new Vector2(0f, 4f), new Vector2(900f, 4f));
            line.Padding = 0f;
        }

        void BuildHint(Transform parent)
        {
            RectTransform node = UIFactory.Node("Hint", parent).Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 70f), new Vector2(1100f, 74f));
            hint = node.gameObject.AddComponent<CanvasGroup>();
            hint.alpha = 0f;
            hint.blocksRaycasts = false;
            CyberGraphic panel = UIFactory.Shape(node, "Panel", CyberGraphic.Shape.Panel, new Color(UIStyle.Amber.r, UIStyle.Amber.g, UIStyle.Amber.b, 0.8f), 14f, 2f);
            panel.rectTransform.Stretch();
            hintText = UIFactory.Label(node, "Text", "", UIStyle.Body, 27, UIStyle.Foreground, TextAnchor.MiddleCenter);
            hintText.rectTransform.Stretch(24f, 24f);
        }

        void BuildLockdown(Transform parent)
        {
            RectTransform node = UIFactory.Node("Lockdown", parent).Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -36f), new Vector2(520f, 56f));
            lockdown = node.gameObject.AddComponent<CanvasGroup>();
            lockdown.alpha = 0f;
            lockdown.blocksRaycasts = false;
            CyberGraphic panel = UIFactory.Shape(node, "Panel", CyberGraphic.Shape.Panel, UIStyle.Red, 12f, 2f);
            panel.rectTransform.Stretch();
            lockdownText = UIFactory.Label(node, "Text", "", UIStyle.Mono, 24, UIStyle.Foreground, TextAnchor.MiddleCenter);
            lockdownText.rectTransform.Stretch();
            UIFactory.Glitch(lockdownText, 2f, 0.05f, 2.5f);
        }

        UIScreen BuildPause(Transform parent)
        {
            RectTransform root = UIFactory.Node("Pause", parent).Stretch();
            var screen = root.gameObject.AddComponent<UIScreen>();
            CyberGraphic backdrop = UIFactory.Shape(root, "Backdrop", CyberGraphic.Shape.Backdrop, new Color(0.03f, 0.02f, 0.08f, 0.72f), 0f, 0f);
            backdrop.rectTransform.Stretch();
            backdrop.Padding = 0f;
            backdrop.raycastTarget = true;

            Text system = UIFactory.Label(root, "System", "// SYS.HALT — 시뮬레이션 일시정지", UIStyle.Mono, 20, UIStyle.Cyan);
            system.rectTransform.Place(new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(140f, -150f), new Vector2(800f, 30f));
            Text heading = UIFactory.Label(root, "Heading", "PAUSED", UIStyle.Heading, 132, Color.white, TextAnchor.UpperLeft, FontStyle.Bold);
            heading.rectTransform.Place(new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(130f, -180f), new Vector2(900f, 150f));
            UIFactory.Glitch(heading, 4f, 0.08f, 2.5f);
            string sector = flow.Level != null ? $"{flow.Level.Code}  //  {flow.Level.Title}" : "";
            Text sub = UIFactory.Label(root, "Sector", sector, UIStyle.Body, 30, UIStyle.Magenta, TextAnchor.UpperLeft, FontStyle.Bold);
            sub.rectTransform.Place(new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(140f, -330f), new Vector2(900f, 40f));
            pauseStats = UIFactory.Label(root, "Stats", "", UIStyle.Mono, 24, UIStyle.Dim, TextAnchor.UpperLeft);
            pauseStats.rectTransform.Place(new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(140f, -380f), new Vector2(900f, 30f));

            RectTransform column = UIFactory.Column(root, "Buttons", 14f);
            column.Place(new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(140f, -450f), new Vector2(560f, 500f));
            var size = new Vector2(560f, 70f);
            var items = new List<Selectable>
            {
                UIFactory.Button(column, "계속하기", "RESUME", UIStyle.Cyan, size, ResumeGame),
                UIFactory.Button(column, "체크포인트에서 다시 시작", "RESTART", UIStyle.Cyan, size, () =>
                {
                    pauseScreen.Close();
                    flow.RestartFromCheckpoint();
                }),
                UIFactory.Button(column, "설정", "SETTINGS", UIStyle.Amber, size, () => OpenSettingsFrom(pauseScreen)),
                UIFactory.Button(column, "조작법", "CONTROLS", UIStyle.Amber, size, () => OpenControlsFrom(pauseScreen)),
                UIFactory.Button(column, "타이틀로", "MAIN MENU", UIStyle.Magenta, size, () =>
                {
                    pauseScreen.Close();
                    flow.ReturnToTitle();
                }),
            };
            UIFactory.LinkVertical(items);
            SettingsScreens.Footer(root, "[↑↓] 이동    [ENTER/SPACE] 선택    [ESC] 계속하기", -500f);
            screen.FirstSelected = items[0];
            screen.Back = ResumeGame;
            return screen;
        }

        UIScreen BuildClear(Transform parent)
        {
            RectTransform root = UIFactory.Node("Clear", parent).Stretch();
            var screen = root.gameObject.AddComponent<UIScreen>();
            CyberGraphic backdrop = UIFactory.Shape(root, "Backdrop", CyberGraphic.Shape.Backdrop, new Color(0.02f, 0.02f, 0.06f, 0.6f), 0f, 0f);
            backdrop.rectTransform.Stretch();
            backdrop.Padding = 0f;
            backdrop.raycastTarget = true;

            CyberGraphic panel = UIFactory.Shape(root, "Panel", CyberGraphic.Shape.Panel, new Color(UIStyle.Cyan.r, UIStyle.Cyan.g, UIStyle.Cyan.b, 0.95f), 30f, 3f);
            panel.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1060f, 820f));
            RectTransform body = panel.rectTransform;

            clearHeading = UIFactory.Label(body, "Heading", "SECTOR CLEAR", UIStyle.Heading, 96, Color.white, TextAnchor.UpperCenter, FontStyle.Bold);
            clearHeading.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -36f), new Vector2(1000f, 110f));
            UIFactory.Glitch(clearHeading, 4f, 0.1f, 1.8f);
            clearSub = UIFactory.Label(body, "Sub", "", UIStyle.Body, 30, UIStyle.Magenta, TextAnchor.UpperCenter, FontStyle.Bold);
            clearSub.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -150f), new Vector2(1000f, 40f));
            CyberGraphic line = UIFactory.Shape(body, "Divider", CyberGraphic.Shape.Line, UIStyle.Cyan, 0f, 0f);
            line.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -206f), new Vector2(940f, 4f));
            line.Padding = 0f;

            for (int i = 0; i < 7; i++)
            {
                Text row = UIFactory.Label(body, "Line " + i, "", UIStyle.Mono, 30, UIStyle.Foreground, TextAnchor.MiddleLeft);
                row.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -250f - i * 46f), new Vector2(760f, 44f));
                UIFactory.Decoder(row);
                clearLines.Add(row);
            }

            var size = new Vector2(310f, 68f);
            clearPrimary = UIFactory.Button(body, "다음 섹터", "NEXT", UIStyle.Cyan, size, () => flow.NextStage());
            clearRetry = UIFactory.Button(body, "다시 하기", "RETRY", UIStyle.Amber, size, () => flow.RetryStage());
            clearTitle = UIFactory.Button(body, "타이틀로", "MENU", UIStyle.Magenta, size, () => flow.ReturnToTitle());
            screen.FirstSelected = clearPrimary;
            screen.Back = null; // 결과 화면은 Esc 로 닫지 않는다
            return screen;
        }

        // 가로로 놓인 버튼: 가운데 정렬하고 좌우로 이동하게 잇는다
        static void LayoutClearButtons(CyberButton[] buttons)
        {
            for (int i = 0; i < buttons.Length; i++)
            {
                float x = (i - (buttons.Length - 1) * 0.5f) * 330f;
                buttons[i].GetComponent<RectTransform>().Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(x, 44f), new Vector2(310f, 68f));
                buttons[i].navigation = new Navigation
                {
                    mode = Navigation.Mode.Explicit,
                    selectOnLeft = buttons[(i + buttons.Length - 1) % buttons.Length],
                    selectOnRight = buttons[(i + 1) % buttons.Length],
                };
            }
        }

        // ================= 화면 전환 =================

        void SwitchTo(UIScreen next)
        {
            foreach (UIScreen screen in new[] { pauseScreen, settingsScreen, controlsScreen })
                if (screen != next && screen.IsOpen)
                    screen.Close();
            if (next != null)
                next.Open();
        }

        void OpenSettingsFrom(UIScreen from)
        {
            settingsReturn = from;
            SwitchTo(settingsScreen);
        }

        void OpenControlsFrom(UIScreen from)
        {
            controlsReturn = from;
            SwitchTo(controlsScreen);
        }

        void OpenPause()
        {
            flow.Pause();
            settingsReturn = controlsReturn = pauseScreen;
            pauseStats.text = $"TIME {UIStyle.FormatTime(flow.Stage.Time)}    DEATH {flow.Stage.Deaths}    KILL {flow.Stage.Kills}    DATA {flow.Stage.Chips}/{flow.Stage.ChipsTotal}";
            UISound.Play(UISound.Kind.Glitch, 0.6f);
            pauseScreen.Open();
        }

        void ResumeGame()
        {
            SwitchTo(null);
            flow.Resume();
        }

        // ================= 매 프레임 =================

        void Update()
        {
            HandlePauseInput();
            UpdateHud();
            UpdateToast();
            UpdateIntro();
            UpdateHint();
            UpdateLockdown();
        }

        void HandlePauseInput()
        {
            var keyboard = Keyboard.current;
            var gamepad = Gamepad.current;
            bool escape = keyboard != null && keyboard.escapeKey.wasPressedThisFrame;
            bool start = gamepad != null && gamepad.startButton.wasPressedThisFrame;
            if (!(escape || start) || UIScreen.LastBackFrame == Time.frameCount)
                return;

            if (!flow.IsPaused)
            {
                if (flow.CanPause && UIScreen.Top == null)
                    OpenPause();
            }
            else if (start)
            {
                // 패드 Start 는 어느 메뉴에 있든 바로 게임으로 (Esc 는 화면마다 "뒤로")
                ResumeGame();
            }
        }

        void UpdateHud()
        {
            float dt = Time.unscaledDeltaTime;
            bool showHud = flow.StageRunning || flow.IsPaused;
            hudVisibility = Mathf.MoveTowards(hudVisibility, showHud ? 1f : 0f, dt * 3f);
            hud.alpha = hudVisibility;

            PlayerController player = flow.Player;
            if (player != null && player.Health != null)
            {
                Health health = player.Health;
                float fill = health.Max > 0 ? (float)health.Current / health.Max : 0f;
                // 잃은 칸은 잠깐 붉게 남았다가 줄어든다 (어디서 얼마나 맞았는지 읽히게)
                if (fill > healthTrail || Time.unscaledTime > trailHoldUntil)
                    healthTrail = Mathf.Max(fill, Mathf.MoveTowards(healthTrail, fill, dt * 1.5f));
                healthFlash = Mathf.MoveTowards(healthFlash, 0f, dt * 4f);
                healthBar.Segments = Mathf.Max(1, health.Max);
                healthBar.Fill = fill;
                healthBar.Value2 = healthTrail;
                healthBar.Flash = healthFlash;
                bool danger = health.Current <= 1;
                Color tint = danger ? Color.Lerp(UIStyle.Red, UIStyle.Amber, Mathf.PingPong(Time.unscaledTime * 3f, 1f)) : UIStyle.Cyan;
                healthBar.color = tint;
                healthText.text = $"{health.Current}/{health.Max}";
                healthText.color = danger ? UIStyle.Red : UIStyle.Foreground;
            }

            GameFlow.Record stage = flow.Stage;
            timeText.enabled = GameSettings.ShowTimer;
            timeText.text = UIStyle.FormatTime(stage.Time);
            chipText.text = $"{stage.Chips}/{stage.ChipsTotal}";
            chipFlash = Mathf.MoveTowards(chipFlash, 0f, dt * 2.5f);
            chipIcon.Flash = chipFlash;
            chipText.color = Color.Lerp(UIStyle.Amber, Color.white, chipFlash);
            deathText.text = stage.Deaths > 0 ? $"DEATH {stage.Deaths}" : "";
        }

        public void OnChipCollected()
        {
            chipFlash = 1f;
            GameFlow.Record stage = flow.Stage;
            if (stage.ChipsTotal > 0 && stage.Chips >= stage.ChipsTotal)
                ShowToast("DATA COMPLETE", "이 섹터의 데이터 칩을 모두 회수했다", UIStyle.Amber);
        }

        // ----- 토스트 -----

        public void ShowToast(string title, string message, Color color)
        {
            toastQueue.Enqueue((title, message, color));
            if (toastUntil < 0f)
                NextToast();
        }

        void NextToast()
        {
            if (toastQueue.Count == 0)
            {
                toastUntil = -1f;
                return;
            }
            var (title, message, color) = toastQueue.Dequeue();
            toastPanel.color = new Color(color.r, color.g, color.b, 0.85f);
            toastPanel.Flash = 1f;
            toastTitle.color = color;
            toastDecoder.Play(title, 0f, 50f);
            toastMessage.text = message;
            toastUntil = Time.unscaledTime + 2.6f;
        }

        void UpdateToast()
        {
            float dt = Time.unscaledDeltaTime;
            bool visible = toastUntil > Time.unscaledTime;
            // 메뉴(일시정지·결과)가 열려 있으면 숨긴다 — 반투명 패널 너머로 비쳐 보이지 않게
            float target = visible && UIScreen.Top == null ? 1f : 0f;
            toast.alpha = Mathf.MoveTowards(toast.alpha, target, dt * (target > 0f ? 8f : 6f));
            toastPanel.Flash = Mathf.MoveTowards(toastPanel.Flash, 0f, dt * 3f);
            if (toastUntil >= 0f && !visible && toast.alpha <= 0f)
                NextToast();
        }

        // ----- 섹터 인트로 -----

        public void ShowIntro()
        {
            if (introShown || flow.Level == null)
                return;
            introShown = true;
            introCodeDecoder.Play(flow.Level.Code, 0.15f, 18f);
            introTitle.text = flow.Level.Title;
            introGlitch.Pulse(1f, 1f);
            introUntil = Time.unscaledTime + 3.2f;
            UISound.Play(UISound.Kind.Glitch, 0.5f);
        }

        void UpdateIntro()
        {
            bool visible = introUntil > Time.unscaledTime && !flow.IsPaused;
            intro.alpha = Mathf.MoveTowards(intro.alpha, visible ? 1f : 0f, Time.unscaledDeltaTime * (visible ? 6f : 1.5f));
        }

        // ----- 조작 안내 -----

        public void ShowHint(HintZone zone)
        {
            activeHint = zone;
            hintText.text = zone.Message;
        }

        public void HideHint(HintZone zone)
        {
            if (activeHint == zone)
                activeHint = null;
        }

        void UpdateHint()
        {
            bool visible = activeHint != null && flow.StageRunning && !flow.IsPaused;
            hint.alpha = Mathf.MoveTowards(hint.alpha, visible ? 1f : 0f, Time.unscaledDeltaTime * 5f);
        }

        // ----- 아레나 봉쇄 -----

        void UpdateLockdown()
        {
            ArenaGate gate = flow.Player != null && flow.StageRunning ? ArenaGate.FindLocked(flow.Player.BodyCenter) : null;
            lockdown.alpha = Mathf.MoveTowards(lockdown.alpha, gate != null ? 1f : 0f, Time.unscaledDeltaTime * 5f);
            if (gate != null)
                lockdownText.text = $"▲ LOCKDOWN  —  남은 적 {gate.Remaining}";
        }

        // ----- 클리어 -----

        public void ShowStageClear(GameFlow.Record stage, bool newRecord, float best, bool isLast, GameFlow.Record total)
        {
            SwitchTo(null);
            toastQueue.Clear();
            toastUntil = Mathf.Min(toastUntil, Time.unscaledTime);
            activeHint = null;
            string code = flow.Level != null ? flow.Level.Code : "SECTOR";
            string title = flow.Level != null ? flow.Level.Title : "";
            clearHeading.text = isLast ? "MISSION COMPLETE" : "SECTOR CLEAR";
            clearSub.text = isLast ? $"{code}  //  {title}  —  모든 섹터 돌파" : $"{code}  //  {title}";

            var lines = new List<string>
            {
                UIFactory.Dotted("클리어 시간", UIStyle.FormatTime(stage.Time) + (newRecord ? $"  <color=#{UIStyle.Hex(UIStyle.Amber)}>NEW!</color>" : "")),
                UIFactory.Dotted(newRecord ? "이전 기록" : "최고 기록", best < 0f ? "--:--.--" : UIStyle.FormatTime(best)),
                UIFactory.Dotted("사망", stage.Deaths.ToString()),
                UIFactory.Dotted("처치", stage.Kills.ToString()),
                UIFactory.Dotted("데이터 칩", $"{stage.Chips} / {stage.ChipsTotal}"),
            };
            if (total != null)
            {
                lines.Add($"<color=#{UIStyle.Hex(UIStyle.Magenta)}>── 전체 기록 ──</color>");
                lines.Add(UIFactory.Dotted("총 시간 / 사망", $"{UIStyle.FormatTime(total.Time)} / {total.Deaths}"));
            }
            for (int i = 0; i < clearLines.Count; i++)
            {
                TextDecoder decoder = clearLines[i].GetComponent<TextDecoder>();
                if (i < lines.Count)
                    decoder.Play(lines[i], 0.3f + i * 0.18f, 60f);
                else
                    decoder.Play("", 0f);
            }

            clearPrimary.Label.text = isLast ? "타이틀로" : "다음 섹터";
            clearPrimary.Caption.text = isLast ? "MENU" : "NEXT";
            // 마지막 섹터면 "타이틀로" 가 두 개가 되므로 오른쪽 버튼을 숨긴다
            clearTitle.gameObject.SetActive(!isLast);
            LayoutClearButtons(isLast ? new[] { clearPrimary, clearRetry } : new[] { clearPrimary, clearRetry, clearTitle });
            clearScreen.Open();
        }
    }
}
