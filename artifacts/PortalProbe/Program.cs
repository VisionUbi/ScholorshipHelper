using Microsoft.Playwright;

using var playwright = await Playwright.CreateAsync();
await using var browser = await playwright.Chromium.LaunchAsync(new() { Headless = true });
var page = await browser.NewPageAsync();
var apiUrls = new List<string>();
page.Response += (_, response) =>
{
    var url = response.Url;
    if (url.Contains("course", StringComparison.OrdinalIgnoreCase) ||
        url.Contains("program", StringComparison.OrdinalIgnoreCase) ||
        url.Contains("application", StringComparison.OrdinalIgnoreCase) ||
        url.Contains("api", StringComparison.OrdinalIgnoreCase))
    {
        apiUrls.Add($"{response.Status} {url}");
    }
};

await page.GotoAsync("https://apply.unimi.it/courses", new() { WaitUntil = WaitUntilState.DOMContentLoaded, Timeout = 60000 });
await page.WaitForTimeoutAsync(1500);
await ClickTextAsync("non-EU residing abroad with foreign qualification (visa app)");
await page.WaitForTimeoutAsync(1500);
await ClickTextAsync("Find programmes");
await page.WaitForLoadStateAsync(LoadState.NetworkIdle, new() { Timeout = 15000 }).ContinueWith(_ => { });
await page.WaitForTimeoutAsync(5000);

var text = await page.Locator("body").InnerTextAsync();
var cards = await page.EvaluateAsync<string[]>(
    """
    () => {
      const anchors = Array.from(document.querySelectorAll('a[href*="/courses/course/"]'));
      return anchors.map(a => {
        const card = a.closest('.card,.item,.segment,.course,.result') || a.parentElement;
        return `${(a.innerText || a.textContent || '').trim()}|${a.href}|${card ? card.innerText.trim().replace(/\s+/g, ' ') : ''}`;
      }).filter(x => x.split('|')[0] && !/^more information$/i.test(x.split('|')[0]) && !/^how to apply$/i.test(x.split('|')[0]));
    }
    """);

Console.WriteLine("TEXT START");
Console.WriteLine(text[..Math.Min(text.Length, 4000)]);
Console.WriteLine("CARDS");
foreach (var card in cards.Take(80)) Console.WriteLine(card);
Console.WriteLine("NETWORK");
foreach (var url in apiUrls.Distinct().Take(120)) Console.WriteLine(url);

async Task ClickTextAsync(string text)
{
    try
    {
        await page.EvaluateAsync<bool>(
            """
            text => {
              const wanted = text.toLowerCase();
              const nodes = Array.from(document.querySelectorAll('button,a,div,span,label'));
              const visible = el => {
                const r = el.getBoundingClientRect();
                const s = getComputedStyle(el);
                return r.width > 0 && r.height > 0 && s.display !== 'none' && s.visibility !== 'hidden';
              };
              const el = nodes.find(x => visible(x) && (x.innerText || x.textContent || '').trim().toLowerCase().includes(wanted));
              if (!el) return false;
              el.click();
              return true;
            }
            """,
            text);
    }
    catch
    {
    }
}
