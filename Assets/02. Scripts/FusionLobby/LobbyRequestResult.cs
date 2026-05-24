public readonly struct LobbyRequestResult
{
    public LobbyRequestResult(bool isSuccess, string errorMessage = null)
    {
        IsSuccess = isSuccess;
        ErrorMessage = errorMessage;
    }

    public bool IsSuccess { get; }
    public string ErrorMessage { get; }

    public static LobbyRequestResult Success()
    {
        return new LobbyRequestResult(true);
    }

    public static LobbyRequestResult Fail(string message)
    {
        return new LobbyRequestResult(false, message);
    }
}
