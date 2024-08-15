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


        DotBot? bot;
        await using (bot = new DotBot())
        {
            var success = await bot.Connect(settings.Token);
            if (!success)
            {
                Console.WriteLine("Unable to connect bot to discord.");
                return;
            }

            async void SigIntListener(object? _, ConsoleCancelEventArgs args)
            {
                Console.WriteLine("Shutting down bot...");
                bot.Stop();
            }

            Console.CancelKeyPress += SigIntListener;
            await bot.RunUntilCompletion();
            Console.CancelKeyPress -= SigIntListener;
            Console.WriteLine("Bot has been shut down.");
        }
    }
}