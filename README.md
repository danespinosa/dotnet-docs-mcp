# .NET Docs MCP

A local, read-only Model Context Protocol server for current .NET development
information. It is written in C# for .NET 10 and communicates with MCP hosts
over stdio.

## Tools

| Tool | Description |
| --- | --- |
| `search_dotnet_docs` | Searches the official Microsoft Learn index for .NET, C#, ASP.NET Core, EF Core, and .NET MAUI documentation. |
| `fetch_dotnet_doc` | Fetches and cleans a page from `learn.microsoft.com`, including API member tables. |
| `get_nuget_package` | Returns current NuGet metadata, latest listed stable versions, and deprecation/replacement information. |
| `get_local_dotnet_info` | Runs the fixed read-only `dotnet --info` command with no user-controlled arguments. |

## Security boundaries

- Uses local stdio transport and opens no MCP network port.
- `fetch_dotnet_doc` accepts only HTTPS pages on the exact
  `learn.microsoft.com` host.
- Every redirect is revalidated.
- NuGet calls use fixed `api.nuget.org` endpoints and never download packages.
- Decompressed responses are limited to 2 MB.
- Active and hidden HTML content is removed.
- Fetched pages are marked as untrusted reference content.
- All tools are annotated as read-only and idempotent.
- MCP protocol output uses stdout; logs use stderr.

## Requirements

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- An MCP host that supports local stdio servers, such as LM Studio, VS Code,
  Visual Studio, or GitHub Copilot.

## Build and test

```powershell
dotnet restore .\dotnet-docs-mcp.slnx
dotnet build .\dotnet-docs-mcp.slnx -c Release --no-restore
dotnet run --project .\DotNetDocsMcp.SmokeTests\DotNetDocsMcp.SmokeTests.csproj -c Release --no-build
```

The smoke client launches the server over stdio and calls every tool against
the live Microsoft Learn and NuGet services. It also verifies that a
non-allowlisted URL is rejected with an actionable MCP error.

## Publish

Example self-contained Windows publish:

```powershell
dotnet publish .\DotNetDocsMcp\DotNetDocsMcp.csproj `
  -c Release `
  -r win-x64 `
  --self-contained true `
  -p:PublishSingleFile=true `
  -o .\artifacts\win-x64
```

## Configure LM Studio

Add the published executable to `%USERPROFILE%\.lmstudio\mcp.json`:

```json
{
  "mcpServers": {
    "dotnet-docs": {
      "command": "C:\\path\\to\\DotNetDocsMcp.exe",
      "args": []
    }
  }
}
```

LM Studio's graphical chat can use the configured server directly. LM Studio
requires API authentication when an external HTTP client invokes servers from
`mcp.json`; that token is validated by LM Studio and is not passed to this MCP
server.

## Development

Run from source:

```json
{
  "mcpServers": {
    "dotnet-docs": {
      "command": "dotnet",
      "args": [
        "run",
        "--project",
        "C:\\path\\to\\dotnet-docs-mcp\\DotNetDocsMcp\\DotNetDocsMcp.csproj"
      ]
    }
  }
}
```

## License

MIT
