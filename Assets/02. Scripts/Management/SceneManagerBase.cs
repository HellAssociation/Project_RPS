using System;
using SystemEnums;
using UnityEngine;

public abstract class SceneManagerBase : CommonManagerBase
{
    protected override void Awake()
    {
        Debug.Log($"[{GetType().Name}.Awake] GameObject='{gameObject.name}', Scene='{gameObject.scene.name}'", this);
        base.Awake();
    }

    public virtual void MoveToNextScene(EScene nextScene)
    {
        App.LoadScene(nextScene);
    }

    // Logs a failed request (SessionNotFound downgraded to Log) then forwards the result.
    protected static void CompleteRequest(LobbyRequestResult result, Action<LobbyRequestResult> onComplete, string tag)
    {
        if (!result.IsSuccess && !string.IsNullOrEmpty(result.ErrorMessage))
        {
            if (result.ErrorMessage == NetworkManager.SessionNotFoundMessage)
                Debug.Log($"[{tag}] {result.ErrorMessage}");
            else
                Debug.LogError($"[{tag}] {result.ErrorMessage}");
        }

        onComplete?.Invoke(result);
    }
}
