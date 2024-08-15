using Config.Net;
using DotBot.Utility;

namespace DotBot;

public static class Program
{
    static async Task Main(string[] args)
    {
        var settings = new ConfigurationBuilder<IDotBotSettings>()
            .UseDotEnvFile()
            .Build();

        if (settings.Token.IsNullOrEmpty())
        {
            Console.WriteLine("Unable to load token.");
            return;
        }

        await using (var bot = new DotBot())
        {
            bool success = await bot.Connect(settings.Token);
            if (!success)
            {
                Console.WriteLine("Unable to connect bot to discord.");
                return;
            }

            await Task.Delay(TimeSpan.FromSeconds(5));
        }
    }
}