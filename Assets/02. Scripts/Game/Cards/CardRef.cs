using SystemEnums;

/// <summary>Identifies a card over the wire and for data lookup: its kind plus its enum index.</summary>
public readonly struct CardRef
{
    public readonly ECardKind Kind;
    public readonly int Index;

    public CardRef(ECardKind kind, int index)
    {
        Kind  = kind;
        Index = index;
    }
}
