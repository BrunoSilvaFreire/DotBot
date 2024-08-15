using Discord;
using Discord.WebSocket;

namespace DotBot;

public class DotBot : IAsyncDisposable
{
    private readonly DiscordSocketClient _discordClient;
    private bool _isReady;

    public DotBot()
    {
        _isReady = false;

        _discordClient = new DiscordSocketClient(new DiscordSocketConfig());
        _discordClient.Log += Log;
        _discordClient.Ready += OnDiscordClientBecameReady;
    }

    private Task OnDiscordClientBecameReady()
    {
        _isReady = true;
        return Task.CompletedTask;
    }

    public IDiscordClient DiscordClient => _discordClient;

    private Task Log(LogMessage arg)
    {
        Console.WriteLine($"{arg}");
        return Task.CompletedTask;
    }

    public async Task<bool> Connect(string token)
    {
        await _discordClient.LoginAsync(TokenType.Bot, token);
        switch (_discordClient.LoginState)
        {
            case LoginState.LoggedOut:
            case LoginState.LoggingOut:
                return false;
        }

        await _discordClient.StartAsync();
        await _discordClient.SetStatusAsync(UserStatus.AFK);
        await EnsureIsReady();

        return _isReady;
    }

    private async Task EnsureIsReady()
    {
        while (!_isReady)
        {
            await Task.Delay(100);
        }
    }


    public async ValueTask DisposeAsync()
    {
        await _discordClient.LogoutAsync();
        await _discordClient.StopAsync();
        await _discordClient.DisposeAsync();
    }
}