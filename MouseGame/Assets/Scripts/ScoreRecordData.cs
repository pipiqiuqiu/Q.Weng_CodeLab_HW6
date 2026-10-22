using System;
using System.IO;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

[CreateAssetMenu(fileName = "ScoreRecords", menuName = "MouseGame/Score Records")]
public sealed class ScoreRecordData : ScriptableObject
{
    private const int RankCount = 3;
    private const string SaveFileName = "score-records.json";

    [SerializeField, Range(0, RankCount)] private int bestCount;
    [SerializeField] private int[] bestScores = new int[RankCount];
    [SerializeField, Range(0, RankCount)] private int worstCount;
    [SerializeField] private int[] worstScores = new int[RankCount];

    [Serializable]
    private sealed class RecordSnapshot
    {
        public int bestCount;
        public int[] bestScores;
        public int worstCount;
        public int[] worstScores;
    }

    public void LoadInto(int[] best, int[] worst, out int loadedBestCount, out int loadedWorstCount)
    {
        RecordSnapshot snapshot = new RecordSnapshot
        {
            bestCount = bestCount,
            bestScores = bestScores,
            worstCount = worstCount,
            worstScores = worstScores
        };

#if !UNITY_EDITOR
        string path = Path.Combine(Application.persistentDataPath, SaveFileName);
        try
        {
            if (File.Exists(path))
            {
                RecordSnapshot saved = JsonUtility.FromJson<RecordSnapshot>(File.ReadAllText(path));
                if (saved != null)
                    snapshot = saved;
            }
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"Could not read score records from {path}: {exception.Message}", this);
        }
#endif

        loadedBestCount = Mathf.Clamp(snapshot.bestCount, 0,
            Mathf.Min(RankCount, snapshot.bestScores == null ? 0 : snapshot.bestScores.Length));
        loadedWorstCount = Mathf.Clamp(snapshot.worstCount, 0,
            Mathf.Min(RankCount, snapshot.worstScores == null ? 0 : snapshot.worstScores.Length));

        Array.Clear(best, 0, best.Length);
        Array.Clear(worst, 0, worst.Length);
        if (loadedBestCount > 0)
            Array.Copy(snapshot.bestScores, best, loadedBestCount);
        if (loadedWorstCount > 0)
            Array.Copy(snapshot.worstScores, worst, loadedWorstCount);
    }

    public void SaveFrom(int[] best, int savedBestCount, int[] worst, int savedWorstCount)
    {
        bestCount = Mathf.Clamp(savedBestCount, 0, RankCount);
        worstCount = Mathf.Clamp(savedWorstCount, 0, RankCount);
        if (bestScores == null || bestScores.Length != RankCount)
            bestScores = new int[RankCount];
        if (worstScores == null || worstScores.Length != RankCount)
            worstScores = new int[RankCount];

        Array.Clear(bestScores, 0, RankCount);
        Array.Clear(worstScores, 0, RankCount);
        Array.Copy(best, bestScores, bestCount);
        Array.Copy(worst, worstScores, worstCount);

#if UNITY_EDITOR
        EditorUtility.SetDirty(this);
        AssetDatabase.SaveAssets();
#else
        string path = Path.Combine(Application.persistentDataPath, SaveFileName);
        try
        {
            RecordSnapshot snapshot = new RecordSnapshot
            {
                bestCount = bestCount,
                bestScores = bestScores,
                worstCount = worstCount,
                worstScores = worstScores
            };
            Directory.CreateDirectory(Application.persistentDataPath);
            File.WriteAllText(path, JsonUtility.ToJson(snapshot, true));
        }
        catch (Exception exception)
        {
            Debug.LogError($"Could not save score records to {path}: {exception.Message}", this);
        }
#endif
    }
}
