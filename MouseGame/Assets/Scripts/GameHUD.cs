using UnityEngine;
using UnityEngine.UI;

[DefaultExecutionOrder(-200)]
public sealed class GameHUD : MonoBehaviour
{
    [SerializeField, Min(1f)] private float roundDuration = 30f;

    private Text scoreText;
    private Text timerText;
    private float remainingSeconds;
    private int displayedSeconds;
    private int score;

    private void Awake()
    {
        remainingSeconds = roundDuration;

        GameObject canvasObject = new GameObject("HUD Canvas", typeof(RectTransform),
            typeof(Canvas), typeof(CanvasScaler));
        canvasObject.transform.SetParent(transform, false);

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        scoreText = CreateLabel(canvas.transform, "Score", new Vector2(0f, 1f),
            new Vector2(0f, 1f), new Vector2(36f, -24f), TextAnchor.UpperLeft);
        timerText = CreateLabel(canvas.transform, "Timer", new Vector2(1f, 1f),
            new Vector2(1f, 1f), new Vector2(-36f, -24f), TextAnchor.UpperRight);

        scoreText.text = "Score: 0";
        displayedSeconds = Mathf.CeilToInt(remainingSeconds);
        timerText.text = $"Time: {displayedSeconds}";
    }

    private void OnEnable()
    {
        FloorBreak.FloorDestroyed += AddPoint;
    }

    private void OnDisable()
    {
        FloorBreak.FloorDestroyed -= AddPoint;
    }

    private void Update()
    {
        if (remainingSeconds <= 0f) return;

        remainingSeconds = Mathf.Max(0f, remainingSeconds - Time.deltaTime);
        int seconds = Mathf.CeilToInt(remainingSeconds);
        if (seconds == displayedSeconds) return;

        displayedSeconds = seconds;
        timerText.text = $"Time: {seconds}";
    }

    private void AddPoint()
    {
        score++;
        scoreText.text = $"Score: {score}";
    }

    private static Text CreateLabel(Transform parent, string name, Vector2 anchor,
        Vector2 pivot, Vector2 position, TextAnchor alignment)
    {
        GameObject label = new GameObject(name, typeof(RectTransform),
            typeof(CanvasRenderer), typeof(Text), typeof(Outline));
        label.transform.SetParent(parent, false);

        RectTransform rect = label.GetComponent<RectTransform>();
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = pivot;
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(360f, 72f);

        Text text = label.GetComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = 46;
        text.color = Color.white;
        text.alignment = alignment;
        text.raycastTarget = false;

        Outline outline = label.GetComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.8f);
        outline.effectDistance = new Vector2(2f, -2f);
        return text;
    }
}
