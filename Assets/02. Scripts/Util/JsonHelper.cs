using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

public static class JsonHelper
{

    [Serializable]
    private class Wrapper<T>
    {
        public T[] array;
    }

    public static T[] FromJson<T>(string json)
    {
        string newJson = "";
        if (json[0] == '{')
        {
            newJson = json;
        }
        else
        {
            newJson = "{ \"array\": " + json + "}";
        }
        Wrapper<T> wrapper = JsonUtility.FromJson<Wrapper<T>>(newJson);
        return wrapper.array;
    }

    public static string ToJson<T>(T[] array)
    {
        Wrapper<T> wrapper = new Wrapper<T>();
        wrapper.array = array;
        return JsonUtility.ToJson(wrapper);
    }

    public static Task JsonToDictAsync<T>(string json, Dictionary<string, T> dict) where T : IGameData
    {
        return Task.Run(() =>
        {
            var dataArr = FromJson<T>(json);
            dict.Clear();
            dict.EnsureCapacity(dataArr.Length);

            for (int i = 0; i < dataArr.Length; ++i)
            {
                dict.Add(dataArr[i].Code, dataArr[i]);
            }
        });
    }

    public static Task JsonToDictAsync<TEnum, TValue>(string json, Dictionary<TEnum, TValue> dict) 
    where TValue : IGameData
    where TEnum : struct, Enum
    {
        return Task.Run(() =>
        {
            var dataArr = FromJson<TValue>(json);
            dict.Clear();
            dict.EnsureCapacity(dataArr.Length);

            foreach (var data in dataArr)
            {
                string code = data.Code?.Trim() ?? string.Empty;
                if (string.IsNullOrEmpty(code))
                {
                    continue;
                }

                if (Enum.TryParse(code, ignoreCase: true, out TEnum enumKey))
                {
                    dict[enumKey] = data;
                }
                else
                {
                    Debug.LogWarning($"{nameof(JsonHelper)}: {typeof(TEnum).Name} 키 파싱 실패 — 행이 딕셔너리에서 빠집니다. Code='{data.Code}'");
                }
            }
        });
    }
}
