using Discord;
using Discord.WebSocket;
using FurryImageBot.Models;
using FurryImageBot.SiteProviders;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FurryImageBot.Services
{
    public class SiteProviderService
    {
        private readonly ISiteProvider[] SiteProviders;
        private const int RandomMax = 120;
        private const int SubscriptionMax = 72;
        private const int CacheMax = 300;
        private readonly CloudStorageAccount CloudStorageAccount;
        private readonly CloudTableClient CloudTableClient;
        private Task PollerThread;
        private const int PollerThreadLatency = 10000;
        private const string SubscriptionTableName = "SubscriptionTable";
        private const int MaxReplyLines = 5;
        private DiscordSocketClient DiscordSocketClient;

        public SiteProviderService(IServiceProvider serviceProvider)
        {
            DiscordSocketClient = serviceProvider.GetRequiredService<DiscordSocketClient>();
            SiteProviders = serviceProvider.GetRequiredService<ISiteProvider[]>();
            CloudStorageAccount = new CloudStorageAccount
                (
                    storageCredentials: new Microsoft.WindowsAzure.Storage.Auth.StorageCredentials
                    (
                        accountName: Environment.GetEnvironmentVariable("STORAGE_ACCOUNT_NAME"),
                        keyValue: Environment.GetEnvironmentVariable("STORAGE_ACCOUNT_KEY")
                    ), 
                    useHttps: true
                );
            CloudTableClient = CloudStorageAccount.CreateCloudTableClient();
        }

        public async Task<bool> Subscribe(string query, ulong userId, bool isPrivate, ulong channelId, ulong guildId)
        {
            string partitionKey = GeneratePartitionKey
            (
                userId: userId, 
                isPrivate: isPrivate,
                channelId: channelId,
                guildId: guildId
            );

            SubscriptionEntity subscriptionEntity = new SubscriptionEntity(partitionKey, query);
            subscriptionEntity.IsPrivate = isPrivate;
            subscriptionEntity.UserId = userId.ToString();
            subscriptionEntity.ChannelId = channelId.ToString();
            subscriptionEntity.GuildId = guildId.ToString();
            subscriptionEntity.CacheFilled = false;

            TableOperation insertOperation = TableOperation.Insert(subscriptionEntity);

            CloudTable subscriptionTable = CloudTableClient.GetTableReference(SubscriptionTableName);
            await subscriptionTable.ExecuteAsync(insertOperation);

            return true;
        }

        public async Task<List<string>> List(ulong userId, bool isPrivate, ulong channelId, ulong guildId)
        {
            List<string> subscriptions = new List<string>();

            string partitionKey = GeneratePartitionKey
            (
                userId: userId,
                isPrivate: isPrivate,
                channelId: channelId,
                guildId: guildId
            );

            TableQuery<SubscriptionEntity> query = new TableQuery<SubscriptionEntity>().Where(TableQuery.GenerateFilterCondition("PartitionKey", QueryComparisons.Equal, partitionKey));

            CloudTable subscriptionTable = CloudTableClient.GetTableReference(SubscriptionTableName);

            TableContinuationToken token = null;
            do
            {
                TableQuerySegment<SubscriptionEntity> resultSegment = await subscriptionTable.ExecuteQuerySegmentedAsync(query, token);
                token = resultSegment.ContinuationToken;

                foreach (SubscriptionEntity subscriptionEntity in resultSegment.Results)
                {
                    subscriptions.Add(subscriptionEntity.Query);
                }
            } while (token != null);

            return subscriptions;
        }

        public async Task<bool> Remove(string query, ulong userId, bool isPrivate, ulong channelId, ulong guildId)
        {
            string partitionKey = GeneratePartitionKey
            (
                userId: userId,
                isPrivate: isPrivate,
                channelId: channelId,
                guildId: guildId
            );

            CloudTable subscriptionTable = CloudTableClient.GetTableReference(SubscriptionTableName);
            TableOperation retrieveOperation = TableOperation.Retrieve<SubscriptionEntity>(partitionKey, query);

            TableResult retrievedResult = await subscriptionTable.ExecuteAsync(retrieveOperation);

            SubscriptionEntity subscriptionEntity = (SubscriptionEntity)retrievedResult.Result;

            if (subscriptionEntity == null)
            {
                return false;
            }
            else
            {
                TableOperation deleteOperation = TableOperation.Delete(subscriptionEntity);

                await subscriptionTable.ExecuteAsync(deleteOperation);

                return true;
            }
        }

        public async Task InitializeAsync()
        {
            CloudTable subscriptionTable = CloudTableClient.GetTableReference(SubscriptionTableName);
            await subscriptionTable.CreateIfNotExistsAsync();

            PollerThread = Task.Factory.StartNew(() => this.PollerLoop(), TaskCreationOptions.LongRunning);
        }

        public async Task<string> GetRandomPicture(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return "Query is empty";
            }

            List<string> pictures = new List<string>();

            foreach (ISiteProvider siteProvider in SiteProviders)
            {
                try
                {
                    List<string> currentPictures = await siteProvider.QueryByTagAsync(query, RandomMax);
                    pictures.AddRange(currentPictures);
                }
                catch (Exception) { }
            }

            if (pictures.Count == 0)
            {
                return "No posts matched your search.";
            } 
          
            string randomPicture = pictures[RandomThreadSafe.Next(0, pictures.Count)];
            return randomPicture;
        }

        private async Task PollerLoop()
        {
            while (true)
            {
                try
                {
                    List<SubscriptionEntity> subscriptions = new List<SubscriptionEntity>();

                    CloudTable subscriptionTable = CloudTableClient.GetTableReference(SubscriptionTableName);

                    TableQuery<SubscriptionEntity> query = new TableQuery<SubscriptionEntity>();

                    TableContinuationToken token = null;
                    do
                    {
                        TableQuerySegment<SubscriptionEntity> resultSegment = await subscriptionTable.ExecuteQuerySegmentedAsync(query, token);
                        token = resultSegment.ContinuationToken;
                        subscriptions.AddRange(resultSegment.Results);
                    } while (token != null);

                    foreach (SubscriptionEntity subscriptionEntity in subscriptions)
                    {
                        try
                        {
                            if (subscriptionEntity.CacheFilled)
                            {
                                List<List<string>> pictureListList = new List<List<string>>();
                                foreach (ISiteProvider siteProvider in SiteProviders)
                                {
                                    try
                                    {
                                        List<string> currentPictures = await siteProvider.QueryByTagAsync(subscriptionEntity.Query, SubscriptionMax);
                                        pictureListList.Add(currentPictures);
                                    }
                                    catch (Exception) { }
                                }
                                List<string> interleavedPictureList = pictureListList.Interleave().ToList();

                                HashSet<string> pictureSet = new HashSet<string>(interleavedPictureList);
                                List<string> currentCache = JsonConvert.DeserializeObject<List<string>>(subscriptionEntity.QueryCache);
                                pictureSet.ExceptWith(currentCache);
                                if (pictureSet.Count != 0)
                                {
                                    List<string> newCache = interleavedPictureList.Concat(currentCache).Take(CacheMax * SiteProviders.Count()).ToList();
                                    subscriptionEntity.QueryCache = JsonConvert.SerializeObject(newCache);
                                    TableOperation insertOperation = TableOperation.Merge(subscriptionEntity);
                                    await subscriptionTable.ExecuteAsync(insertOperation);
                                    await SendUpdates(subscriptionEntity, pictureSet);
                                }
                            }
                            else
                            {
                                subscriptionEntity.CacheFilled = true;

                                List<List<string>> pictureListList = new List<List<string>>();
                                foreach (ISiteProvider siteProvider in SiteProviders)
                                {
                                    try
                                    {
                                        List<string> currentPictures = await siteProvider.QueryByTagAsync(subscriptionEntity.Query, SubscriptionMax);
                                        pictureListList.Add(currentPictures);
                                    }
                                    catch (Exception) { }
                                }
                                List<string> interleavedPictureList = pictureListList.Interleave().ToList();

                                subscriptionEntity.QueryCache = JsonConvert.SerializeObject(interleavedPictureList);
                                TableOperation insertOperation = TableOperation.Merge(subscriptionEntity);
                                await subscriptionTable.ExecuteAsync(insertOperation);
                            }
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine(e);
                        }
                        finally
                        {
                            await Task.Delay(PollerThreadLatency);
                        }
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }
                finally
                {
                    await Task.Delay(PollerThreadLatency);
                }
            }
        }

        private async Task SendUpdates(SubscriptionEntity subscriptionEntity, HashSet<string> newPictures)
        {
            try
            {
                if (newPictures.Count == 0)
                {
                    return;
                }

                string replyString = $"Subscription Update for [{subscriptionEntity.Query}]!";
                int counter = 1;
                foreach (string newPicture in newPictures)
                {
                    replyString = $"{replyString}\n{newPicture}";
                    counter++;
                    if (counter >= MaxReplyLines)
                    {
                        if (subscriptionEntity.IsPrivate)
                        {
                            IDMChannel channel = await DiscordSocketClient.GetUser(Convert.ToUInt64(subscriptionEntity.UserId)).GetOrCreateDMChannelAsync();
                            await channel.SendMessageAsync(replyString);
                        }
                        else
                        {
                            await DiscordSocketClient.GetGuild(Convert.ToUInt64(subscriptionEntity.GuildId)).GetTextChannel(Convert.ToUInt64(subscriptionEntity.ChannelId)).SendMessageAsync(replyString);
                        }
                        replyString = "";
                        counter = 0;
                    }
                }

                if (!String.IsNullOrWhiteSpace(replyString))
                {
                    if (subscriptionEntity.IsPrivate)
                    {
                        IDMChannel channel = await DiscordSocketClient.GetUser(Convert.ToUInt64(subscriptionEntity.UserId)).GetOrCreateDMChannelAsync();
                        await channel.SendMessageAsync(replyString);
                    }
                    else
                    {
                        await DiscordSocketClient.GetGuild(Convert.ToUInt64(subscriptionEntity.GuildId)).GetTextChannel(Convert.ToUInt64(subscriptionEntity.ChannelId)).SendMessageAsync(replyString);
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }

        private string GeneratePartitionKey(ulong userId, bool isPrivate, ulong channelId, ulong guildId)
        {
            if (isPrivate)
            {
                return $"{isPrivate}+{userId}";
            }
            else
            {
                return $"{isPrivate}+{guildId}+{channelId}";
            }
        }
    }
}
