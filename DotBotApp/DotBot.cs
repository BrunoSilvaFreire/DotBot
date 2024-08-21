using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using DotBot.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace DotBot;

public class DotBot : IAsyncDisposable
{
    private readonly DiscordSocketClient _discordClient;
    private bool _isReady;
    private readonly IHost _host;

    public DotBot()
    {
        _isReady = false;
        _discordClient = new DiscordSocketClient(new DiscordSocketConfig
        {
            GatewayIntents = GatewayIntents.AllUnprivileged
        });
        _discordClient.Log += Log;
        _discordClient.Ready += OnDiscordClientBecameReady;

        var builder = new HostBuilder()
            .ConfigureServices(collection =>
            {
                collection.AddSingleton(_discordClient)
                    .AddSingleton(x => new InteractionService(_discordClient))
                    .AddSingleton<AudioService>()
                    .AddSingleton<QueueService>()
                    .AddSingleton<InteractionHandlerService>();
            });
        _host = builder.Build();
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
        await _discordClient.SetStatusAsync(UserStatus.Online);
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

    private async Task Initialize()
    {
        var services = new[]
        {
            _host.Services.GetRequiredService<InteractionHandlerService>()
        };
        await foreach (var initTask in services.Select(service => service.InitializeAsync()).ToAsyncEnumerable())
        {
            Console.WriteLine($"Initialized service {initTask}");
        }
    }

    public async Task RunUntilCompletion()
    {
        await Initialize();
        await _host.RunAsync();
    }

    public async Task Stop()
    {
        await _host.StopAsync();
    }
}