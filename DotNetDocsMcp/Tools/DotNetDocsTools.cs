using System.ComponentModel;
using System.Text.Encodings.Web;
using System.Text.Json;
using ModelContextProtocol;
using ModelContextProtocol.Server;

internal sealed class DotNetDocsTools(DotNetDocsService service)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    [McpServerTool(
        Name = "search_dotnet_docs",
        Title = "Search .NET documentation",
        ReadOnly = true,
        Destructive = false,
        Idempotent = true,
        OpenWorld = true)]
    [Description(
        "Searches the official Microsoft Learn index for current .NET, C#, ASP.NET Core, EF Core, and MAUI documentation. Returns titles, descriptions, update dates, and source URLs.")]
    public async Task<string> SearchDotNetDocsAsync(
        [Description("Search query, such as 'C# 14 field keyword' or 'ASP.NET Core 10 passkeys'.")]
        string query,
        [Description("Maximum number of results from 1 through 10.")]
        int maxResults = 5,
        CancellationToken cancellationToken = default)
    {
        return await InvokeAsync(async () =>
        {
            var result = await service.SearchDotNetDocsAsync(
                query,
                maxResults,
                cancellationToken);
            return JsonSerializer.Serialize(result, JsonOptions);
        });
    }

    [McpServerTool(
        Name = "fetch_dotnet_doc",
        Title = "Fetch .NET documentation",
        ReadOnly = true,
        Destructive = false,
        Idempotent = true,
        OpenWorld = true)]
    [Description(
        "Fetches and cleans one HTTPS page from learn.microsoft.com. Redirects are revalidated, scripts and navigation are removed, and returned page text is marked as untrusted reference content.")]
    public Task<string> FetchDotNetDocAsync(
        [Description("An HTTPS URL whose host is exactly learn.microsoft.com.")]
        string url,
        [Description("Maximum returned document characters from 1,000 through 30,000.")]
        int maxCharacters = 12_000,
        CancellationToken cancellationToken = default) =>
        InvokeAsync(() =>
            service.FetchDotNetDocAsync(url, maxCharacters, cancellationToken));

    [McpServerTool(
        Name = "get_nuget_package",
        Title = "Get NuGet package metadata",
        ReadOnly = true,
        Destructive = false,
        Idempotent = true,
        OpenWorld = true)]
    [Description(
        "Gets current metadata for a package from the official NuGet API. If version is omitted, returns the latest stable version; it never installs or downloads the package.")]
    public async Task<string> GetNuGetPackageAsync(
        [Description("NuGet package ID, for example Microsoft.Extensions.Hosting.")]
        string packageId,
        [Description("Optional exact semantic version. Omit to select the latest stable version.")]
        string? version = null,
        CancellationToken cancellationToken = default)
    {
        return await InvokeAsync(async () =>
        {
            var result = await service.GetNuGetPackageAsync(
                packageId,
                version,
                cancellationToken);
            return JsonSerializer.Serialize(result, JsonOptions);
        });
    }

    [McpServerTool(
        Name = "get_local_dotnet_info",
        Title = "Get local .NET information",
        ReadOnly = true,
        Destructive = false,
        Idempotent = true,
        OpenWorld = false)]
    [Description(
        "Runs the fixed, read-only 'dotnet --info' command and returns installed SDKs, runtimes, workloads, architecture, and operating-system information. It accepts no command arguments.")]
    public Task<string> GetLocalDotNetInfoAsync(
        CancellationToken cancellationToken = default) =>
        InvokeAsync(() => service.GetLocalDotNetInfoAsync(cancellationToken));

    private static async Task<T> InvokeAsync<T>(Func<Task<T>> action)
    {
        try
        {
            return await action();
        }
        catch (McpException)
        {
            throw;
        }
        catch (Exception exception) when (
            exception is ArgumentException
                or InvalidOperationException
                or HttpRequestException
                or TimeoutException)
        {
            throw new McpException(exception.Message, exception);
        }
    }
}
