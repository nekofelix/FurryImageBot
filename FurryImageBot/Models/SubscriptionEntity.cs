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

        public string UserId { get; set; }

        public string ChannelId { get; set; }

        public string GuildId { get; set; }

        public string QueryCache { get; set; }

        public string Query { get { return this.RowKey; } }
    }
}
