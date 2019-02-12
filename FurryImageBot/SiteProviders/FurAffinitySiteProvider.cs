using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace FurryImageBot.SiteProviders
{
    public class FurAffinitySiteProvider : ISiteProvider
    {
        private const int MaximumNumberPosts = 60;
        private HttpClient HttpClient;
        private SemaphoreSlim FurAffinityThrottleGuard;
        private const int ThrottleTiming = 1000;

        public FurAffinitySiteProvider()
        {
            HttpClient = new HttpClient();
            FurAffinityThrottleGuard = new SemaphoreSlim(1);
        }

        public async Task<List<string>> QueryByTagAsync(string query, int maxPosts)
        {
            List<string> FurAffinityPosts = new List<string>();
            {
                int counter = 0;
                int currentPage = 1;
                while (counter < maxPosts)
                {
                    List<string> CurrentE6Posts = await QueryFurAffinityByTag(query, currentPage);
                    counter += CurrentE6Posts.Count();
                    currentPage++;

                    FurAffinityPosts.AddRange(CurrentE6Posts);

                    if (CurrentE6Posts.Count == 0)
                    {
                        break;
                    }
                }
            }

            List<string> Posts = FurAffinityPosts.Select(x => $"https://www.furaffinity.net/view/{x}").Take(maxPosts).ToList();
            return Posts;
        }

        private async Task<List<string>> QueryFurAffinityByTag(string query, int pageNumber)
        {
            string queryString = $"http://faexport.boothale.net/search.json?perpage=60&page={pageNumber}&q={query}";
            List<string> furAffinityPosts = await GetFurAffinity(queryString);
            return furAffinityPosts;
        }

        // GET from FurAffinity API.
        private async Task<List<string>> GetFurAffinity(string url)
        {
            List<string> furAffinityPosts;
            using (Stream stream = await FurAffinityGetStreamAsync(url))
            using (StreamReader streamReader = new StreamReader(stream))
            using (JsonReader jsonReader = new JsonTextReader(streamReader))
            {
                JsonSerializer serializer = new JsonSerializer();
                furAffinityPosts = await Task.Run(() => serializer.Deserialize<List<string>>(jsonReader));
            }
            return furAffinityPosts;
        }

        // Returns a Stream from the given FurAffinity URL. 
        // The caller must Dispose of this Stream.
        // This method takes care of Throttling, to ensure that no more than 1 call per second
        // is made to the FurAffinity servers.
        private async Task<Stream> FurAffinityGetStreamAsync(string url)
        {
            Stream s = null;
            try
            {
                await FurAffinityThrottleGuard.WaitAsync().ConfigureAwait(false);
                s = await HttpClient.GetStreamAsync(url);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
            finally
            {
                await Task.Delay(ThrottleTiming);
                FurAffinityThrottleGuard.Release();
            }
            return s;
        }
    }
}
