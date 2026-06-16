using System.Collections.Generic;
using SystemEnums;
using UnityEngine.InputSystem;

/// <summary>두억시니: remaps the five finger keys to random A~Z keys, re-rolled each round.</summary>
public class KeyChaosEffect : IRoundBeginEffect
{
    static readonly EFingerType[] Fingers =
    {
        EFingerType.Thumb, EFingerType.Index, EFingerType.Middle,
        EFingerType.Ring,  EFingerType.Pinky,
    };

    readonly List<Key> _pool = BuildPool();
    readonly Dictionary<EFingerType, Key> _bindings = new(Fingers.Length);

    public ECardEffect Id => ECardEffect.Deviation_KeyChaos;

    public void OnRoundBegin(IInputControl control)
    {
        for (int i = _pool.Count - 1; i > 0; i--)
        {
            int j = control.Rng.Next(i + 1);
            (_pool[i], _pool[j]) = (_pool[j], _pool[i]);
        }

        _bindings.Clear();
        for (int i = 0; i < Fingers.Length; i++)
            _bindings[Fingers[i]] = _pool[i];

        control.ApplyBindings(_bindings);
    }

    static List<Key> BuildPool()
    {
        var pool = new List<Key>(26);
        for (Key k = Key.A; k <= Key.Z; k++)
            pool.Add(k);
        return pool;
    }
}
