using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DefaultExecutionOrder(-200)]
[RequireComponent(typeof(Canvas))]
public sealed class GameHUD : MonoBehaviour
{
    private enum RoundState { WaitingForClick, Playing, Resetting }

    private const int RecordLimit = 3;
    private const string LegacyRecordKey = "MouseGame.ScoreRecords.v1.";

    [SerializeField, Min(1f)] private float roundDuration = 30f;
    [SerializeField] private ScoreRecordData recordData;
    [SerializeField] private InchwormLocomotion player;
    [SerializeField] private HeadDirectionSweep directionIndicator;
    [SerializeField] private Text scoreText;
    [SerializeField] private Text timerText;
    [SerializeField] private Text startPrompt;
    [SerializeField] private Text[] bestRankTexts = new Text[3];
    [SerializeField] private Text[] worstRankTexts = new Text[3];

    private readonly int[] bestScores = new int[RecordLimit];
    private readonly int[] worstScores = new int[RecordLimit];
    private int bestCount;
    private int worstCount;
    private float remainingSeconds;
    private int displayedSeconds;
    private int score;
    private RoundState state;

    private void Awake()
    {
        if (recordData == null || player == null || directionIndicator == null ||
            scoreText == null || timerText == null || startPrompt == null ||
            bestRankTexts == null || bestRankTexts.Length != RecordLimit ||
            worstRankTexts == null || worstRankTexts.Length != RecordLimit)
        {
            Debug.LogError("GameHUD needs the player, direction indicator, records, and all UI Text references.", this);
            enabled = false;
            return;
        }
        for (int i = 0; i < RecordLimit; i++)
        {
            if (bestRankTexts[i] != null && worstRankTexts[i] != null) continue;
            Debug.LogError("GameHUD needs all six rank Text references.", this);
            enabled = false;
            return;
        }

        remainingSeconds = roundDuration;
        score = 0;
        state = RoundState.WaitingForClick;
        player.enabled = false;
        directionIndicator.enabled = false;
        startPrompt.gameObject.SetActive(true);
        LoadRecords();
        RefreshRecords();
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
        if (state == RoundState.WaitingForClick)
        {
            if (WasStartPressed()) StartRound();
            return;
        }
        if (state != RoundState.Playing) return;

        remainingSeconds = Mathf.Max(0f, remainingSeconds - Time.deltaTime);
        int seconds = Mathf.CeilToInt(remainingSeconds);
        if (seconds != displayedSeconds)
        {
            displayedSeconds = seconds;
            timerText.text = $"Time: {seconds}";
        }

        if (remainingSeconds <= 0f)
            FinishRound();
    }

    private void AddPoint()
    {
        if (state != RoundState.Playing) return;

        score++;
        scoreText.text = $"Score: {score}";
    }

    private void FinishRound()
    {
        state = RoundState.Resetting;
        player.enabled = false;
        directionIndicator.enabled = false;

        bool changed = InsertScore(bestScores, ref bestCount, score, true);
        changed |= InsertScore(worstScores, ref worstCount, score, false);
        if (changed) SaveRecords();

        int sceneIndex = SceneManager.GetActiveScene().buildIndex;
        if (sceneIndex < 0)
        {
            Debug.LogError("The current scene must be included in Build Settings to restart the round.", this);
            return;
        }
        SceneManager.LoadScene(sceneIndex);
    }

    private void StartRound()
    {
        state = RoundState.Playing;
        directionIndicator.enabled = true;
        player.enabled = true;
        if (!player.enabled)
        {
            state = RoundState.WaitingForClick;
            directionIndicator.enabled = false;
            return;
        }

        startPrompt.gameObject.SetActive(false);
    }

    private static bool WasStartPressed()
    {
        return (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) ||
            (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame);
    }

    private void LoadRecords()
    {
        recordData.LoadInto(bestScores, worstScores, out bestCount, out worstCount);
        if (bestCount != 0 || worstCount != 0 ||
            (!PlayerPrefs.HasKey(LegacyRecordKey + "BestCount") &&
             !PlayerPrefs.HasKey(LegacyRecordKey + "WorstCount")))
            return;

        bestCount = Mathf.Clamp(PlayerPrefs.GetInt(LegacyRecordKey + "BestCount", 0), 0, RecordLimit);
        worstCount = Mathf.Clamp(PlayerPrefs.GetInt(LegacyRecordKey + "WorstCount", 0), 0, RecordLimit);

        for (int i = 0; i < bestCount; i++)
            bestScores[i] = PlayerPrefs.GetInt(LegacyRecordKey + "Best" + i);
        for (int i = 0; i < worstCount; i++)
            worstScores[i] = PlayerPrefs.GetInt(LegacyRecordKey + "Worst" + i);

        if (bestCount > 0 || worstCount > 0)
            SaveRecords();
    }

    private void SaveRecords()
    {
        recordData.SaveFrom(bestScores, bestCount, worstScores, worstCount);
    }

    private void RefreshRecords()
    {
        for (int i = 0; i < RecordLimit; i++)
        {
            if (i < bestCount)
            {
                bestRankTexts[i].text = (i + 1) + ". " + bestScores[i].ToString();
            }
            else
            {
                bestRankTexts[i].text = (i + 1) + ". --";
            }
            if (i < worstCount)
            {
                worstRankTexts[i].text = (i + 1) + ". " + worstScores[i].ToString();
            }
            else
            {
                worstRankTexts[i].text = (i + 1) + ". --";
            }
        }
    }

    private static bool InsertScore(int[] scores, ref int count, int newScore, bool highestFirst)
    {
        int insertAt = 0;
        while (insertAt < count &&
            (highestFirst ? newScore <= scores[insertAt] : newScore >= scores[insertAt]))
            insertAt++;

        if (insertAt >= RecordLimit) return false;

        for (int i = Mathf.Min(count, RecordLimit - 1); i > insertAt; i--)
            scores[i] = scores[i - 1];

        scores[insertAt] = newScore;
        count = Mathf.Min(count + 1, RecordLimit);
        return true;
    }
}
