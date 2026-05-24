using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.Scripting;

[RequireDerived]
public static class ManagerHandler
{
    private static readonly Dictionary<Type, FieldInfo> _appFieldInfo;

    static ManagerHandler()
    {
        var flag = BindingFlags.Instance | BindingFlags.NonPublic;
        var fields = typeof(App).GetFields(flag);

        _appFieldInfo = new(fields.Length);

        foreach (var field in fields)
        {
            if (!_appFieldInfo.TryAdd(field.FieldType, field))
            {
                Debug.LogError($"Unresolved manager found. Type: {field.FieldType.Name}");
            }
        }
    }

    internal static void SetFieldValue(Type type, MonoBehaviour manager)
    {
        if (!_appFieldInfo.TryGetValue(type, out var fieldInfo))
        {
            var fields = _appFieldInfo.Values.Where(field => field.FieldType.IsAssignableFrom(type));

            if (fields == null || fields.Count() != 1)
            {
                Debug.LogError($"Unresolved manager found. Type: {type.Name}");
                return;
            }

            fieldInfo = fields.ElementAt(0);
        }

        fieldInfo.SetValue(App.Instance, manager);
    }

    internal static void SetFieldValue(MonoBehaviour manager)
    {
        var type = manager.GetType();
        SetFieldValue(type, manager);
    }
}

public class DataManagerBase : MonoBehaviour
{
    public bool IsDataLoaded { get; protected set; }
    
    protected virtual void Awake() => ManagerHandler.SetFieldValue(this);
}

public class CommonManagerBase : MonoBehaviour
{
    protected virtual void Awake() => ManagerHandler.SetFieldValue(this);
}
