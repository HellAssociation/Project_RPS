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

    protected override void Awake()
    {
        base.Awake();
        LoadData();
    }

    private void LoadData()
    {

    }


    private IEnumerator LoadDataToDictionaryAsync<TKey, TValue>(string dataName, Dictionary<TKey, TValue> targetDictionary)
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

// [Serializable]
// public class WordData : IGameData
// {
//     string IGameData.Code => Code;

//     public int Index;
//     public string Code;
//     public string Word;
// }
