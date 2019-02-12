using Discord;
using Discord.Commands;
using FurryImageBot.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace FurryImageBot.Modules
{
    // Modules must be public and inherit from an IModuleBase
    public class PublicModule : ModuleBase<SocketCommandContext>
    {
        private const int MaxReplyLines = 5;

        // Dependency Injection will fill this value in for us
        public SiteProviderService SiteProviderService { get; set; }

        [Command("random", RunMode = RunMode.Async)]
        [Alias("rand")]
        public async Task Random([Remainder] string args = null)
        {
            using (IDisposable x = Context.Channel.EnterTypingState())
            {
                string RandomPicture = await SiteProviderService.GetRandomPicture(args);
                await ReplyAsync(RandomPicture);
            }
        }

        [Command("Subscribe", RunMode = RunMode.Async)]
        [Alias("sub")]
        public async Task Subscribe([Remainder] string args = null)
        {
            using (IDisposable x = Context.Channel.EnterTypingState())
            {
                bool isSuccessful = await SiteProviderService.Subscribe
                (
                    query: args.Trim(),
                    userId: Context.User.Id,
                    isPrivate: Context.IsPrivate,
                    channelId: Context.IsPrivate ? 0 : Context.Channel.Id,
                    guildId: Context.IsPrivate ? 0 : Context.Guild.Id
                );

                if (isSuccessful)
                {
                    await ReplyAsync($"<@!{Context.User.Id}>, you have successfully subscribed with the Search Query: [{args}]");
                }
                else
                {
                    await ReplyAsync($"<@!{Context.User.Id}>, you have FAILED to subscribe with the Search Query: [{args}]");
                }
            }
        }

        [Command("list", RunMode = RunMode.Async)]
        [Alias( "ls")]
        public async Task List()
        {
            using (IDisposable x = Context.Channel.EnterTypingState())
            {
                List<string> subscriptions = await SiteProviderService.List
                (
                    userId: Context.User.Id,
                    isPrivate: Context.IsPrivate,
                    channelId: Context.IsPrivate ? 0 : Context.Channel.Id,
                    guildId: Context.IsPrivate ? 0 : Context.Guild.Id
                );

                int printCounter = 1;

                string replyString = "";
                int counter = 0;
                foreach (string subscription in subscriptions)
                {
                    replyString = $"{replyString}\n{printCounter}. {subscription}";
                    printCounter++;
                    counter++;
                    if (counter >= MaxReplyLines)
                    {
                        await ReplyAsync(replyString);
                        replyString = "";
                        counter = 0;
                    }
                }

                if (!String.IsNullOrWhiteSpace(replyString))
                {
                    await ReplyAsync(replyString);
                }

                if (subscriptions.Count == 0)
                {
                    await ReplyAsync("There are 0 Subscriptions in this Channel.");
                }
            }
        }

        [Command("remove", RunMode = RunMode.Async)]
        [Alias("rm")]
        public async Task Remove([Remainder] string args = null)
        {
            using (IDisposable x = Context.Channel.EnterTypingState())
            {
                bool isSuccessful = await SiteProviderService.Remove
                (
                    query: args.Trim(),
                    userId: Context.User.Id,
                    isPrivate: Context.IsPrivate,
                    channelId: Context.IsPrivate ? 0 : Context.Channel.Id,
                    guildId: Context.IsPrivate ? 0 : Context.Guild.Id
                );
                if (isSuccessful)
                {
                    await ReplyAsync("Subscription successfully deleted!");
                }
                else
                {
                    await ReplyAsync("No subscription matching that query found!");
                }
            }
        }

        [Command("Help")]
        [Alias("help")]
        public async Task Help([Remainder] string args = null)
        {
            using (IDisposable x = Context.Channel.EnterTypingState())
            {
                EmbedBuilder builder = new EmbedBuilder();

                builder.AddField("rand SEARCH", "Search for a random picture associated with the SEARCH.");
                builder.AddField("sub SEARCH", "Subscribe to a particular SEARCH, and get new posts as they come in. These updates will be given in the channel where the Subscription was created.");
                builder.AddField("ls", "List all current Subscriptions for the current channel.");
                builder.AddField("rm SEARCH", "Remove a particular Subscription from the current channel.");
                await ReplyAsync("", false, builder.Build());
            }
        }
    }
}
