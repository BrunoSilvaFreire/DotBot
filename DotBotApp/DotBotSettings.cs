using Config.Net;

namespace DotBot;

public interface IDotBotSettings
{
    [Option(Alias = "DOTBOT_TOKEN")]
    string Token { get; }
}