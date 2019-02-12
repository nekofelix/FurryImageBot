using FurryImageBot.SiteProviders;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace FurryImageBot.Services
{
    public class SiteProviderService
    {
        private ISiteProvider[] SiteProviders;
        private const int RandomMax = 120;

        public SiteProviderService(IServiceProvider serviceProvider)
        {
            SiteProviders = serviceProvider.GetRequiredService<ISiteProvider[]>();
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
    }
}
