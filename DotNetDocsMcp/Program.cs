using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Http.Headers;

var builder = Host.CreateApplicationBuilder(args);

// Configure all logs to go to stderr (stdout is used for the MCP protocol messages).
builder.Logging.ClearProviders();
builder.Logging.AddConsole(o => o.LogToStandardErrorThreshold = LogLevel.Trace);
builder.Logging.SetMinimumLevel(LogLevel.Information);

builder.Services.AddSingleton(_ =>
{
    var handler = new SocketsHttpHandler
    {
        AllowAutoRedirect = false,
        AutomaticDecompression = DecompressionMethods.All,
        ConnectTimeout = TimeSpan.FromSeconds(10),
        PooledConnectionLifetime = TimeSpan.FromMinutes(10)
    };

    var client = new HttpClient(handler)
    {
        Timeout = TimeSpan.FromSeconds(20)
    };
    client.DefaultRequestHeaders.UserAgent.Add(
        new ProductInfoHeaderValue("DotNetDocsMcp", "1.0"));
    client.DefaultRequestHeaders.Accept.Add(
        new MediaTypeWithQualityHeaderValue("application/json"));
    client.DefaultRequestHeaders.Accept.Add(
        new MediaTypeWithQualityHeaderValue("text/html"));
    return client;
});
builder.Services.AddSingleton<DotNetDocsService>();

// Add the MCP services: the transport to use (stdio) and the tools to register.
builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithTools<DotNetDocsTools>();

await builder.Build().RunAsync();
