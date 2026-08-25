namespace OmniChannel.Catalog.Host.Infrastructure;

public abstract class ChangeStreamProcessor<TDocument>(
    IResumeTokenRepository resumeTokens,
    IOptions<MongoDbSettings> settings,
    ILogger logger) : BackgroundService where TDocument : class
{
    protected MongoDbSettings Settings => settings.Value;

    private long _outstanding;

    protected abstract string ServiceName { get; }
    protected abstract IMongoCollection<TDocument> Collection { get; }
    protected virtual ChangeStreamFullDocumentOption FullDocument => ChangeStreamFullDocumentOption.UpdateLookup;
    protected virtual bool IncludePreImage => false;

    protected abstract PipelineDefinition<ChangeStreamDocument<TDocument>, ChangeStreamDocument<TDocument>> BuildPipeline();
    protected abstract string GetEntityId(ChangeStreamDocument<TDocument> change);
    protected abstract Task HandleAsync(ChangeStreamDocument<TDocument> change, CancellationToken cancellationToken);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var attempts = 0;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunAsync(stoppingToken);
                attempts = 0;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex) when (IsResumeTokenInvalid(ex))
            {
                logger.LogWarning("OPLOG ROLLOVER detected for {Service}; clearing resume token and restarting from now", ServiceName);
                await resumeTokens.DeleteAsync(ServiceName, CancellationToken.None);
                attempts = 0;
            }
            catch (Exception ex) when (IsRecoverable(ex))
            {
                attempts++;
                if (attempts > Settings.MaxReconnectAttempts)
                {
                    throw;
                }

                var delay = Settings.ReconnectDelayMs * attempts;
                logger.LogWarning(ex, "Recoverable error in {Service}; reconnect attempt {Attempt} in {Delay}ms", ServiceName, attempts, delay);
                await Task.Delay(delay, stoppingToken);
            }
        }
    }

    private async Task RunAsync(CancellationToken stoppingToken)
    {
        var options = new ChangeStreamOptions
        {
            FullDocument = FullDocument,
            MaxAwaitTime = TimeSpan.FromMilliseconds(Settings.MaxAwaitTimeMs),
            BatchSize = Settings.BatchSize
        };
        if (IncludePreImage)
        {
            options.FullDocumentBeforeChange = ChangeStreamFullDocumentBeforeChangeOption.WhenAvailable;
        }

        var saved = await resumeTokens.GetLatestAsync(ServiceName, stoppingToken);
        if (saved != null)
        {
            options.ResumeAfter = saved;
        }

        Interlocked.Exchange(ref _outstanding, 0);
        var workers = Math.Max(1, Settings.ParallelWorkers);
        var partitions = new Channel<ChangeStreamDocument<TDocument>>[workers];
        for (var i = 0; i < workers; i++)
        {
            partitions[i] = Channel.CreateBounded<ChangeStreamDocument<TDocument>>(
                new BoundedChannelOptions(Math.Max(1, Settings.ChannelCapacity / workers)) { FullMode = BoundedChannelFullMode.Wait });
        }

        var workerTasks = partitions.Select(p => Task.Run(() => WorkerLoop(p.Reader))).ToArray();

        logger.LogInformation("{Service} opening change stream (resume={Resume})", ServiceName, saved != null);

        using var cursor = await Collection.WatchAsync(BuildPipeline(), options, stoppingToken);
        var processedSinceSave = 0;
        try
        {
            while (!stoppingToken.IsCancellationRequested && await cursor.MoveNextAsync(stoppingToken))
            {
                foreach (var change in cursor.Current)
                {
                    var partition = (GetEntityId(change).GetHashCode() & int.MaxValue) % workers;
                    Interlocked.Increment(ref _outstanding);
                    await partitions[partition].Writer.WriteAsync(change, stoppingToken);

                    if (++processedSinceSave >= Settings.ResumeTokenSaveInterval)
                    {
                        await DrainAsync(stoppingToken);
                        if (!stoppingToken.IsCancellationRequested)
                        {
                            await resumeTokens.SaveAsync(ServiceName, change.ResumeToken, stoppingToken);
                        }

                        processedSinceSave = 0;
                    }
                }
            }
        }
        finally
        {
            foreach (var p in partitions)
            {
                p.Writer.TryComplete();
            }

            await Task.WhenAll(workerTasks);
            var token = cursor.GetResumeToken();
            if (token != null)
            {
                await resumeTokens.SaveAsync(ServiceName, token, CancellationToken.None);
            }
        }
    }

    private async Task WorkerLoop(ChannelReader<ChangeStreamDocument<TDocument>> reader)
    {
        await foreach (var change in reader.ReadAllAsync(CancellationToken.None))
        {
            try
            {
                await ProcessWithRetryAsync(change);
            }
            finally
            {
                Interlocked.Decrement(ref _outstanding);
            }
        }
    }

    private async Task ProcessWithRetryAsync(ChangeStreamDocument<TDocument> change)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await HandleAsync(change, CancellationToken.None);
                return;
            }
            catch (Exception ex)
            {
                if (attempt >= Settings.MaxRetries)
                {
                    logger.LogError(ex, "{Service} giving up on entity {EntityId} after {Attempts} attempts", ServiceName, SafeEntityId(change), attempt);
                    return;
                }

                logger.LogWarning(ex, "{Service} retry {Attempt} for entity {EntityId}", ServiceName, attempt, SafeEntityId(change));
                await Task.Delay(Settings.RetryDelayMs);
            }
        }
    }

    private async Task DrainAsync(CancellationToken cancellationToken)
    {
        while (Interlocked.Read(ref _outstanding) > 0 && !cancellationToken.IsCancellationRequested)
        {
            await Task.Delay(5, cancellationToken);
        }
    }

    private string SafeEntityId(ChangeStreamDocument<TDocument> change)
    {
        try
        {
            return GetEntityId(change);
        }
        catch
        {
            return "unknown";
        }
    }

    private static bool IsRecoverable(Exception ex) =>
        ex is MongoConnectionException
            or MongoExecutionTimeoutException
            or MongoNotPrimaryException
            or MongoNodeIsRecoveringException
            or MongoCursorNotFoundException
            or TimeoutException
        || (ex.InnerException != null && IsRecoverable(ex.InnerException));

    private static bool IsResumeTokenInvalid(Exception ex)
    {
        if (ex is MongoCommandException command && command.Code is 286 or 280 or 260)
        {
            return true;
        }

        var message = ex.Message;
        return message.Contains("resume point may no longer be in the oplog", StringComparison.OrdinalIgnoreCase)
            || message.Contains("ChangeStreamHistoryLost", StringComparison.OrdinalIgnoreCase)
            || (ex.InnerException != null && IsResumeTokenInvalid(ex.InnerException));
    }
}