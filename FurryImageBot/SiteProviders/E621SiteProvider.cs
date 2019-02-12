using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace FurryImageBot.SiteProviders
{
    public class E621SiteProvider : ISiteProvider
    {
        private const int MaximumNumberPosts = 300;
        private HttpClient HttpClient;
        private SemaphoreSlim E621ThrottleGuard;
        private const int ThrottleTiming = 1000;

        public E621SiteProvider()
        {
            HttpClient = new HttpClient();
            HttpClient.DefaultRequestHeaders.Add("User-Agent", Environment.GetEnvironmentVariable("E621_USER_AGENT_HEADER"));
            E621ThrottleGuard = new SemaphoreSlim(1);
        }

        public async Task<List<string>> QueryByTagAsync(string query, int maxPosts)
        {
            List<string> E6Posts = new List<string>();
            {
                int counter = 0;
                int currentPage = 1;
                while (counter < maxPosts)
                {
                    int tempMax = Math.Min(maxPosts - counter, MaximumNumberPosts);
                    List<string> CurrentE6Posts = await QueryE6ByTag(query, tempMax, currentPage);
                    counter += CurrentE6Posts.Count();
                    currentPage++;

                    E6Posts.AddRange(CurrentE6Posts);

                    if (CurrentE6Posts.Count == 0)
                    {
                        break;
                    }
                }
            }

            List<string> Posts = E6Posts.Select(x => $"https://e621.net/post/show/{x}").ToList();
            return Posts;
        }

        // Query the GET e6 Posts List API
        private async Task<List<string>> QueryE6ByTag(string query, int maxPosts, int pageNumber)
        {
            string queryString = $"https://e621.net/post/index.json?tags={query}&limit={maxPosts}&page={pageNumber}";
            List<string> E6Posts = await GetE6(queryString);
            return E6Posts;
        }

        // GET from e6 API.
        private async Task<List<string>> GetE6(string url)
        {
            List<string> e6Posts;
            using (Stream stream = await E6GetStreamAsync(url))
            using (StreamReader streamReader = new StreamReader(stream))
            using (JsonReader jsonReader = new JsonTextReader(streamReader))
            {
                JsonSerializer jsonSerializer = new JsonSerializer();
                IEnumerable<E6Post> e6PostJson = await Task.Run(() => jsonSerializer.Deserialize<IEnumerable<E6Post>>(jsonReader));
                e6Posts = e6PostJson.Select(x => x.Id.ToString()).ToList();
            }
            return e6Posts;
        }

        // Returns a Stream from the given e6 URL. 
        // The caller must Dispose of this Stream.
        // This method takes care of Throttling, to ensure that no more than 1 call per second
        // is made to the e6 servers.
        private async Task<Stream> E6GetStreamAsync(string url)
        {
            Stream stream = null;
            try
            {
                await E621ThrottleGuard.WaitAsync().ConfigureAwait(false);
                stream = await HttpClient.GetStreamAsync(url);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
            finally
            {
                await Task.Delay(ThrottleTiming);
                E621ThrottleGuard.Release();
            }
            return stream;
        }

        public class E6Post
        {
            [JsonProperty("id")]
            public int Id { get; set; }
        }
    }
}
