using SystemEnums;

public class ModeData
{
    public static bool IsSingleControl =>
        App.CurrentScene == EScene.PvE_1v1 || App.CurrentScene == EScene.PvP_1v1;
}
