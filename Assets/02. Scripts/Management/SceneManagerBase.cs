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
}
