namespace OmniChannel.Catalog.Host.HostedServices;

using OmniChannel.Catalog.Host.Infrastructure;

public class ChannelSimulatorService(
    IMongoContext context,
    IAppendLogRepository<ListingObservedState> observedLog,
    SimulatorSwitch state,
    IOptions<ChannelSimulatorSettings> settings,
    ILogger<ChannelSimulatorService> logger) : BackgroundService
{
    private readonly ChannelSimulatorSettings _settings = settings.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("ChannelSimulator started (interval={Interval}ms, enabled={Enabled})", _settings.IntervalMs, state.Enabled);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (state.Enabled)
                {
                    await RunCycleAsync(stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "ChannelSimulator cycle failed");
            }
            await Task.Delay(_settings.IntervalMs, stoppingToken);
        }
    }

    private async Task RunCycleAsync(CancellationToken cancellationToken)
    {
        var candidates = await context.ListingsCurrent
            .Find(l => (!l.Removed
                    && l.DesiredStatus == PublishStatus.Published
                    && (l.LastObservedAt == null || l.PublishStatus == PublishStatus.Pending))
                || ((l.Removed || l.DesiredStatus == PublishStatus.Withdrawn)
                    && l.LastObservedAt != null
                    && l.EffectiveStatus != ChannelStatus.Paused))
            .Limit(_settings.BatchSize)
            .ToListAsync(cancellationToken);

        foreach (var listing in candidates)
        {
            var observed = BuildObservation(listing);
            await observedLog.InsertAsync(observed, cancellationToken);
        }

        if (candidates.Count > 0)
        {
            logger.LogInformation("ChannelSimulator reported {Count} observations", candidates.Count);
        }
    }

    private static ListingObservedState BuildObservation(ListingCurrentState listing)
    {
        var now = DateTime.UtcNow;
        if (listing.Removed || listing.DesiredStatus == PublishStatus.Withdrawn)
        {
            return new ListingObservedState
            {
                EntityId = listing.EntityId,
                Channel = listing.Channel,
                EffectiveStatus = ChannelStatus.Paused,
                ObservedAt = now,
                CreatedAt = now
            };
        }

        var firstContact = listing.LastObservedAt == null && listing.Channel != SalesChannel.Webshop;
        if (firstContact)
        {
            return new ListingObservedState
            {
                EntityId = listing.EntityId,
                Channel = listing.Channel,
                EffectiveStatus = ChannelStatus.PendingReview,
                ObservedAt = now,
                CreatedAt = now
            };
        }

        return new ListingObservedState
        {
            EntityId = listing.EntityId,
            Channel = listing.Channel,
            EffectiveStatus = ChannelStatus.Active,
            ObservedPrice = listing.Price.Effective,
            Available = listing.Available.Effective,
            ObservedAt = now,
            CreatedAt = now
        };
    }
}