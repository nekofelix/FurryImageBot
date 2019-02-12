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
    }
}
