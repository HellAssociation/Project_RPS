using System.Collections.Generic;
using SystemEnums;
using UnityEngine;

/// <summary>
/// Holds one co-op run's progress and resolves gameplay stats through a modifier pipeline:
/// base value (Define/Round data) → Σ Add modifiers → Π Mul modifiers. Boon/Deviation cards
/// contribute modifiers via AddModifier (see CardSystem).
/// </summary>
public class RunState
{
    static DataManager Data => App.Data.BaseData;

    public int CurrentRound { get; set; } = 1;
    public int WonWavesInRound { get; set; }
    public int Lives { get; set; }
    public int EnemyHp { get; set; }
    public int EnemyMaxHp { get; set; }

    readonly List<StatModifier> _modifiers = new();

    public CardDeck BoonDeck { get; } = new();
    public CardDeck DeviationDeck { get; } = new();

    readonly List<int> _drawBuffer = new();

    public void AddModifier(in StatModifier modifier) => _modifiers.Add(modifier);
    public void ClearModifiers() => _modifiers.Clear();

    int BaseMaxHp
    {
        get
        {
            if (Data != null && Data.TryGetDefine(EDefine.DEFINE_PLAYER_DEFAULT_HP, out DefineData d))
                return d.value;
            return PlayerManager.MAX_HP;
        }
    }

    int BaseDamage
    {
        get
        {
            if (Data != null && Data.TryGetDefine(EDefine.DEFINE_PLAYER_DEFAULT_DAMAGE, out DefineData d))
                return d.value;
            return 100;
        }
    }

    float BaseTimer
    {
        get
        {
            if (Data != null && Data.TryGetDefine(EDefine.DEFINE_PLAYER_DEFAULT_TIMER, out DefineData d))
                return d.value;
            return 3f;
        }
    }

    public int MaxLives    => Mathf.RoundToInt(GetStat(EStat.PlayerMaxHp, BaseMaxHp));
    public int PlayerDamage => Mathf.RoundToInt(GetStat(EStat.PlayerDamage, BaseDamage));

    public float GetWaveDuration(int round)
    {
        float baseDuration = BaseTimer;
        if (Data != null && Data.TryGetRound((ERound)(round - 1), out RoundData rd))
            baseDuration += rd.roundTimer;
        return GetStat(EStat.WaveTimer, baseDuration);
    }

    public int GetEnemyMaxHp(int round)
    {
        float mult = 1f;
        if (Data != null && Data.TryGetRound((ERound)(round - 1), out RoundData rd))
            mult = rd.roundHPMultiflier;
        mult = GetStat(EStat.EnemyHpMultiplier, mult);
        return Mathf.RoundToInt(BaseMaxHp * mult);
    }

    public void InitEnemyHp()
    {
        EnemyMaxHp = GetEnemyMaxHp(CurrentRound);
        EnemyHp    = EnemyMaxHp;
    }

    public void Reset()
    {
        CurrentRound    = 1;
        WonWavesInRound = 0;
        ClearModifiers();
        ResetCardDecks();
        Lives           = MaxLives;
        EnemyHp         = 0;
        EnemyMaxHp      = 0;
    }

    void ResetCardDecks()
    {
        if (Data == null)
        {
            BoonDeck.Reset(null);
            DeviationDeck.Reset(null);
            return;
        }

        Data.GetBoonIndices(_drawBuffer);
        BoonDeck.Reset(_drawBuffer);
        Data.GetDeviationIndices(_drawBuffer);
        DeviationDeck.Reset(_drawBuffer);
    }

    float GetStat(EStat stat, float baseValue)
    {
        float value = baseValue;

        for (int i = 0; i < _modifiers.Count; i++)
            if (_modifiers[i].Stat == stat && _modifiers[i].Op == EModifierOp.Add)
                value += _modifiers[i].Value;

        for (int i = 0; i < _modifiers.Count; i++)
            if (_modifiers[i].Stat == stat && _modifiers[i].Op == EModifierOp.Mul)
                value *= _modifiers[i].Value;

        return value;
    }
}
