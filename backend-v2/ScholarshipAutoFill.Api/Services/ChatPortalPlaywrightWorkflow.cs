using Microsoft.Playwright;

namespace ScholarshipAutoFill.Api.Services;

public interface IChatPortalPlaywrightWorkflow
{
    Task LoginAsync(string targetChatPortalUrl);
    Task<string> ExecuteQueryAsync(string targetChatPortalUrl, string formattedPrompt);
    bool HasSavedSession();
}

public sealed class ChatPortalPlaywrightWorkflow(
    IChatPortalCredentialsReader credentialsReader,
    IConfiguration configuration,
    IWebHostEnvironment environment,
    ILogger<ChatPortalPlaywrightWorkflow> logger) : IChatPortalPlaywrightWorkflow
{
    private const string ChatPromptInputSelector = "textarea, [contenteditable='true'], div[role='textbox'], [aria-label*='message' i], textarea[placeholder*='message' i], textarea[placeholder*='ask' i]";

    private readonly string storageStatePath = Path.Combine(
        environment.ContentRootPath,
        "App_Data",
        "playwright",
        "chat-portal-storage-state.json");

    private readonly string dedicatedChromeProfilePath = Path.Combine(
        environment.ContentRootPath,
        "App_Data",
        "playwright",
        "chrome-profile");

    public async Task LoginAsync(string targetChatPortalUrl)
    {
        ValidateAbsoluteUrl(targetChatPortalUrl, nameof(targetChatPortalUrl));
        var isGoogleSigningEnabled = configuration.GetValue<bool>("isGooglesigningEnabled");
        var credentials = credentialsReader.GetCredentials(requirePassword: !isGoogleSigningEnabled);
        Directory.CreateDirectory(Path.GetDirectoryName(storageStatePath)!);

        try
        {
            using var playwright = await Playwright.CreateAsync();
            if (isGoogleSigningEnabled)
            {
                await using var googleContext = await LaunchGoogleLoginContextAsync(playwright);
                var page = googleContext.Pages.FirstOrDefault() ?? await googleContext.NewPageAsync();
                await page.GotoAsync(targetChatPortalUrl, new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded });
                await LoginWithGoogleAsync(googleContext, page, targetChatPortalUrl);
                await googleContext.StorageStateAsync(new BrowserContextStorageStateOptions { Path = storageStatePath });
                return;
            }

            await using var browser = await LaunchBrowserAsync(playwright);
            var context = await browser.NewContextAsync();
            var passwordPage = await context.NewPageAsync();
            await passwordPage.GotoAsync(targetChatPortalUrl, new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded });
            await LoginWithEmailAndPasswordAsync(passwordPage, credentials.AccountEmail, credentials.AccountPassword);
            await context.StorageStateAsync(new BrowserContextStorageStateOptions { Path = storageStatePath });
        }
        catch (PlaywrightException ex)
        {
            logger.LogError(ex, "Playwright login failed for {TargetChatPortalUrl}", targetChatPortalUrl);
            throw new InvalidOperationException(BuildLoginErrorMessage(ex), ex);
        }
        catch (TimeoutException ex)
        {
            logger.LogError(ex, "Playwright login timed out for {TargetChatPortalUrl}", targetChatPortalUrl);
            throw new InvalidOperationException($"Chat portal login timed out: {ex.Message}", ex);
        }
    }

    public bool HasSavedSession()
    {
        return File.Exists(storageStatePath);
    }

    public async Task<string> ExecuteQueryAsync(string targetChatPortalUrl, string formattedPrompt)
    {
        ValidateAbsoluteUrl(targetChatPortalUrl, nameof(targetChatPortalUrl));
        if (string.IsNullOrWhiteSpace(formattedPrompt))
            throw new ArgumentException("Formatted prompt is required.", nameof(formattedPrompt));

        if (!File.Exists(storageStatePath))
            throw new InvalidOperationException("No authenticated Playwright session found. Call LoginAsync first.");

        try
        {
            using var playwright = await Playwright.CreateAsync();
            await using var browser = await LaunchBrowserAsync(playwright);

            var context = await browser.NewContextAsync(new BrowserNewContextOptions
            {
                StorageStatePath = storageStatePath
            });
            var page = await context.NewPageAsync();
            await page.GotoAsync(targetChatPortalUrl, new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded });
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle, new PageWaitForLoadStateOptions { Timeout = 60000 });

            var beforeText = await page.Locator("body").InnerTextAsync();

            // Generic chat prompt inputs. Supports textarea, contenteditable editors, and ARIA textboxes.
            var promptInput = await FirstVisibleAsync(page, ChatPromptInputSelector, "chat prompt input");
            await SetTextAsync(page, promptInput, formattedPrompt);

            await ClickChatSendAsync(page, promptInput);

            await WaitForResponseToFinishAsync(page, formattedPrompt);
            await context.StorageStateAsync(new BrowserContextStorageStateOptions { Path = storageStatePath });

            return await ExtractResponseTextAsync(page, formattedPrompt);
        }
        catch (PlaywrightException ex)
        {
            logger.LogError(ex, "Playwright query failed for {TargetChatPortalUrl}", targetChatPortalUrl);
            throw new InvalidOperationException($"Chat portal query execution failed: {ex.Message}", ex);
        }
        catch (TimeoutException ex)
        {
            logger.LogError(ex, "Playwright query timed out for {TargetChatPortalUrl}", targetChatPortalUrl);
            throw new InvalidOperationException($"Chat portal query timed out: {ex.Message}", ex);
        }
    }

    private static async Task LoginWithEmailAndPasswordAsync(IPage page, string accountEmail, string accountPassword)
    {
        // Generic login fields used by common chat portals. Tighten these selectors for a known portal if needed.
        var emailInput = await FirstVisibleAsync(page, "input[type='email'], input[name*='email' i], input[autocomplete='username'], input[placeholder*='email' i]", "email input");
        await emailInput.FillAsync(accountEmail);

        var passwordInput = await FirstVisibleAsync(page, "input[type='password'], input[name*='password' i], input[autocomplete='current-password'], input[placeholder*='password' i]", "password input");
        await passwordInput.FillAsync(accountPassword);

        // Generic submit/login buttons. Many portals use either submit buttons, "Continue", "Sign in", or "Log in".
        var submitButton = await FirstVisibleAsync(page, "button[type='submit'], input[type='submit'], button:has-text('Continue'), button:has-text('Sign in'), button:has-text('Log in'), button:has-text('Login')", "login submit button");
        await submitButton.ClickAsync();
    }

    private static Task<IBrowser> LaunchBrowserAsync(IPlaywright playwright)
    {
        var launchOptions = new BrowserTypeLaunchOptions
        {
            Headless = false
        };

        var chromePath = FindInstalledChromePath();
        if (!string.IsNullOrWhiteSpace(chromePath))
            launchOptions.ExecutablePath = chromePath;

        return playwright.Chromium.LaunchAsync(launchOptions);
    }

    private Task<IBrowserContext> LaunchGoogleLoginContextAsync(IPlaywright playwright)
    {
        var chromePath = FindInstalledChromePath();
        Directory.CreateDirectory(dedicatedChromeProfilePath);
        return playwright.Chromium.LaunchPersistentContextAsync(dedicatedChromeProfilePath, new BrowserTypeLaunchPersistentContextOptions
        {
            Headless = false,
            ExecutablePath = string.IsNullOrWhiteSpace(chromePath) ? null : chromePath,
            ChromiumSandbox = true,
            IgnoreDefaultArgs = ["--enable-automation"],
            Args = ["--disable-blink-features=AutomationControlled"]
        });
    }

    private static string? FindInstalledChromePath()
    {
        string?[] candidates =
        [
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Google", "Chrome", "Application", "chrome.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Google", "Chrome", "Application", "chrome.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Google", "Chrome", "Application", "chrome.exe")
        ];

        return candidates.FirstOrDefault(File.Exists);
    }

    private static string BuildLoginErrorMessage(PlaywrightException ex)
    {
        if (ex.Message.Contains("user data directory is already in use", StringComparison.OrdinalIgnoreCase) ||
            ex.Message.Contains("process singleton", StringComparison.OrdinalIgnoreCase) ||
            ex.Message.Contains("profile", StringComparison.OrdinalIgnoreCase))
        {
            return "Chat portal login failed because the app-owned Chrome profile is already open or locked. Close the Chrome window opened by this app, then click Login again.";
        }

        return $"Chat portal login failed: {ex.Message}";
    }

    private static async Task LoginWithGoogleAsync(IBrowserContext context, IPage page, string targetChatPortalUrl)
    {
        // Manual Google mode: open DeepSeek visibly, try the Google button if it is obvious,
        // then let the user finish account selection, password, 2FA, or consent in the browser.
        await TryClickFirstVisibleAsync(
            page,
            "button:has-text('Google'), div[role='button']:has-text('Google'), a:has-text('Google'), [aria-label*='Google' i], [data-provider*='google' i]",
            5000);
        await WaitForManualLoginCompletionAsync(context, targetChatPortalUrl);
    }

    private static async Task<bool> TryClickFirstVisibleAsync(IPage page, string selector, float timeout)
    {
        try
        {
            await page.Locator(selector).First.ClickAsync(new LocatorClickOptions { Timeout = timeout });
            return true;
        }
        catch (Exception ex) when (ex is PlaywrightException or TimeoutException)
        {
            return false;
        }
    }

    private static async Task WaitForManualLoginCompletionAsync(IBrowserContext context, string targetChatPortalUrl)
    {
        var targetHost = new Uri(targetChatPortalUrl).Host;
        var deadline = DateTimeOffset.UtcNow.AddMinutes(5);

        while (DateTimeOffset.UtcNow < deadline)
        {
            foreach (var browserPage in context.Pages)
            {
                if (browserPage.IsClosed)
                    continue;

                if (!HostMatches(browserPage.Url, targetHost))
                    continue;

                if (await HasVisibleChatPromptAsync(browserPage))
                {
                    await browserPage.BringToFrontAsync();
                    await browserPage.WaitForLoadStateAsync(LoadState.NetworkIdle, new PageWaitForLoadStateOptions { Timeout = 60000 });
                    return;
                }
            }

            await Task.Delay(1000);
        }

        throw new InvalidOperationException("DeepSeek login window opened, but login was not completed within 5 minutes. Click Login again and finish Google sign-in in the opened browser window.");
    }

    private static async Task<bool> HasVisibleChatPromptAsync(IPage page)
    {
        try
        {
            return await page.Locator(ChatPromptInputSelector).First.IsVisibleAsync();
        }
        catch (Exception ex) when (ex is PlaywrightException or TimeoutException)
        {
            return false;
        }
    }

    private static bool HostMatches(string pageUrl, string targetHost)
    {
        return Uri.TryCreate(pageUrl, UriKind.Absolute, out var uri) &&
               uri.Host.Contains(targetHost, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<ILocator> FirstVisibleAsync(IPage page, string selector, string description)
    {
        var locator = page.Locator(selector);
        await locator.First.WaitForAsync(new LocatorWaitForOptions
        {
            State = WaitForSelectorState.Attached,
            Timeout = 30000
        });

        var count = await locator.CountAsync();
        for (var index = 0; index < count; index++)
        {
            var candidate = locator.Nth(index);
            if (await candidate.IsVisibleAsync())
                return candidate;
        }

        throw new PlaywrightException($"Could not find a visible {description} using selector: {selector}");
    }

    private static async Task SetTextAsync(IPage page, ILocator locator, string text)
    {
        var tagName = await locator.EvaluateAsync<string>("element => element.tagName.toLowerCase()");
        var isContentEditable = await locator.EvaluateAsync<bool>("element => element.isContentEditable");

        if (tagName is "textarea" or "input")
        {
            await locator.FillAsync(text);
            return;
        }

        if (isContentEditable)
        {
            await locator.ClickAsync();
            await page.Keyboard.InsertTextAsync(text);
            return;
        }

        await locator.FillAsync(text);
    }

    private static async Task ClickChatSendAsync(IPage page, ILocator promptInput)
    {
        // DeepSeek's blue arrow is icon-only, so it often has no "Send" text.
        var semanticSendSelectors = new[]
        {
            "button[type='submit']",
            "button[aria-label*='send' i]",
            "button[aria-label*='submit' i]",
            "button:has-text('Send')",
            "button:has-text('Submit')",
            "[data-testid*='send' i]",
            "div[role='button'][aria-label*='send' i]"
        };

        foreach (var selector in semanticSendSelectors)
        {
            if (await TryClickFirstVisibleAsync(page, selector, 1500))
                return;
        }

        var clickedNearbyButton = await page.EvaluateAsync<bool>(
            """
            (input) => {
              const inputRect = input.getBoundingClientRect();
              const buttons = [...document.querySelectorAll('button')]
                .filter(button => {
                  const rect = button.getBoundingClientRect();
                  const style = window.getComputedStyle(button);
                  return rect.width > 0 &&
                    rect.height > 0 &&
                    style.visibility !== 'hidden' &&
                    style.display !== 'none' &&
                    !button.disabled &&
                    rect.top >= inputRect.top - 30 &&
                    rect.left >= inputRect.left &&
                    rect.left <= inputRect.right + 30;
                })
                .sort((a, b) => {
                  const ar = a.getBoundingClientRect();
                  const br = b.getBoundingClientRect();
                  return (br.left + br.top) - (ar.left + ar.top);
                });

              const button = buttons[0];
              if (!button) return false;
              button.click();
              return true;
            }
            """,
            await promptInput.ElementHandleAsync());

        if (clickedNearbyButton)
            return;

        await promptInput.FocusAsync();
        await page.Keyboard.PressAsync("Enter");
    }

    private static async Task WaitForResponseToFinishAsync(IPage page, string formattedPrompt)
    {
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle, new PageWaitForLoadStateOptions { Timeout = 60000 });

        var deadline = DateTimeOffset.UtcNow.AddMinutes(5);
        string? lastJson = null;
        var stableTicks = 0;
        while (DateTimeOffset.UtcNow < deadline)
        {
            var currentJson = await TryExtractJsonResponseTextAsync(page, formattedPrompt);
            if (!string.IsNullOrWhiteSpace(currentJson))
            {
                if (currentJson == lastJson)
                {
                    stableTicks++;
                    if (stableTicks >= 1 && await HasVisibleChatPromptAsync(page))
                        return;
                }
                else
                {
                    lastJson = currentJson;
                    stableTicks = 0;
                }
            }

            await page.WaitForTimeoutAsync(1500);
        }

        throw new TimeoutException("Timed out waiting for DeepSeek to return a JSON response.");
    }

    private static async Task WaitForLegacyResponseToStabilizeAsync(IPage page)
    {
        await WaitForBusyIndicatorsToDisappearAsync(page);
        await WaitForBodyTextToStabilizeAsync(page);
    }

    private static async Task WaitForBusyIndicatorsToDisappearAsync(IPage page)
    {
        var busy = page.Locator("[aria-busy='true'], [data-testid*='loading' i], [class*='loading' i], [class*='spinner' i]");
        try
        {
            await busy.First.WaitForAsync(new LocatorWaitForOptions
            {
                State = WaitForSelectorState.Hidden,
                Timeout = 30000
            });
        }
        catch (TimeoutException)
        {
            // Not every portal exposes a reliable loading indicator.
        }
    }

    private static async Task WaitForBodyTextToStabilizeAsync(IPage page)
    {
        var previous = await page.Locator("body").InnerTextAsync();
        for (var attempts = 0; attempts < 5; attempts++)
        {
            await page.WaitForTimeoutAsync(1000);
            var current = await page.Locator("body").InnerTextAsync();
            if (current == previous)
                return;

            previous = current;
        }
    }

    private static async Task<string> ExtractResponseTextAsync(IPage page, string formattedPrompt)
    {
        var json = await TryExtractJsonResponseTextAsync(page, formattedPrompt);
        if (!string.IsNullOrWhiteSpace(json))
            return json;

        await WaitForLegacyResponseToStabilizeAsync(page);
        json = await TryExtractJsonResponseTextAsync(page, formattedPrompt);
        if (!string.IsNullOrWhiteSpace(json))
            return json;

        throw new TimeoutException("DeepSeek finished, but no valid-looking JSON response was found.");
    }

    private static async Task<string?> TryExtractJsonResponseTextAsync(IPage page, string formattedPrompt)
    {
        var candidates = page.Locator("[data-message-author-role='assistant'], [data-testid*='response' i], [data-testid*='message' i], [class*='response' i], [class*='message' i]");
        var count = await candidates.CountAsync();

        for (var index = count - 1; index >= 0; index--)
        {
            var candidate = candidates.Nth(index);
            if (!await candidate.IsVisibleAsync())
                continue;

            var text = (await candidate.InnerTextAsync()).Trim();
            var json = ExtractJsonObject(text, formattedPrompt);
            if (!string.IsNullOrWhiteSpace(json))
                return json;
        }

        var bodyText = (await page.Locator("body").InnerTextAsync()).Trim();
        return ExtractJsonObject(bodyText, formattedPrompt);
    }

    private static string? ExtractJsonObject(string text, string formattedPrompt)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        if (text.Contains("You are an Expert University Admission", StringComparison.OrdinalIgnoreCase) &&
            text.Contains(formattedPrompt.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            var afterPromptIndex = text.LastIndexOf(formattedPrompt.Trim(), StringComparison.OrdinalIgnoreCase);
            text = afterPromptIndex >= 0 ? text[(afterPromptIndex + formattedPrompt.Trim().Length)..] : text;
        }

        return ExtractLastBalancedJsonObject(text);
    }

    private static string? ExtractLastBalancedJsonObject(string text)
    {
        var universityNameIndex = text.LastIndexOf("\"university_name\"", StringComparison.OrdinalIgnoreCase);
        if (universityNameIndex < 0)
            return null;

        var start = text.LastIndexOf('{', universityNameIndex);
        if (start < 0)
            return null;

        var depth = 0;
        var inString = false;
        var escaped = false;
        for (var index = start; index < text.Length; index++)
        {
            var character = text[index];
            if (escaped)
            {
                escaped = false;
                continue;
            }

            if (character == '\\' && inString)
            {
                escaped = true;
                continue;
            }

            if (character == '"')
            {
                inString = !inString;
                continue;
            }

            if (inString)
                continue;

            if (character == '{')
                depth++;
            else if (character == '}')
            {
                depth--;
                if (depth == 0)
                    return text[start..(index + 1)].Trim();
            }
        }

        return null;
    }

    private static void ValidateAbsoluteUrl(string value, string parameterName)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))
            throw new ArgumentException("A valid absolute http or https URL is required.", parameterName);
    }
}
