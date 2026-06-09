using System;

[Serializable]
public class BoonData : IGameData
{
    public int index;
    public string code;
    public string name;
    public string description;
    public string position;
    public int value1;
    public int value2;
    public int value3;
    public int value4;
    public int value5;
    string IGameData.Code => code;
}

[Serializable]
public class DefineData : IGameData
{
    public int index;
    public string code;
    public int value;
    string IGameData.Code => code;
}

[Serializable]
public class DeviationData : IGameData
{
    public int index;
    public string code;
    public string name;
    public string description;
    public string position;
    public int value1;
    public int value2;
    public int value3;
    public int value4;
    public int value5;
    string IGameData.Code => code;
}

[Serializable]
public class EnemyData : IGameData
{
    public int index;
    public string code;
    public string roundTimer;
    public string roundHPMultiflier;
    public string resource;
    public int appearMinRound;
    public int appearMaxRound;
    public int value1;
    public int value2;
    public int value3;
    string IGameData.Code => code;
}

[Serializable]
public class RoundData : IGameData
{
    public int index;
    public string code;
    public float roundTimer;
    public float roundHPMultiflier;
    string IGameData.Code => code;
}
