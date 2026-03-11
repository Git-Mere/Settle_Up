public sealed record ApiErrorResponse(
    string Error,
    string Message)
{
    public static ApiErrorResponse InvalidRequest(string message) => new("InvalidRequest", message);

    public static ApiErrorResponse DraftNotFound(string message) => new("DraftNotFound", message);

    public static ApiErrorResponse UnexpectedServerError(string message) => new("UnexpectedServerError", message);
}
