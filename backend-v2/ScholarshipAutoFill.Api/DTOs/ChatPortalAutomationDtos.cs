namespace ScholarshipAutoFill.Api.DTOs;

public sealed record ChatPortalLoginRequest(string TargetChatPortalUrl);

public sealed record ChatPortalLoginResponse(bool Success, string Message);

public sealed record ChatPortalSessionStatusResponse(bool IsLoggedIn);

public sealed record ChatPortalPromptRequest(string UniversityUrl);

public sealed record ChatPortalPromptResponse(string Prompt);

public sealed record ChatPortalQueryRequest(string TargetChatPortalUrl, string UniversityUrl);

public sealed record ChatPortalQueryResponse(string Prompt, string Result);
