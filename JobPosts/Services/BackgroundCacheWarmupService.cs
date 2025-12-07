using System.Threading.Channels;

namespace JobPosts.Services
{
    public class BackgroundCacheWarmupService : IHostedService
    {
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly ILogger<BackgroundCacheWarmupService> _logger;
        private readonly Channel<string> _countryQueue;
        private readonly ChannelWriter<string> _writer;
        private readonly ChannelReader<string> _reader;
        private readonly CancellationTokenSource _cancellationTokenSource;

        public BackgroundCacheWarmupService(
            IServiceScopeFactory serviceScopeFactory,
            ILogger<BackgroundCacheWarmupService> logger)
        {
            _serviceScopeFactory = serviceScopeFactory;
            _logger = logger;
            _cancellationTokenSource = new CancellationTokenSource();

            var options = new BoundedChannelOptions(100)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = true,
                SingleWriter = false
            };
            _countryQueue = Channel.CreateBounded<string>(options);
            _writer = _countryQueue.Writer;
            _reader = _countryQueue.Reader;
        }

        public void QueueCacheWarmup(string countryCode)
        {
            if (_writer.TryWrite(countryCode))
            {
                _logger.LogDebug("\n\t\t-> Queued cache warmup for country [{Country}]", countryCode);
            }
            else
            {
                _logger.LogWarning("\n\t\t-> Failed to queue cache warmup for country [{Country}] - queue may be full", countryCode);
            }
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _ = Task.Run(ProcessQueueAsync, cancellationToken);
            return Task.CompletedTask;
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            _writer.Complete();
            _cancellationTokenSource.Cancel();

            // Wait a bit for current operations to complete
            try
            {
                await Task.Delay(5000, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                // Expected when shutting down
            }
        }

        private async Task ProcessQueueAsync()
        {
            await foreach (var countryCode in _reader.ReadAllAsync(_cancellationTokenSource.Token))
            {
                try
                {
                    _logger.LogInformation("\n\t\t-> Starting background cache warmup for country [{Country}]", countryCode);

                    using var scope = _serviceScopeFactory.CreateScope();
                    var cacheManagementService = scope.ServiceProvider.GetRequiredService<CacheManagementService>();

                    // Use a longer timeout for background cache warming
                    using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(10));
                    using var combined = CancellationTokenSource.CreateLinkedTokenSource(
                        _cancellationTokenSource.Token, cts.Token);

                    await WarmupCountryWithRetry(cacheManagementService, countryCode, combined.Token);

                    _logger.LogInformation("\n\t\t-> Completed background cache warmup for country [{Country}]", countryCode);

                    // Add delay between cache warmups to avoid overwhelming the database
                    await Task.Delay(2000, _cancellationTokenSource.Token);
                }
                catch (OperationCanceledException)
                {
                    _logger.LogInformation("\n\t\t-> Cache warmup cancelled for country [{Country}]", countryCode);
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "\n\t\t-> Failed background cache warmup for country [{Country}]", countryCode);
                }
            }
        }

        private async Task WarmupCountryWithRetry(CacheManagementService cacheService, string countryCode, CancellationToken cancellationToken)
        {
            const int maxRetries = 2;
            var delay = TimeSpan.FromMinutes(2);

            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    await cacheService.RefreshCacheForCountry(countryCode);
                    return; // Success
                }
                catch (OperationCanceledException)
                {
                    throw; // Don't retry cancellation
                }
                catch (Exception ex) when (attempt < maxRetries)
                {
                    _logger.LogWarning(ex, "\n\t\t-> Cache warmup attempt {Attempt} failed for country [{Country}], retrying in [{Delay}]",
                        attempt, countryCode, delay);

                    await Task.Delay(delay, cancellationToken);
                    delay = TimeSpan.FromMinutes(delay.TotalMinutes * 1.5); // Exponential backoff
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "\n\t\t-> All cache warmup attempts failed for country [{Country}]", countryCode);
                    throw;
                }
            }
        }

        public void Dispose()
        {
            _cancellationTokenSource?.Dispose();
        }
    }
}
