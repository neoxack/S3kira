namespace s3kira;

internal readonly struct HeaderValues
{
    public string Host { get; }
    public string XAmzContentSha { get; }
    public string XAmzDate { get; }

    public HeaderValues(string host, string xAmzContentSha, string xAmzDate)
    {
        Host = host;
        XAmzContentSha = xAmzContentSha;
        XAmzDate = xAmzDate;
    }
}