using Discord;
using Discord.WebSocket;

namespace DotBot;

public class DotBot : IAsyncDisposable
{
    private readonly DiscordSocketClient _client;

    public DotBot()
    {
        _client = new DiscordSocketClient(new DiscordSocketConfig
        {
        });
        _client.Log += Log;
    }

    private Task Log(LogMessage arg)
    {
        // TODO: Implement
        return Task.CompletedTask;
    }

    public async void StartUp(string token)
    {
        await _client.LoginAsync(TokenType.Bot, token);
        await _client.StartAsync();
    }
    

    public async ValueTask DisposeAsync()
    {
        await _client.DisposeAsync();
    }
}