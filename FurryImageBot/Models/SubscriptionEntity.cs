using Microsoft.WindowsAzure.Storage.Table;
using System;
using System.Collections.Generic;
using System.Text;

namespace FurryImageBot.Models
{
    public class SubscriptionEntity : TableEntity
    {
        public SubscriptionEntity(string partitionKey, string query)
        {
            this.PartitionKey = partitionKey;
            this.RowKey = query;
        }

        public SubscriptionEntity() { }

        public bool IsPrivate { get; set; }

        public ulong UserId { get; set; }

        public ulong ChannelId { get; set; }

        public ulong GuildId { get; set; }

        public HashSet<string> QueryCache { get; set; }
    }
}
