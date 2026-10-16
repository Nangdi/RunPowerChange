using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

[Serializable]
public class PowerRecord
{
    // 기록 시각 (DateTime.Ticks, 로컬 시간). 이름 대신 종료 시각으로 표시한다.
    public long ticks;
    // 발전왕 기준: 체험 중 누적 환산 발전량 (Wh)
    public float energyWh;
    // 판매왕 기준: 판매금액 (원)
    public float saleWon;
    public float arrivedWh;
    public float distanceM;
    public string voltageId;

    public DateTime Time => new DateTime(ticks);
}

[Serializable]
public class PowerRankingData
{
    // 기록이 속한 날짜 (yyyy-MM-dd). 날짜가 바뀌면 기록을 비운다.
    public string day;
    public List<PowerRecord> records = new List<PowerRecord>();
}

// 쳇바퀴 1대의 오늘 랭킹 (발전왕 / 판매왕). persistentDataPath/ranking_wheel{n}.json 에 저장한다.
public class WheelRankingStore
{
    public enum Kind { Energy, Sale }

    private readonly string path;
    private PowerRankingData data = new PowerRankingData();

    // 오늘 체험자 수
    public int Count
    {
        get { RollDay(); return data.records.Count; }
    }

    public WheelRankingStore(int wheelId)
    {
        path = Path.Combine(Application.persistentDataPath, $"ranking_wheel{wheelId}.json");
        Load();
    }

    private static string Today => DateTime.Now.ToString("yyyy-MM-dd");

    public void Load()
    {
        try
        {
            data = File.Exists(path) ? JsonUtility.FromJson<PowerRankingData>(File.ReadAllText(path)) : new PowerRankingData();
        }
        catch (Exception ex)
        {
            Debug.LogError($"랭킹 파일 읽기 실패 - 빈 랭킹으로 시작: {path}\n{ex.Message}");
            data = new PowerRankingData();
        }
        data ??= new PowerRankingData();
        data.records ??= new List<PowerRecord>();
        if (RollDay()) Save();
    }

    public PowerRecord Add(PowerRecord record)
    {
        RollDay();
        data.records.Add(record);
        Save();
        return record;
    }

    // value 기록의 순위. 큰 값이 높은 순위, 같은 값이면 같은 순위.
    // 아직 추가하지 않은 기록의 순위를 미리 볼 때도 같은 값이 나온다.
    public int GetRank(Kind kind, float value)
    {
        RollDay();
        int rank = 1;
        foreach (var r in data.records)
            if (ValueOf(r, kind) > value) rank++;
        return rank;
    }

    // 상위 몇 %인지 (1~100). total 은 이 기록을 포함한 인원.
    public static int TopPercent(int rank, int total)
    {
        if (total <= 0) return 100;
        return Mathf.Clamp(Mathf.CeilToInt(rank * 100f / total), 1, 100);
    }

    public List<PowerRecord> GetTop(Kind kind, int count) => GetTopWith(kind, null, count);

    // 아직 저장하지 않은 기록(extra)을 끼워 넣은 상위 목록. 발전 결과 화면의 미리보기용.
    public List<PowerRecord> GetTopWith(Kind kind, PowerRecord extra, int count)
    {
        RollDay();
        var list = new List<PowerRecord>(data.records);
        if (extra != null) list.Add(extra);
        list.Sort((a, b) =>
        {
            int c = ValueOf(b, kind).CompareTo(ValueOf(a, kind));
            return c != 0 ? c : a.ticks.CompareTo(b.ticks);
        });
        if (count >= 0 && list.Count > count) list.RemoveRange(count, list.Count - count);
        return list;
    }

    // 운영자용: 오늘 기록 전체 삭제
    public void Clear()
    {
        data.records.Clear();
        Save();
    }

    private static float ValueOf(PowerRecord r, Kind kind) => kind == Kind.Energy ? r.energyWh : r.saleWon;

    // 날짜가 바뀌었으면 기록을 비운다. 비웠으면 true.
    private bool RollDay()
    {
        string today = Today;
        if (data.day == today) return false;

        bool hadRecords = data.records.Count > 0;
        data.day = today;
        data.records.Clear();
        if (hadRecords) Debug.Log($"날짜 변경 - 랭킹 초기화: {path}");
        return true;
    }

    private void Save()
    {
        try { JsonManager.SaveData(data, path); }
        catch (Exception ex) { Debug.LogError($"랭킹 저장 실패: {path}\n{ex.Message}"); }
    }
}
