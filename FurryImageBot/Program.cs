using Discord;
using Discord.Commands;
using Discord.WebSocket;
using FurryImageBot.Services;
using FurryImageBot.SiteProviders;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace FurryImageBot
{
    public class Program
    {
        static void Main(string[] args)
            => new Program().MainAsync().GetAwaiter().GetResult();

        public async Task MainAsync()
        {
            using (var services = ConfigureServices())
            {
                DiscordSocketClient client = services.GetRequiredService<DiscordSocketClient>();

                client.Log += LogAsync;
                services.GetRequiredService<CommandService>().Log += LogAsync;

                await client.LoginAsync(TokenType.Bot, Environment.GetEnvironmentVariable("DISCORDTOKEN"));
                await client.SetGameAsync(Environment.GetEnvironmentVariable("GAMESTATUS"));
                await client.StartAsync();

                await services.GetRequiredService<CommandHandlingService>().InitializeAsync(Environment.GetEnvironmentVariable("PREFIXES").Split(","));

                await Task.Delay(-1);
            }
        }

        private Task LogAsync(LogMessage log)
        {
            Console.WriteLine(log.ToString());

            return Task.CompletedTask;
        }

        private ServiceProvider ConfigureServices()
        {
            E621SiteProvider e621SiteProvider = new E621SiteProvider();
            FurAffinitySiteProvider furAffinitySiteProvider = new FurAffinitySiteProvider();
            ISiteProvider[] siteProviders = { e621SiteProvider, furAffinitySiteProvider };

            return new ServiceCollection()
                .AddSingleton<DiscordSocketClient>()
                .AddSingleton<CommandService>()
                .AddSingleton<CommandHandlingService>()
                .AddSingleton<HttpClient>()
                .AddSingleton(siteProviders)
                .AddSingleton<SiteProviderService>()
                .BuildServiceProvider();
        }
    }
}
