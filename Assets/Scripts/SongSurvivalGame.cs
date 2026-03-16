using System.Collections;
using SongSurvival.Core;
using SongSurvival.Gameplay;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SongSurvival
{
    public sealed class SongSurvivalGame : MonoBehaviour
    {
        private enum GameState
        {
            Boot,
            Home,
            Calibration,
            Gameplay,
            GameOver
        }

        private static Sprite cachedSprite;

        private IAudioInputService audioInputService;
        private IAudioAnalysisService audioAnalysisService;
        private DifficultyModel difficultyModel;
        private RunScoreService runScoreService;
        private HazardDirector hazardDirector;
        private PlayerController playerController;
        private Camera sceneCamera;
        private Image backgroundImage;

        private GameState state;
        private Coroutine calibrationRoutine;
        private CalibrationResult lastCalibrationResult;
        private float gameplayElapsed;

        private Text homeBodyText;
        private Text calibrationStatusText;
        private Text calibrationMetricsText;
        private Text calibrationActionText;
        private Text scoreText;
        private Text bestText;
        private Text audioStatusText;
        private Text gameOverText;

        private GameObject homePanel;
        private GameObject calibrationPanel;
        private GameObject gameplayPanel;
        private GameObject gameOverPanel;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void EnsureBootstrapExists()
        {
            if (FindObjectOfType<SongSurvivalGame>() != null)
            {
                return;
            }

            GameObject bootstrap = new GameObject(nameof(SongSurvivalGame));
            DontDestroyOnLoad(bootstrap);
            bootstrap.AddComponent<SongSurvivalGame>();
        }

        private void Awake()
        {
            Screen.orientation = ScreenOrientation.Portrait;
            Application.targetFrameRate = (int)GameConstants.TargetFrameRate;

            audioInputService = new MicrophoneInputService();
            difficultyModel = new DifficultyModel();
            runScoreService = new RunScoreService();

            BuildWorld();
            BuildUi();
            TransitionTo(GameState.Boot);
        }

        private void Update()
        {
            audioAnalysisService?.Tick(Time.deltaTime);
            UpdateReactiveBackground();

            if (state != GameState.Gameplay)
            {
                return;
            }

            gameplayElapsed += Time.deltaTime;
            DifficultySnapshot difficulty = difficultyModel.Evaluate(audioAnalysisService.CurrentFrame, gameplayElapsed, Time.deltaTime);
            hazardDirector.Tick(difficulty);
            runScoreService.Tick(Time.deltaTime, difficulty.Danger);
            RefreshGameplayHud();
        }

        private void OnDestroy()
        {
            audioInputService?.StopCapture();
        }

        private void BuildWorld()
        {
            sceneCamera = new GameObject("Main Camera").AddComponent<Camera>();
            sceneCamera.orthographic = true;
            sceneCamera.orthographicSize = 8f;
            sceneCamera.backgroundColor = GameConstants.CameraBackground;
            sceneCamera.transform.position = new Vector3(0f, 0f, -10f);
            sceneCamera.tag = "MainCamera";

            GameObject playerGo = new GameObject("Player");
            SpriteRenderer renderer = playerGo.AddComponent<SpriteRenderer>();
            renderer.sprite = GetSharedSprite();
            renderer.color = GameConstants.PlayerColor;
            renderer.sortingOrder = 4;
            playerGo.transform.localScale = new Vector3(0.9f, 0.9f, 1f);

            Rigidbody2D body = playerGo.AddComponent<Rigidbody2D>();
            body.isKinematic = true;
            body.gravityScale = 0f;

            CircleCollider2D collider = playerGo.AddComponent<CircleCollider2D>();
            collider.isTrigger = true;
            collider.radius = 0.46f;

            playerController = playerGo.AddComponent<PlayerController>();
            playerController.Initialize(sceneCamera, GameConstants.PlayerMinX, GameConstants.PlayerMaxX, GameConstants.PlayerY);
            playerController.Hit += HandlePlayerHit;
            playerController.SetActive(false);

            GameObject hazardRoot = new GameObject("Hazards");
            hazardDirector = new HazardDirector(hazardRoot.transform, GetSharedSprite());
        }

        private void BuildUi()
        {
            if (FindObjectOfType<EventSystem>() == null)
            {
                GameObject eventSystem = new GameObject("EventSystem");
                eventSystem.AddComponent<EventSystem>();
                eventSystem.AddComponent<StandaloneInputModule>();
            }

            Canvas canvas = new GameObject("Canvas").AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.pixelPerfect = true;
            CanvasScaler scaler = canvas.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            canvas.gameObject.AddComponent<GraphicRaycaster>();

            GameObject background = CreateUiElement("Background", canvas.transform);
            backgroundImage = background.AddComponent<Image>();
            backgroundImage.color = new Color(0.11f, 0.12f, 0.18f, 0.92f);
            Stretch((RectTransform)background.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            homePanel = CreatePanel(canvas.transform);
            CreateLabel(homePanel.transform, "Song Survival", 72, new Vector2(0.5f, 0.78f), FontStyle.Bold);
            homeBodyText = CreateLabel(homePanel.transform, string.Empty, 34, new Vector2(0.5f, 0.58f));
            homeBodyText.alignment = TextAnchor.MiddleCenter;
            homeBodyText.rectTransform.sizeDelta = new Vector2(900f, 500f);
            CreateButton(homePanel.transform, "Start Calibration", new Vector2(0.5f, 0.27f), BeginCalibration);

            calibrationPanel = CreatePanel(canvas.transform);
            CreateLabel(calibrationPanel.transform, "Listening...", 70, new Vector2(0.5f, 0.78f), FontStyle.Bold);
            calibrationStatusText = CreateLabel(calibrationPanel.transform, string.Empty, 34, new Vector2(0.5f, 0.56f));
            calibrationStatusText.alignment = TextAnchor.MiddleCenter;
            calibrationStatusText.rectTransform.sizeDelta = new Vector2(900f, 250f);
            calibrationMetricsText = CreateLabel(calibrationPanel.transform, string.Empty, 28, new Vector2(0.5f, 0.42f));
            calibrationMetricsText.alignment = TextAnchor.MiddleCenter;
            calibrationMetricsText.rectTransform.sizeDelta = new Vector2(900f, 200f);
            calibrationActionText = CreateLabel(calibrationPanel.transform, "Play stays locked until audio is at least weak-playable.", 26, new Vector2(0.5f, 0.32f));
            calibrationActionText.alignment = TextAnchor.MiddleCenter;
            calibrationActionText.rectTransform.sizeDelta = new Vector2(900f, 180f);
            CreateButton(calibrationPanel.transform, "Retry", new Vector2(0.35f, 0.24f), BeginCalibration);
            CreateButton(calibrationPanel.transform, "Play", new Vector2(0.65f, 0.24f), TryStartRun);

            gameplayPanel = CreatePanel(canvas.transform);
            scoreText = CreateLabel(gameplayPanel.transform, "Score 0.0", 42, new Vector2(0.18f, 0.95f), FontStyle.Bold);
            bestText = CreateLabel(gameplayPanel.transform, "Best 0.0", 34, new Vector2(0.18f, 0.91f));
            audioStatusText = CreateLabel(gameplayPanel.transform, "Audio quiet", 32, new Vector2(0.76f, 0.95f));
            CreateButton(gameplayPanel.transform, "Pause", new Vector2(0.82f, 0.08f), ReturnHome);

            gameOverPanel = CreatePanel(canvas.transform);
            CreateLabel(gameOverPanel.transform, "Run Over", 74, new Vector2(0.5f, 0.75f), FontStyle.Bold);
            gameOverText = CreateLabel(gameOverPanel.transform, string.Empty, 36, new Vector2(0.5f, 0.55f));
            gameOverText.alignment = TextAnchor.MiddleCenter;
            gameOverText.rectTransform.sizeDelta = new Vector2(900f, 260f);
            CreateButton(gameOverPanel.transform, "Replay", new Vector2(0.35f, 0.27f), TryStartRun);
            CreateButton(gameOverPanel.transform, "Home", new Vector2(0.65f, 0.27f), ReturnHome);
        }

        private void TransitionTo(GameState nextState)
        {
            state = nextState;
            homePanel.SetActive(state == GameState.Home || state == GameState.Boot);
            calibrationPanel.SetActive(state == GameState.Calibration);
            gameplayPanel.SetActive(state == GameState.Gameplay);
            gameOverPanel.SetActive(state == GameState.GameOver);

            if (state == GameState.Boot)
            {
                StartCoroutine(BootSequence());
            }
            else if (state == GameState.Home)
            {
                RefreshHome();
                playerController.SetActive(false);
                hazardDirector.Clear();
            }
            else if (state == GameState.Gameplay)
            {
                playerController.SetActive(true);
            }
        }

        private IEnumerator BootSequence()
        {
            homeBodyText.text = "Ask for mic permission, listen to the room, then survive inside the song.";
            if (!Application.HasUserAuthorization(UserAuthorization.Microphone))
            {
                yield return Application.RequestUserAuthorization(UserAuthorization.Microphone);
            }

            audioInputService.RefreshDevices();
            if (!audioInputService.HasPermission)
            {
                homeBodyText.text = "Microphone access is required. Enable it in Settings to let the game hear the song.";
            }
            else if (!audioInputService.HasDevices)
            {
                homeBodyText.text = "No microphone input found on this device.";
            }

            audioInputService.StartCapture(GameConstants.SampleRate, GameConstants.ClipLengthSeconds);
            audioAnalysisService = new AudioAnalysisService(audioInputService);
            yield return null;
            TransitionTo(GameState.Home);
        }

        private void RefreshHome()
        {
            if (!audioInputService.HasPermission)
            {
                homeBodyText.text = "Microphone access is off. Enable it in Settings, then relaunch.";
                return;
            }

            if (!audioInputService.IsInputAvailable)
            {
                homeBodyText.text = "This device has no active microphone input.";
                return;
            }

            homeBodyText.text = "Play music nearby, then calibrate. Same-device speaker is best-effort only on iPhone, so the app may ask for louder audio or an external speaker.";
        }

        private void BeginCalibration()
        {
            if (!audioInputService.HasPermission || !audioInputService.IsInputAvailable)
            {
                ReturnHome();
                return;
            }

            if (calibrationRoutine != null)
            {
                StopCoroutine(calibrationRoutine);
            }

            TransitionTo(GameState.Calibration);
            calibrationRoutine = StartCoroutine(CalibrationSequence());
        }

        private IEnumerator CalibrationSequence()
        {
            audioAnalysisService.ResetCalibrationWindow();
            float duration = GameConstants.CalibrationDurationSeconds;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                AudioFeatureFrame frame = audioAnalysisService.CurrentFrame;
                calibrationStatusText.text = $"Listening {Mathf.CeilToInt(duration - elapsed)}";
                calibrationMetricsText.text = $"Energy {frame.Energy:0.00}  Bass {frame.BassEnergy:0.00}  Confidence {frame.Confidence:0.00}";
                yield return null;
            }

            lastCalibrationResult = audioAnalysisService.BuildCalibrationResult();
            calibrationStatusText.text = lastCalibrationResult.Message;
            calibrationMetricsText.text = $"Avg energy {lastCalibrationResult.AverageEnergy:0.00}\nAvg confidence {lastCalibrationResult.AverageConfidence:0.00}\nPeak activity {lastCalibrationResult.PeakRate:0.00}";
            calibrationActionText.text = BuildCalibrationAction(lastCalibrationResult.Quality);
            calibrationRoutine = null;
        }

        private void TryStartRun()
        {
            if (state == GameState.Calibration && calibrationRoutine != null)
            {
                return;
            }

            if (lastCalibrationResult.Quality == CalibrationQuality.FallbackRequired)
            {
                calibrationStatusText.text = "Still too quiet. Retry after raising volume or moving the phone closer to the speaker.";
                return;
            }

            hazardDirector.Clear();
            gameplayElapsed = 0f;
            runScoreService.ResetRun();
            playerController.Initialize(sceneCamera, GameConstants.PlayerMinX, GameConstants.PlayerMaxX, GameConstants.PlayerY);
            TransitionTo(GameState.Gameplay);
            RefreshGameplayHud();
        }

        private void HandlePlayerHit()
        {
            if (state != GameState.Gameplay)
            {
                return;
            }

            runScoreService.CommitIfBest();
            playerController.SetActive(false);
            hazardDirector.Clear();
            gameOverText.text = $"Score {runScoreService.CurrentScore:0.0}\nBest {runScoreService.BestScore:0.0}\n\nPress Replay to dive back into the song.";
            TransitionTo(GameState.GameOver);
        }

        private void ReturnHome()
        {
            TransitionTo(GameState.Home);
        }

        private void RefreshGameplayHud()
        {
            AudioFeatureFrame frame = audioAnalysisService.CurrentFrame;
            scoreText.text = $"Score {runScoreService.CurrentScore:0.0}";
            bestText.text = $"Best {runScoreService.BestScore:0.0}";
            audioStatusText.text = BuildLiveAudioStatus(frame);
        }

        private void UpdateReactiveBackground()
        {
            if (backgroundImage == null || audioAnalysisService == null)
            {
                return;
            }

            AudioFeatureFrame frame = audioAnalysisService.CurrentFrame;
            backgroundImage.color = Color.Lerp(GameConstants.BackgroundCalm, GameConstants.BackgroundLoud, Mathf.Clamp01((frame.Energy * 0.65f) + (frame.Brightness * 0.35f)));
        }

        private static GameObject CreatePanel(Transform parent)
        {
            GameObject panel = CreateUiElement("Panel", parent);
            Stretch((RectTransform)panel.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            return panel;
        }

        private static GameObject CreateUiElement(string name, Transform parent)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        private static Text CreateLabel(Transform parent, string text, int fontSize, Vector2 anchor, FontStyle fontStyle = FontStyle.Normal)
        {
            GameObject go = CreateUiElement("Text", parent);
            RectTransform rect = (RectTransform)go.transform;
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(800f, 120f);

            Text label = go.AddComponent<Text>();
            label.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            label.text = text;
            label.fontSize = fontSize;
            label.color = Color.white;
            label.alignment = TextAnchor.MiddleCenter;
            label.fontStyle = fontStyle;
            return label;
        }

        private static void CreateButton(Transform parent, string title, Vector2 anchor, UnityEngine.Events.UnityAction onClick)
        {
            GameObject go = CreateUiElement(title, parent);
            RectTransform rect = (RectTransform)go.transform;
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(320f, 110f);

            Image image = go.AddComponent<Image>();
            image.color = GameConstants.ButtonColor;

            Button button = go.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(onClick);

            Text text = CreateLabel(go.transform, title, 34, new Vector2(0.5f, 0.5f), FontStyle.Bold);
            text.rectTransform.sizeDelta = rect.sizeDelta;
            text.color = GameConstants.ButtonTextColor;
        }

        private static void Stretch(RectTransform rect, Vector2 min, Vector2 max, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }

        private static string BuildCalibrationAction(CalibrationQuality quality)
        {
            switch (quality)
            {
                case CalibrationQuality.Usable:
                    return "Signal is healthy. Start the run when ready.";
                case CalibrationQuality.WeakPlayable:
                    return "Playable now, but louder music should make the run feel richer.";
                default:
                    return "Raise volume, move the phone, or switch to an external speaker before starting.";
            }
        }

        private static string BuildLiveAudioStatus(AudioFeatureFrame frame)
        {
            if (frame.Confidence >= 0.55f)
            {
                return $"Audio strong {frame.Confidence:0.00}";
            }

            if (frame.Confidence >= 0.22f)
            {
                return $"Audio weak {frame.Confidence:0.00}";
            }

            return $"Audio quiet {frame.Confidence:0.00}";
        }

        private static Sprite GetSharedSprite()
        {
            if (cachedSprite != null)
            {
                return cachedSprite;
            }

            cachedSprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f));
            return cachedSprite;
        }
    }
}
