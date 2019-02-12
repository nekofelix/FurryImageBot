using FurryImageBot.Models;
using FurryImageBot.SiteProviders;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace FurryImageBot.Services
{
    public class SiteProviderService
    {
        private readonly ISiteProvider[] SiteProviders;
        private const int RandomMax = 120;
        private const int SubscriptionMax = 60;
        private readonly CloudStorageAccount CloudStorageAccount;
        private readonly CloudTableClient CloudTableClient;
        private Task PollerThread;
        private const int PollerThreadLatency = 1000;
        private const string SubscriptionTableName = "SubscriptionTable";

        public SiteProviderService(IServiceProvider serviceProvider)
        {
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
            List<string> pictures = new List<string>();

            foreach (ISiteProvider siteProvider in SiteProviders)
            {
                List<string> currentPictures = await siteProvider.QueryByTagAsync(query, SubscriptionMax);
                pictures.AddRange(currentPictures);
            }

            string partitionKey = GeneratePartitionKey
            (
                userId: userId, 
                isPrivate: isPrivate,
                channelId: channelId,
                guildId: guildId
            );

            SubscriptionEntity subscriptionEntity = new SubscriptionEntity(partitionKey, query);
            subscriptionEntity.IsPrivate = isPrivate;
            subscriptionEntity.UserId = userId;
            subscriptionEntity.ChannelId = channelId;
            subscriptionEntity.GuildId = guildId;
            subscriptionEntity.QueryCache = new HashSet<string>(pictures);

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

            // Construct the query operation for all customer entities where PartitionKey="Smith".
            TableQuery<SubscriptionEntity> query = new TableQuery<SubscriptionEntity>().Where(TableQuery.GenerateFilterCondition("PartitionKey", QueryComparisons.Equal, partitionKey));

            CloudTable subscriptionTable = CloudTableClient.GetTableReference(SubscriptionTableName);

            // Print the fields for each customer.
            TableContinuationToken token = null;
            do
            {
                TableQuerySegment<SubscriptionEntity> resultSegment = await subscriptionTable.ExecuteQuerySegmentedAsync(query, token);
                token = resultSegment.ContinuationToken;

                foreach (SubscriptionEntity subscriptionEntity in resultSegment.Results)
                {
                    subscriptions.Add(subscriptionEntity.RowKey);
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

            // Execute the operation.
            TableResult retrievedResult = await subscriptionTable.ExecuteAsync(retrieveOperation);

            // Assign the result to a CustomerEntity object.
            SubscriptionEntity subscriptionEntity = (SubscriptionEntity)retrievedResult.Result;

            // Create the Delete TableOperation and then execute it.
            if (subscriptionEntity == null)
            {
                return false;
            }
            else
            {
                TableOperation deleteOperation = TableOperation.Delete(subscriptionEntity);

                // Execute the operation.
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
                List<string> currentPictures = await siteProvider.QueryByTagAsync(query, RandomMax);
                pictures.AddRange(currentPictures);
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
                    CloudTable subscriptionTable = CloudTableClient.GetTableReference(SubscriptionTableName);



                    
               
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
