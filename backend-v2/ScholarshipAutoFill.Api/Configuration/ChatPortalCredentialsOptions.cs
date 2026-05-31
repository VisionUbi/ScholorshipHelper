namespace ScholarshipAutoFill.Api.Configuration;

public sealed class ChatPortalCredentialsOptions
{
    public const string SectionName = "ChatPortalCredentials";

    public string AccountEmail { get; init; } = "";
    public string AccountPassword { get; init; } = "";
}
