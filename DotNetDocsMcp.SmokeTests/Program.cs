using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using System.Text.Json;

var serverProject = Path.GetFullPath(
    Path.Combine(
        AppContext.BaseDirectory,
        "..",
        "..",
        "..",
        "..",
        "DotNetDocsMcp",
        "DotNetDocsMcp.csproj"));

string command;
IList<string> arguments;
string workingDirectory;

if (args is ["--server-exe", var serverExecutable])
{
    command = Path.GetFullPath(serverExecutable);
    arguments = [];
    workingDirectory = Path.GetDirectoryName(command)
        ?? throw new InvalidOperationException(
            "The published server executable has no parent directory.");
}
else
{
    command = "dotnet";
    arguments =
    [
        "run",
        "--no-build",
        "--project",
        serverProject
    ];
    workingDirectory = Path.GetDirectoryName(serverProject)
        ?? throw new InvalidOperationException(
            "The server project has no parent directory.");
}

var transport = new StdioClientTransport(new StdioClientTransportOptions
{
    Name = "DotNetDocsMcp smoke test",
    Command = command,
    Arguments = arguments,
    WorkingDirectory = workingDirectory,
    StandardErrorLines = line => Console.Error.WriteLine($"[server] {line}")
});

using var testTimeout = new CancellationTokenSource(TimeSpan.FromMinutes(3));
var cancellationToken = testTimeout.Token;
await using var client = await McpClient.CreateAsync(
    transport,
    cancellationToken: cancellationToken);
var tools = await client.ListToolsAsync(cancellationToken: cancellationToken);
var expectedTools = new[]
{
    "search_dotnet_docs",
    "fetch_dotnet_doc",
    "get_nuget_package",
    "get_local_dotnet_info"
};

foreach (var expectedTool in expectedTools)
{
    if (!tools.Any(tool => tool.Name == expectedTool))
    {
        throw new InvalidOperationException(
            $"Expected MCP tool '{expectedTool}' was not registered.");
    }
}

Console.WriteLine(
    $"Registered tools: {string.Join(", ", tools.Select(tool => tool.Name).Order())}");

var searchResult = await CallAsync(
    client,
    "search_dotnet_docs",
    new Dictionary<string, object?>
    {
        ["query"] = "C# 14 field keyword",
        ["maxResults"] = 3
    },
    cancellationToken);
using (var searchDocument = JsonDocument.Parse(searchResult))
{
    var searchRoot = searchDocument.RootElement;
    if (searchRoot.GetProperty("resultCount").GetInt32() <= 0)
    {
        throw new InvalidOperationException(
            "Documentation search returned no results.");
    }

    var hasLearnResult = searchRoot.GetProperty("results")
        .EnumerateArray()
        .Any(result =>
            result.GetProperty("url").GetString()?.StartsWith(
                "https://learn.microsoft.com/",
                StringComparison.OrdinalIgnoreCase) is true);
    if (!hasLearnResult)
    {
        throw new InvalidOperationException(
            "Documentation search returned no Microsoft Learn URLs.");
    }
}

var fetchResult = await CallAsync(
    client,
    "fetch_dotnet_doc",
    new Dictionary<string, object?>
    {
        ["url"] = "https://learn.microsoft.com/en-us/dotnet/csharp/whats-new/csharp-14",
        ["maxCharacters"] = 5_000
    },
    cancellationToken);
RequireContains(fetchResult, "SECURITY NOTICE", "documentation fetch security label");
RequireContains(fetchResult, "field", "documentation fetch");

var apiFetchResult = await CallAsync(
    client,
    "fetch_dotnet_doc",
    new Dictionary<string, object?>
    {
        ["url"] = "https://learn.microsoft.com/en-us/dotnet/api/system.net.http.httpclient?view=net-10.0",
        ["maxCharacters"] = 30_000
    },
    cancellationToken);
RequireContains(apiFetchResult, "BaseAddress", "API member table extraction");
RequireContains(apiFetchResult, "SendAsync", "API member table extraction");

var packageResult = await CallAsync(
    client,
    "get_nuget_package",
    new Dictionary<string, object?>
    {
        ["packageId"] = "Microsoft.Extensions.Hosting"
    },
    cancellationToken);
using (var packageDocument = JsonDocument.Parse(packageResult))
{
    var packageRoot = packageDocument.RootElement;
    RequireJsonText(packageRoot, "id", "NuGet package ID");
    RequireJsonText(packageRoot, "version", "NuGet package version");
    RequireJsonText(packageRoot, "published", "NuGet published date");
}

var deprecatedPackageResult = await CallAsync(
    client,
    "get_nuget_package",
    new Dictionary<string, object?>
    {
        ["packageId"] = "Microsoft.Azure.ServiceBus",
        ["version"] = "5.2.0"
    },
    cancellationToken);
using (var deprecatedDocument = JsonDocument.Parse(deprecatedPackageResult))
{
    var deprecatedRoot = deprecatedDocument.RootElement;
    if (!deprecatedRoot.GetProperty("deprecated").GetBoolean())
    {
        throw new InvalidOperationException(
            "Known deprecated NuGet package was not marked deprecated.");
    }

    if (deprecatedRoot.GetProperty("deprecationReasons").GetArrayLength() == 0)
    {
        throw new InvalidOperationException(
            "Deprecated NuGet package returned no deprecation reasons.");
    }
}

var dotnetInfoResult = await CallAsync(
    client,
    "get_local_dotnet_info",
    new Dictionary<string, object?>(),
    cancellationToken);
RequireContains(dotnetInfoResult, ".NET SDK", "local dotnet information");

var rejectedResult = await client.CallToolAsync(
    "fetch_dotnet_doc",
    new Dictionary<string, object?>
    {
        ["url"] = "https://example.com/",
        ["maxCharacters"] = 2_000
    },
    cancellationToken: cancellationToken);
if (rejectedResult.IsError is not true)
{
    throw new InvalidOperationException(
        "The URL allowlist test unexpectedly accepted example.com.");
}

var rejectionText = GetText(rejectedResult);
RequireContains(
    rejectionText,
    "learn.microsoft.com",
    "allowlist rejection message");

Console.WriteLine("Allowlist rejection: passed");
Console.WriteLine("All MCP smoke tests passed.");

static async Task<string> CallAsync(
    McpClient client,
    string toolName,
    IReadOnlyDictionary<string, object?> arguments,
    CancellationToken cancellationToken)
{
    var result = await client.CallToolAsync(
        toolName,
        arguments,
        cancellationToken: cancellationToken);

    if (result.IsError is true)
    {
        var errorText = GetText(result);
        throw new InvalidOperationException(
            $"Tool '{toolName}' returned an error: {errorText}");
    }

    var text = GetText(result);
    if (string.IsNullOrWhiteSpace(text))
    {
        throw new InvalidOperationException(
            $"Tool '{toolName}' returned no text content.");
    }

    Console.WriteLine($"{toolName}: passed ({text.Length:N0} characters)");
    return text;
}

static string GetText(CallToolResult result) =>
    string.Join(
        Environment.NewLine,
        result.Content.OfType<TextContentBlock>().Select(block => block.Text));

static void RequireJsonText(
    JsonElement element,
    string propertyName,
    string testName)
{
    if (!element.TryGetProperty(propertyName, out var property)
        || property.ValueKind != JsonValueKind.String
        || string.IsNullOrWhiteSpace(property.GetString()))
    {
        throw new InvalidOperationException(
            $"{testName} was missing or empty.");
    }
}

static void RequireContains(
    string value,
    string expected,
    string testName)
{
    if (!value.Contains(expected, StringComparison.OrdinalIgnoreCase))
    {
        throw new InvalidOperationException(
            $"{testName} did not contain expected text '{expected}'.");
    }
}
