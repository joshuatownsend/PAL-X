using System.ComponentModel;
using Spectre.Console.Cli;

namespace Pal.Cli.Commands.Remote;

public abstract class RemoteSettings : CommandSettings
{
    [CommandOption("--api")]
    [Description("Base URL of the PAL API server")]
    [DefaultValue("http://localhost:8080")]
    public string ApiBase { get; init; } = "http://localhost:8080";

    public HttpClient CreateClient() => new() { BaseAddress = new Uri(ApiBase.TrimEnd('/') + "/") };
}
