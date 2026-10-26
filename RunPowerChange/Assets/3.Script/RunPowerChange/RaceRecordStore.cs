using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

// 달리기 1회의 시간별 누적거리. 고스트 캐릭터와 85형 레코드 라인이 "이 기록은 이 시간에 이 위치였다"를 보여 줄 때 쓴다.
[Serializable]
public class RaceTrack
{
    public long ticks;
    public int wheelId;
    // 달린 캐릭터 번호 (고스트를 같은 캐릭터로 보여 준다)
    public int characterIndex;
    public float distanceM;
    public float energyWh;
    // samples[i] = i * SampleSeconds 초 시점의 누적거리(m)
    public List<float> samples = new List<float>();

    public const float SampleSeconds = 0.5f;

    // elapsed 초 시점의 거리. 기록보다 오래 달리면 마지막 위치에 멈춰 있다.
    public float DistanceAt(float elapsed)
    {
        if (samples == null || samples.Count == 0) return 0f;
        float f = Mathf.Max(0f, elapsed) / SampleSeconds;
        int i = Mathf.FloorToInt(f);
        if (i >= samples.Count - 1) return samples[samples.Count - 1];
        return Mathf.Lerp(samples[i], samples[i + 1], f - i);
    }
}

[Serializable]
public class RaceRecordData
{
    // today 가 속한 날짜 (yyyy-MM-dd)
    public string day;
    // 오늘 상위 기록 (거리 내림차순)
    public List<RaceTrack> today = new List<RaceTrack>();
    // 마지막으로 기록이 있었던 날의 상위 기록. 오늘 기록이 없을 때(그날 첫 체험자) 대신 보여 준다.
    public string previousDay;
    public List<RaceTrack> previous = new List<RaceTrack>();
}

// 두 쳇바퀴 공통의 당일 상위 달리기 기록. persistentDataPath/race_records.json.
// 날짜가 바뀌면 오늘 기록을 전날 기록으로 넘기고 비운다 (매일 초기화).
// 오늘 기록이 하나도 없으면 전날 기록을 리더로 쓴다.
public static class RaceRecordStore
{
    // 저장해 두는 상위 기록 수 (레코드 라인 최대 개수)
    private const int KeepCount = 5;

    private static RaceRecordData data;
    private static string Path => System.IO.Path.Combine(Application.persistentDataPath, "race_records.json");
    private static string Today => DateTime.Now.ToString("yyyy-MM-dd");

    public static event Action Changed;

    // 리더 기록 (오늘 → 없으면 전날). 거리 내림차순, 최대 count 개.
    public static List<RaceTrack> GetLeaders(int count)
    {
        Ensure();
        var source = data.today.Count > 0 ? data.today : data.previous;
        var list = new List<RaceTrack>();
        for (int i = 0; i < source.Count && i < count; i++) list.Add(source[i]);
        return list;
    }

    // 지금 리더가 전날 기록인지 (화면 문구용)
    public static bool LeadersFromPreviousDay
    {
        get { Ensure(); return data.today.Count == 0 && data.previous.Count > 0; }
    }

    // 달리기 1회 기록. 거리가 0이면 기록하지 않는다. 오늘 상위에 들면 저장.
    public static void Add(RaceTrack track)
    {
        if (track == null || track.distanceM <= 0f) return;
        Ensure();

        data.today.Add(track);
        data.today.Sort((a, b) =>
        {
            int c = b.distanceM.CompareTo(a.distanceM);
            return c != 0 ? c : a.ticks.CompareTo(b.ticks);
        });
        bool kept = data.today.IndexOf(track) < KeepCount;
        if (data.today.Count > KeepCount) data.today.RemoveRange(KeepCount, data.today.Count - KeepCount);
        if (!kept) return;

        Save();
        Changed?.Invoke();
    }

    // 운영자용: 오늘·전날 기록 모두 삭제
    public static void Clear()
    {
        Ensure();
        data.today.Clear();
        data.previous.Clear();
        data.previousDay = null;
        Save();
        Changed?.Invoke();
    }

    private static void Ensure()
    {
        if (data == null) Load();
        if (data.day == Today) return;

        // 날짜 변경: 오늘 기록이 있으면 전날 기록으로 넘긴다
        if (data.today.Count > 0)
        {
            data.previous = data.today;
            data.previousDay = data.day;
        }
        data.today = new List<RaceTrack>();
        data.day = Today;
        Save();
        Debug.Log($"날짜 변경 - 달리기 기록 초기화 (리더: {data.previousDay ?? "없음"})");
    }

    private static void Load()
    {
        try
        {
            data = File.Exists(Path) ? JsonUtility.FromJson<RaceRecordData>(File.ReadAllText(Path)) : new RaceRecordData();
        }
        catch (Exception ex)
        {
            Debug.LogError($"달리기 기록 파일 읽기 실패 - 빈 기록으로 시작: {Path}\n{ex.Message}");
            data = new RaceRecordData();
        }
        data ??= new RaceRecordData();
        data.today ??= new List<RaceTrack>();
        data.previous ??= new List<RaceTrack>();
    }

    private static void Save()
    {
        try { JsonManager.SaveData(data, Path); }
        catch (Exception ex) { Debug.LogError($"달리기 기록 저장 실패: {Path}\n{ex.Message}"); }
    }
}
