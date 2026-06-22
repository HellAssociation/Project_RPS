using SystemEnums;

// 통나무: STUB — in 5-player mode, only one player controls per match (single-control keys).
// TODO: requires networked sole-controller assignment (1v5). Not implemented yet.
public class LogEffect : IRuntimeCardEffect
{
    public ECardEffect Id => ECardEffect.Deviation_Log;
}
