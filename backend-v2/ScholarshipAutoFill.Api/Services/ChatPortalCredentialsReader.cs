using Microsoft.Extensions.Options;
using ScholarshipAutoFill.Api.Configuration;

namespace ScholarshipAutoFill.Api.Services;

public interface IChatPortalCredentialsReader
{
    ChatPortalCredentialsOptions GetCredentials(bool requirePassword = true);
}

public sealed class ChatPortalCredentialsReader(IOptions<ChatPortalCredentialsOptions> options) : IChatPortalCredentialsReader
{
    public ChatPortalCredentialsOptions GetCredentials(bool requirePassword = true)
    {
        var credentials = options.Value;
        if (string.IsNullOrWhiteSpace(credentials.AccountEmail))
            throw new InvalidOperationException("Chat portal AccountEmail is missing from appsettings.json.");

        if (requirePassword && string.IsNullOrWhiteSpace(credentials.AccountPassword))
            throw new InvalidOperationException("Chat portal AccountPassword is missing from appsettings.json.");

        return credentials;
    }
}
