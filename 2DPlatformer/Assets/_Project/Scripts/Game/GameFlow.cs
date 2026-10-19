using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using Platformer.UI;

namespace Platformer
{
    /// <summary>
    /// 게임 진행: 타이틀 → 섹터 1..N → 결과. 씬이 바뀌어도 살아 있고, 플레이 시작 시 자동으로 만들어진다 (TimeManager 처럼).
    ///   - 레벨 씬이 열리면(PlayerController 가 있으면) 기록을 새로 시작하고 HUD·메뉴(GameUI)를 만든다
    ///   - 기록: 시간(실제 시간, 일시정지 제외)·사망·처치·데이터 칩. 최고 기록과 해금 상태는 PlayerPrefs 에 저장
    ///   - 일시정지: TimeManager.Paused + 플레이어 입력 끄기
    ///   - 씬 전환: ScreenWipe 로 덮고 → 불러오고 → 걷어낸다
    /// 섹터 순서는 Resources/LevelCatalog (Step 27 이 만든다).
    /// </summary>
    [DefaultExecutionOrder(-90)]
    public sealed class GameFlow : MonoBehaviour
    {
        public const string TitleScene = "Title";
        const string UnlockedKey = "progress.unlocked";

        public sealed class Record
        {
            public float Time;
            public int Deaths;
            public int Kills;
            public int Chips;
            public int ChipsTotal;
        }

        // 새 게임으로 시작해 섹터를 순서대로 이어서 깼을 때의 누적 기록 (마지막 결과 화면에 보여 준다)
        sealed class Run
        {
            public bool Active;
            public int Stages;
            public readonly Record Total = new();
        }

        public static GameFlow Instance { get; private set; }

        public Record Stage { get; private set; } = new();
        public LevelInfo Level { get; private set; }
        public PlayerController Player { get; private set; }
        /// <summary>LevelCatalog 안의 지금 섹터 번호 (카탈로그에 없는 씬이면 -1)</summary>
        public int LevelIndex { get; private set; } = -1;
        public bool StageRunning { get; private set; }
        public bool IsPaused { get; private set; }
        public bool IsTransitioning { get; private set; }
        public bool CanPause => Player != null && StageRunning && !IsTransitioning && !IsPaused;

        public static LevelCatalog Catalog => LevelCatalog.Instance;
        public int LevelCount => Catalog != null ? Catalog.levels.Length : 0;
        public bool HasProgress => PlayerPrefs.HasKey(UnlockedKey);
        public int UnlockedIndex => Mathf.Clamp(PlayerPrefs.GetInt(UnlockedKey, 0), 0, Mathf.Max(0, LevelCount - 1));

        readonly Run run = new();
        ScreenWipe wipe;
        GameUI ui;
        int setupSceneHandle = -1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Create()
        {
            if (Instance == null)
                new GameObject("[GameFlow]").AddComponent<GameFlow>();
        }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            EnsureEventSystem();
            wipe = ScreenWipe.Create(transform);
            SceneManager.sceneLoaded += OnSceneLoaded;
            Enemy.Killed += OnEnemyKilled;
            SetupScene(SceneManager.GetActiveScene());
        }

        void OnDestroy()
        {
            if (Instance != this)
                return;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            Enemy.Killed -= OnEnemyKilled;
            TimeManager.Paused = false;
            Instance = null;
        }

        static void EnsureEventSystem()
        {
            if (EventSystem.current != null || FindAnyObjectByType<EventSystem>() != null)
                return;
            var go = new GameObject("[EventSystem]");
            DontDestroyOnLoad(go);
            go.AddComponent<EventSystem>();
            go.AddComponent<InputSystemUIInputModule>(); // 액션을 안 넣으면 기본 UI 액션(방향키·WASD·패드·마우스)을 쓴다
        }

        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (mode == LoadSceneMode.Single)
                SetupScene(scene);
        }

        void SetupScene(Scene scene)
        {
            if (scene.handle == setupSceneHandle)
                return;
            setupSceneHandle = scene.handle;

            TimeManager.Paused = false;
            IsPaused = false;
            Player = FindAnyObjectByType<PlayerController>();
            Level = FindAnyObjectByType<LevelInfo>();
            LevelIndex = Catalog != null ? Catalog.IndexOf(scene.name) : -1;
            StageRunning = Player != null;
            ui = null;
            if (Player == null)
                return;

            Stage = new Record { ChipsTotal = FindObjectsByType<DataChip>(FindObjectsSortMode.None).Length };
            if (Player.Health != null)
                Player.Health.Died += _ => Stage.Deaths++;
            ui = GameUI.Create(this);
        }

        void Update()
        {
            if (StageRunning && !IsPaused && !IsTransitioning)
                Stage.Time += Time.unscaledDeltaTime; // 슬로우·히트스톱에 기록이 늘어지지 않게 실제 시간으로
        }

        void OnEnemyKilled(Enemy enemy)
        {
            if (StageRunning)
                Stage.Kills++;
        }

        // ----- 게임 안 알림 -----

        public void CollectChip()
        {
            Stage.Chips++;
            if (ui != null)
                ui.OnChipCollected();
        }

        public void NotifyCheckpoint(bool first)
        {
            Toast("CHECKPOINT", first ? "동기화 완료 — 이곳에서 다시 시작합니다" : "재동기화 — 체력 회복", UIStyle.Cyan);
        }

        public void Toast(string title, string message, Color color)
        {
            if (ui != null)
                ui.ShowToast(title, message, color);
        }

        // ----- 일시정지 -----

        public void Pause()
        {
            if (!CanPause)
                return;
            IsPaused = true;
            TimeManager.Paused = true;
            Player.Input.InputEnabled = false;
        }

        public void Resume()
        {
            if (!IsPaused)
                return;
            IsPaused = false;
            TimeManager.Paused = false;
            if (Player != null && StageRunning)
                Player.Input.InputEnabled = true;
        }

        public void RestartFromCheckpoint()
        {
            Resume();
            if (Player != null)
                Player.RestartAtCheckpoint();
        }

        // ----- 클리어 -----

        public void CompleteStage(Vector2 exitPosition)
        {
            if (!StageRunning)
                return;
            StageRunning = false;
            Player.Input.InputEnabled = false;
            if (Player.Health != null)
                Player.Health.SetInvincible(9999f);

            string scene = SceneManager.GetActiveScene().name;
            float previousBest = BestTime(scene);
            bool newRecord = previousBest < 0f || Stage.Time < previousBest;
            if (newRecord)
                PlayerPrefs.SetFloat("best." + scene, Stage.Time);
            if (Stage.Chips > BestChips(scene))
                PlayerPrefs.SetInt("chips." + scene, Stage.Chips);
            if (LevelIndex >= 0 && LevelIndex + 1 < LevelCount)
                PlayerPrefs.SetInt(UnlockedKey, Mathf.Max(PlayerPrefs.GetInt(UnlockedKey, 0), LevelIndex + 1));
            else if (!HasProgress)
                PlayerPrefs.SetInt(UnlockedKey, 0);
            PlayerPrefs.Save();

            // 새 게임부터 순서대로 이어서 깨고 있으면 누적
            if (run.Active && LevelIndex == run.Stages)
            {
                run.Stages++;
                run.Total.Time += Stage.Time;
                run.Total.Deaths += Stage.Deaths;
                run.Total.Kills += Stage.Kills;
                run.Total.Chips += Stage.Chips;
                run.Total.ChipsTotal += Stage.ChipsTotal;
            }
            else
            {
                run.Active = false;
            }

            bool isLast = LevelIndex < 0 || LevelIndex >= LevelCount - 1;
            Record total = isLast && run.Active && run.Stages == LevelCount ? run.Total : null;
            StartCoroutine(ClearSequence(exitPosition, newRecord, newRecord ? previousBest : BestTime(scene), isLast, total));
        }

        IEnumerator ClearSequence(Vector2 exitPosition, bool newRecord, float best, bool isLast, Record total)
        {
            TimeManager.SlowMotion(0.25f, 0.5f, 0.05f, 0.3f);
            PostFX.Shockwave(exitPosition + Vector2.up * 2f, 1f, 0.8f);
            PostFX.Glitch(0.4f, 0.5f);
            UISound.Play(UISound.Kind.Clear);
            for (float t = 0f; t < 0.9f; t += Time.unscaledDeltaTime)
                yield return null;
            TimeManager.Paused = true; // 결과 화면 동안 적·총알이 움직이지 않게
            if (ui != null)
                ui.ShowStageClear(Stage, newRecord, best, isLast, total);
        }

        public float BestTime(string scene) => PlayerPrefs.GetFloat("best." + scene, -1f);
        public int BestChips(string scene) => PlayerPrefs.GetInt("chips." + scene, 0);
        public bool IsUnlocked(int index) => index <= UnlockedIndex;

        // ----- 씬 이동 -----

        public void NewGame()
        {
            run.Active = true;
            run.Stages = 0;
            run.Total.Time = 0f;
            run.Total.Deaths = run.Total.Kills = run.Total.Chips = run.Total.ChipsTotal = 0;
            if (!HasProgress)
            {
                PlayerPrefs.SetInt(UnlockedKey, 0);
                PlayerPrefs.Save();
            }
            LoadLevel(0, keepRun: true);
        }

        public void ContinueGame() => LoadLevel(UnlockedIndex);

        public void LoadLevel(int index, bool keepRun = false)
        {
            if (Catalog == null || index < 0 || index >= LevelCount)
            {
                Debug.LogWarning("[GameFlow] LevelCatalog 가 없거나 섹터 번호가 범위를 벗어났습니다 — Step 27 (Build Levels) 을 실행하세요");
                return;
            }
            if (!keepRun)
                run.Active = false;
            LevelCatalog.Entry entry = Catalog.levels[index];
            LoadScene(entry.scene, $"{entry.code}  //  {entry.title}");
        }

        public void NextStage()
        {
            if (LevelIndex >= 0 && LevelIndex + 1 < LevelCount)
                LoadLevel(LevelIndex + 1, keepRun: run.Active);
            else
                ReturnToTitle();
        }

        public void RetryStage()
        {
            run.Active = false;
            Scene scene = SceneManager.GetActiveScene();
            LoadScene(scene.name, Level != null ? $"{Level.Code}  //  RETRY" : "RETRY");
        }

        public void ReturnToTitle() => LoadScene(TitleScene, "MAIN MENU");

        public void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        public void LoadScene(string sceneName, string label)
        {
            if (IsTransitioning)
                return;
            if (!Application.CanStreamedLevelBeLoaded(sceneName))
            {
                Debug.LogWarning($"[GameFlow] '{sceneName}' 씬이 Build Settings 에 없습니다 — Step 27/28 을 실행하세요");
                return;
            }
            StartCoroutine(Transition(sceneName, label));
        }

        IEnumerator Transition(string sceneName, string label)
        {
            IsTransitioning = true;
            if (Player != null)
                Player.Input.InputEnabled = false;
            UISound.Play(UISound.Kind.Glitch);
            PostFX.Glitch(0.5f, 0.4f);
            yield return wipe.Close(label);

            TimeManager.Paused = true; // 덮인 동안은 멈춰 둔다 — 불러오는 사이 이전 씬이 움직이지 않게
            AsyncOperation load = SceneManager.LoadSceneAsync(sceneName);
            while (!load.isDone)
                yield return null;
            TimeManager.Paused = false;
            yield return null; // 새 씬의 Start 가 돌고 카메라가 자리 잡을 시간

            yield return wipe.Open();
            IsTransitioning = false;
            if (ui != null)
                ui.ShowIntro();
        }
    }
}
