using System.Reflection;
using Discord.Interactions;

namespace DotBot.Modules;

public class GeneralModule : InteractionModuleBase
{
    [SlashCommand("version", "Get the version of the bot.")]
    public async Task Version()
    {
        var assembly = Assembly.GetEntryAssembly();
        await RespondAsync(assembly?.FullName);
    }

    [SlashCommand("ping", "Ping the bot.")]
    public async Task Ping()
    {
        await RespondAsync("Pong!");
    }
}