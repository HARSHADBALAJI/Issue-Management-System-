using TicketSystem.Api.Data;

namespace TicketSystem.Api.Services;

public class SlaBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<SlaBackgroundService> _logger;

    public SlaBackgroundService(IServiceScopeFactory scopeFactory, ILogger<SlaBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("SLA background service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);

                using var scope = _scopeFactory.CreateScope();
                var slaService = scope.ServiceProvider.GetRequiredService<ISlaService>();

                await slaService.CheckBreachedSlasAsync();
                await slaService.SendSlaNotificationsAsync();
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SLA background check failed");
            }
        }
    }
}
