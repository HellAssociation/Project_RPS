using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using SystemEnums;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

public class DataManager : DataManagerBase
{
    const string DataPathPrefix = "Data_";

    const int LoadGroupCount = 1;
    int _finishedLoadGroups;

    readonly Dictionary<EBoon, BoonData>         _boonData       = new();
    readonly Dictionary<EDefine, DefineData>      _defineData     = new();
    readonly Dictionary<EDeviation, DeviationData> _deviationData = new();
    readonly Dictionary<EEnemyType, EnemyData>    _enemyData      = new();
    readonly Dictionary<ERound, RoundData>        _roundData      = new();

    protected override void Awake()
    {
        base.Awake();
        LoadData();
    }

    void LoadData()
    {
        StartCoroutine(LoadAllDataCoroutine());
    }

    IEnumerator LoadAllDataCoroutine()
    {
        yield return LoadDataToDictionaryAsync("Boon",      _boonData);
        yield return LoadDataToDictionaryAsync("Define",    _defineData);
        yield return LoadDataToDictionaryAsync("Deviation", _deviationData);
        yield return LoadDataToDictionaryAsync("Enemy",     _enemyData);
        yield return LoadDataToDictionaryAsync("Round",     _roundData);
        NotifyLoadGroupFinished();
    }

    public bool TryGetBoon(EBoon key, out BoonData data)             => _boonData.TryGetValue(key, out data);
    public bool TryGetDefine(EDefine key, out DefineData data)        => _defineData.TryGetValue(key, out data);
    public bool TryGetDeviation(EDeviation key, out DeviationData data) => _deviationData.TryGetValue(key, out data);
    public bool TryGetEnemy(EEnemyType key, out EnemyData data)       => _enemyData.TryGetValue(key, out data);
    public bool TryGetRound(ERound key, out RoundData data)           => _roundData.TryGetValue(key, out data);

    /// <summary>로드된 모든 축복 카드의 인덱스(=CardRef.Index)를 채웁니다. 카드 드로우 풀 구성용.</summary>
    public void GetBoonIndices(List<int> buffer)
    {
        buffer.Clear();
        foreach (EBoon key in _boonData.Keys) buffer.Add((int)key);
    }

    /// <summary>로드된 모든 저주 카드의 인덱스(=CardRef.Index)를 채웁니다. 카드 드로우 풀 구성용.</summary>
    public void GetDeviationIndices(List<int> buffer)
    {
        buffer.Clear();
        foreach (EDeviation key in _deviationData.Keys) buffer.Add((int)key);
    }

    IEnumerator LoadDataToDictionaryAsync<TKey, TValue>(string dataName, Dictionary<TKey, TValue> targetDictionary)
        where TKey : struct, Enum
        where TValue : IGameData
    {
        string dataKey = DataPathPrefix + dataName;
        AsyncOperationHandle<TextAsset> handle = Addressables.LoadAssetAsync<TextAsset>(dataKey);
        yield return handle;

        if (handle.Status != AsyncOperationStatus.Succeeded || handle.Result == null)
        {
            Debug.LogError($"{dataName}Data 로드 실패. key: {dataKey}");
            yield break;
        }

        string json = handle.Result.text;
        if (string.IsNullOrWhiteSpace(json))
        {
            Debug.LogError($"{dataName}Data 파일 내용이 비어 있습니다.");
            yield break;
        }

        Task asyncDeserialize = JsonHelper.JsonToDictAsync<TKey, TValue>(json, targetDictionary);

        while (!asyncDeserialize.IsCompleted)
        {
            yield return null;
        }

        if (!asyncDeserialize.IsCompletedSuccessfully)
        {
            Debug.LogError($"{dataName}Data 역직렬화에 실패했습니다.");
            yield break;
        }

        Debug.Log($"{dataName}Data 로드 완료. 개수: {targetDictionary.Count}");
    }

    void NotifyLoadGroupFinished()
    {
        _finishedLoadGroups++;
        if (_finishedLoadGroups >= LoadGroupCount)
        {
            IsDataLoaded = true;
        }
    }
}

public interface IGameData
{
    string Code { get; }
}
