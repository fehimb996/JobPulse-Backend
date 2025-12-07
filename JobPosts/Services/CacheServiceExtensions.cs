namespace JobPosts.Services
{
    public static class CacheServiceExtensions
    {
        public static IServiceCollection AddCacheManagement(this IServiceCollection services)
        {
            services.AddSingleton<CacheInvalidationService>();
            services.AddScoped<CacheWarmupService>();
            services.AddScoped<CacheManagementService>();
            services.AddSingleton<BackgroundCacheWarmupService>();
            services.AddHostedService(provider => provider.GetService<BackgroundCacheWarmupService>());

            return services;
        }
    }
}
