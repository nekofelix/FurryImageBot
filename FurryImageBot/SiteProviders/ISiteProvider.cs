using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace FurryImageBot.SiteProviders
{
    public interface ISiteProvider
    {
        Task<List<string>> QueryByTagAsync(string query, int maxPosts);
    }
}
