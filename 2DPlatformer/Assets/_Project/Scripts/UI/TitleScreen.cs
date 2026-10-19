using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Platformer.UI
{
    /// <summary>
    /// 타이틀 화면. 배경은 Title 씬의 도시(TitleCamera 가 천천히 흘러간다)이고, 그 위에 로고·메뉴·시스템 모니터를 올린다.
    ///   메인: 새 게임 / 이어하기 / 섹터 선택 / 설정 / 종료
    ///   섹터 선택: LevelCatalog 순서대로, 해금된 섹터만 고를 수 있고 최고 기록·데이터 칩을 보여 준다
    /// UI 는 런타임에 만든다. Step 28 이 Title 씬에 이 컴포넌트를 둔다.
    /// </summary>
    public sealed class TitleScreen : MonoBehaviour
    {
        [SerializeField] string gameTitle = "NEON CHAIN";
        [SerializeField] string tagline = "갈고리 하나로 네온 도시를 가로질러라";

        CanvasGroup logo;
        float logoTarget = 1f;
        UIScreen mainScreen;
        UIScreen sectorScreen;
        UIScreen settingsScreen;
        UIScreen controlsScreen;
        UIScreen settingsReturn;

        GameFlow Flow => GameFlow.Instance;

        void Start()
        {
            Canvas canvas = UIFactory.CreateCanvas("Title Canvas", 10, transform);
            Transform root = canvas.transform;

            // 왼쪽을 어둡게 — 밝은 네온 배경 위에서도 메뉴 글자가 읽히게
            CyberGraphic shade = UIFactory.Shape(root, "Shade", CyberGraphic.Shape.Backdrop, new Color(0.02f, 0.01f, 0.06f, 0.55f), 0f, 0f);
            shade.rectTransform.anchorMin = Vector2.zero;
            shade.rectTransform.anchorMax = new Vector2(0.62f, 1f);
            shade.rectTransform.offsetMin = shade.rectTransform.offsetMax = Vector2.zero;
            shade.Padding = 0f;
            shade.Value2 = 0.45f; // 오른쪽 45% 에서 서서히 사라진다

            BuildLogo(root);
            BuildMonitor(root);
            mainScreen = BuildMain(root);
            sectorScreen = BuildSectors(root);
            settingsScreen = SettingsScreens.BuildSettings(root, () => Show(settingsReturn), () => Show(controlsScreen));
            controlsScreen = SettingsScreens.BuildControls(root, () => Show(settingsScreen));
            foreach (UIScreen screen in new[] { mainScreen, sectorScreen, settingsScreen, controlsScreen })
                screen.CloseImmediate();

            Text hint = UIFactory.Label(root, "Hint", "[↑↓] 이동    [ENTER/SPACE] 선택    [ESC] 뒤로", UIStyle.Mono, 20, UIStyle.Dim);
            hint.rectTransform.Place(Vector2.zero, Vector2.zero, new Vector2(120f, 48f), new Vector2(900f, 30f));

            Show(mainScreen);
            UISound.Play(UISound.Kind.Glitch, 0.6f);
        }

        void BuildLogo(Transform root)
        {
            RectTransform node = UIFactory.Node("Logo", root).Place(new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(110f, -120f), new Vector2(1000f, 300f));
            logo = node.gameObject.AddComponent<CanvasGroup>();
            logo.blocksRaycasts = false;

            Text boot = UIFactory.Label(node, "Boot", "", UIStyle.Mono, 20, UIStyle.Cyan, TextAnchor.UpperLeft);
            boot.rectTransform.Place(new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(8f, 0f), new Vector2(900f, 30f));
            UIFactory.Decoder(boot).Play("// SYS.BOOT  ::  NEURAL LINK ESTABLISHED  ::  v1.0", 0.1f, 60f);

            CyberGraphic glow = UIFactory.Shape(node, "Glow", CyberGraphic.Shape.Glow, new Color(0.45f, 0.05f, 0.5f, 0.45f), 0f, 0f);
            glow.rectTransform.Place(new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(-80f, 0f), new Vector2(1100f, 260f));
            glow.Padding = 0f;

            Text title = UIFactory.Label(node, "Title", "", UIStyle.Heading, 150, Color.white, TextAnchor.UpperLeft, FontStyle.Bold);
            title.rectTransform.Place(new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, -30f), new Vector2(1000f, 170f));
            TextGlitch glitch = UIFactory.Glitch(title, 5f, 0.06f, 3f);
            glitch.Pulse(1f, 1.2f);
            UIFactory.Decoder(title).Play(gameTitle, 0.3f, 14f);

            Text sub = UIFactory.Label(node, "Tagline", tagline, UIStyle.Body, 32, UIStyle.Magenta, TextAnchor.UpperLeft, FontStyle.Bold);
            sub.rectTransform.Place(new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(8f, -200f), new Vector2(900f, 44f));
            CyberGraphic line = UIFactory.Shape(node, "Line", CyberGraphic.Shape.Line, UIStyle.Cyan, 0f, 0f);
            line.rectTransform.Place(new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(8f, -262f), new Vector2(640f, 4f));
            line.Padding = 0f;
        }

        // 오른쪽 아래 장식: 시스템 상태를 해독하듯 한 줄씩 띄우는 모니터
        void BuildMonitor(Transform root)
        {
            CyberGraphic panel = UIFactory.Shape(root, "Monitor", CyberGraphic.Shape.Panel, new Color(UIStyle.Magenta.r, UIStyle.Magenta.g, UIStyle.Magenta.b, 0.5f), 18f, 2f);
            panel.rectTransform.Place(new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-60f, 60f), new Vector2(520f, 250f));
            int sectors = Flow != null ? Flow.LevelCount : 0;
            int unlocked = Flow != null && Flow.HasProgress ? Flow.UnlockedIndex + 1 : 0;
            string[] lines =
            {
                UIFactory.Dotted("NEURAL LINK", "<color=#66FF99>OK</color>", 30),
                UIFactory.Dotted("GRAPPLE ARM", "<color=#66FF99>ONLINE</color>", 30),
                UIFactory.Dotted("CITY GRID", $"{sectors} SECTORS", 30),
                UIFactory.Dotted("ACCESS", $"{unlocked}/{sectors}", 30),
                UIFactory.Dotted("THREAT LEVEL", "<color=#FF4060>HIGH</color>", 30),
            };
            for (int i = 0; i < lines.Length; i++)
            {
                Text row = UIFactory.Label(panel.rectTransform, "Line " + i, "", UIStyle.Mono, 21, UIStyle.Foreground * 0.85f);
                row.rectTransform.Place(new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(28f, -22f - i * 42f), new Vector2(470f, 34f));
                UIFactory.Decoder(row).Play(lines[i], 0.8f + i * 0.25f, 50f);
            }
        }

        UIScreen BuildMain(Transform root)
        {
            RectTransform node = UIFactory.Node("Main", root).Place(new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(120f, -470f), new Vector2(580f, 460f));
            var screen = node.gameObject.AddComponent<UIScreen>();
            RectTransform column = UIFactory.Column(node, "Buttons", 14f).Stretch();

            var size = new Vector2(580f, 72f);
            bool hasProgress = Flow != null && Flow.HasProgress;
            string continueCaption = "CONTINUE";
            if (hasProgress && GameFlow.Catalog != null && Flow.LevelCount > 0)
                continueCaption = $"CONTINUE · {GameFlow.Catalog.levels[Flow.UnlockedIndex].code}";

            CyberButton newGame = UIFactory.Button(column, "새 게임", "NEW GAME", UIStyle.Cyan, size, () => Flow.NewGame());
            CyberButton continueGame = UIFactory.Button(column, "이어하기", continueCaption, UIStyle.Cyan, size, () => Flow.ContinueGame());
            continueGame.interactable = hasProgress;
            CyberButton sectors = UIFactory.Button(column, "섹터 선택", "SECTOR SELECT", UIStyle.Cyan, size, () => Show(sectorScreen));
            CyberButton settings = UIFactory.Button(column, "설정", "SETTINGS", UIStyle.Amber, size, () =>
            {
                settingsReturn = mainScreen;
                Show(settingsScreen);
            });
            CyberButton quit = UIFactory.Button(column, "종료", "QUIT", UIStyle.Magenta, size, () => Flow.Quit());
            var items = new List<Selectable> { newGame, continueGame, sectors, settings, quit };
            UIFactory.LinkVertical(items);
            screen.FirstSelected = hasProgress ? continueGame : newGame;
            screen.Back = null;
            return screen;
        }

        UIScreen BuildSectors(Transform root)
        {
            int count = GameFlow.Catalog != null ? GameFlow.Catalog.levels.Length : 0;
            float height = 230f + Mathf.Max(count, 1) * 104f + 90f;
            UIScreen screen = SettingsScreens.Frame(root, "Sectors", "SECTORS", "섹터 선택", new Vector2(980f, height), out RectTransform body);
            RectTransform column = UIFactory.Column(body, "Cards", 14f);
            column.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -156f), new Vector2(860f, height - 200f));

            var items = new List<Selectable>();
            for (int i = 0; i < count; i++)
            {
                LevelCatalog.Entry entry = GameFlow.Catalog.levels[i];
                bool unlocked = Flow != null && Flow.IsUnlocked(i);
                string caption;
                if (!unlocked)
                {
                    caption = "LOCKED";
                }
                else
                {
                    float best = Flow.BestTime(entry.scene);
                    caption = $"BEST {(best < 0f ? "--:--.--" : UIStyle.FormatTime(best))} · DATA {Flow.BestChips(entry.scene)}/{entry.chips}";
                }
                int index = i;
                CyberButton card = UIFactory.Button(column, $"{entry.code}   {entry.title}", caption, unlocked ? UIStyle.Cyan : UIStyle.Disabled,
                    new Vector2(860f, 90f), () => Flow.LoadLevel(index));
                card.interactable = unlocked;
                items.Add(card);
            }
            CyberButton back = UIFactory.Button(column, "돌아가기", "BACK", UIStyle.Magenta, new Vector2(860f, 62f), () => Show(mainScreen));
            items.Add(back);
            UIFactory.LinkVertical(items);
            SettingsScreens.Footer(screen.transform, "[↑↓] 이동    [ENTER/SPACE] 섹터 시작    [ESC] 뒤로", -height * 0.5f - 30f);
            screen.FirstSelected = items[0].IsInteractable() ? items[0] : back;
            screen.Back = () => Show(mainScreen);
            return screen;
        }

        // 화면은 하나씩만 연다. 메인이 아니면 로고를 흐리게 (가운데 패널과 겹치므로)
        void Show(UIScreen next)
        {
            foreach (UIScreen screen in new[] { mainScreen, sectorScreen, settingsScreen, controlsScreen })
                if (screen != next && screen.IsOpen)
                    screen.Close();
            next.Open();
            logoTarget = next == mainScreen ? 1f : 0.12f;
        }

        void Update()
        {
            if (logo != null)
                logo.alpha = Mathf.MoveTowards(logo.alpha, logoTarget, Time.unscaledDeltaTime * 4f);
        }
    }
}
