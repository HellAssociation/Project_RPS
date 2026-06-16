using SystemEnums;

// 칼라: STUB — couples a toggle to the finger above it (another player's finger).
// TODO: requires networked cross-player finger control (1v5). Not implemented yet.
public class CollarEffect : IRuntimeCardEffect
{
    public ECardEffect Id => ECardEffect.Deviation_Collar;
}
