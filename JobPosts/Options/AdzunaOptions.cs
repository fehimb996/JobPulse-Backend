using JobPosts.Models;
using System.Collections.Generic;

namespace JobPosts.Options
{
    public class AdzunaOptions
    {
        public List<AdzunaCredential> Credentials { get; set; } = new();
    }
} 