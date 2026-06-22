using SystemEnums;
using UnityEngine;

/// <summary>Resolves a card's UI presentation (boon flag, icon sprite) from its CardRef and data.</summary>
public static class CardPresentation
{
    public static bool IsBoon(CardRef card) => card.Kind == ECardKind.Boon;

    public static bool TryGetCode(CardRef card, out string code)
    {
        code = null;
        DataManager data = App.Data.BaseData;
        if (data == null) return false;

        switch (card.Kind)
        {
            case ECardKind.Boon:
                if (data.TryGetBoon((EBoon)card.Index, out BoonData boon)) { code = boon.code; return true; }
                break;
            case ECardKind.Deviation:
                if (data.TryGetDeviation((EDeviation)card.Index, out DeviationData deviation)) { code = deviation.code; return true; }
                break;
        }

        return false;
    }

    public static Sprite ResolveIcon(CardRef card)
    {
        if (!TryGetCode(card, out string code))
            return null;

        string address = $"Card_Icon_{code}";
        return App.SystemManager.Asset != null && App.SystemManager.Asset.TryGetAsset(address, out Sprite sprite)
            ? sprite
            : null;
    }
}
