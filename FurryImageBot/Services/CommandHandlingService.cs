using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Reflection;
using System.Threading.Tasks;

namespace FurryImageBot.Services
{
    public class CommandHandlingService
    {
        private string[] Prefixes;
        private readonly CommandService CommandService;
        private readonly DiscordSocketClient DiscordSocketClient;
        private readonly IServiceProvider ServiceProvider;

        public CommandHandlingService(IServiceProvider serviceProvider)
        {
            CommandService = serviceProvider.GetRequiredService<CommandService>();
            DiscordSocketClient = serviceProvider.GetRequiredService<DiscordSocketClient>();
            ServiceProvider = serviceProvider;

            CommandService.CommandExecuted += CommandExecutedAsync;
            DiscordSocketClient.MessageReceived += MessageReceivedAsync;
        }

        public async Task InitializeAsync(string[] prefixes)
        {
            Prefixes = prefixes;
            await CommandService.AddModulesAsync(Assembly.GetEntryAssembly(), ServiceProvider);
        }

        public async Task MessageReceivedAsync(SocketMessage socketMessage)
        {
            // Ignore system messages, or messages from other bots
            if (!(socketMessage is SocketUserMessage message) || message.Source != MessageSource.User)
            {
                return;
            }

            // This value holds the offset where the prefix ends
            int argPosition = 0;
            if (!PrefixChecker(message, ref argPosition))
            {
                return;
            }

            var context = new SocketCommandContext(DiscordSocketClient, message);
            await CommandService.ExecuteAsync(context, argPosition, ServiceProvider); // we will handle the result in CommandExecutedAsync
        }

        public async Task CommandExecutedAsync(Optional<CommandInfo> command, ICommandContext commandContext, IResult result)
        {
            // command is unspecified when there was a search failure (command not found); we don't care about these errors
            if (!command.IsSpecified)
            {
                return;
            }

            // the command was succesful, we don't care about this result, unless we want to log that a command succeeded.
            if (result.IsSuccess)
            {
                return;
            }

            // the command failed, let's notify the user that something happened.
            await commandContext.Channel.SendMessageAsync($"error: {result.ToString()}");
        }

        private bool PrefixChecker(SocketUserMessage socketUserMessage, ref int argumentPosition)
        {
            foreach (string prefix in Prefixes)
            {
                if (socketUserMessage.HasStringPrefix(prefix, ref argumentPosition))
                {
                    return true;
                } 
            }
            if (socketUserMessage.HasMentionPrefix(DiscordSocketClient.CurrentUser, ref argumentPosition))
            {
                return true;
            } 

            return false;
        }
    }
}
