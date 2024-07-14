using System;
using System.Threading;
using System.Threading.Tasks;
using Artemis.Core;
using Artemis.Core.Services;
using Artemis.UI.Services.Interfaces;
using Artemis.UI.Shared.Services;
using Artemis.UI.Shared.Utilities;
using Artemis.WebClient.Workshop;
using Artemis.WebClient.Workshop.Handlers.InstallationHandlers;
using Artemis.WebClient.Workshop.Models;
using Artemis.WebClient.Workshop.Services;
using Serilog;
using StrawberryShake;

namespace Artemis.UI.Services.Updating;

public class WorkshopUpdateService : IWorkshopUpdateService
{
    private readonly ILogger _logger;
    private readonly IWorkshopClient _client;
    private readonly INotificationService _notificationService;
    private readonly IWorkshopService _workshopService;
    private readonly Lazy<IUpdateNotificationProvider> _updateNotificationProvider;
    private readonly PluginSetting<bool> _showNotifications;

    public WorkshopUpdateService(ILogger logger, IWorkshopClient client, IWorkshopService workshopService, ISettingsService settingsService,
        Lazy<IUpdateNotificationProvider> updateNotificationProvider)
    {
        _logger = logger;
        _client = client;
        _workshopService = workshopService;
        _updateNotificationProvider = updateNotificationProvider;
        _showNotifications = settingsService.GetSetting("Workshop.ShowNotifications", true);
    }

    public async Task AutoUpdateEntries()
    {
        _logger.Information("Checking for workshop updates");
        int checkedEntries = 0;
        int updatedEntries = 0;

        foreach (InstalledEntry entry in _workshopService.GetInstalledEntries())
        {
            if (!entry.AutoUpdate)
                continue;

            checkedEntries++;
            bool updated = await AutoUpdateEntry(entry);
            if (updated)
                updatedEntries++;
        }

        _logger.Information("Checked {CheckedEntries} entries, updated {UpdatedEntries}", checkedEntries, updatedEntries);

        if (updatedEntries > 0 && _showNotifications.Value)
            _updateNotificationProvider.Value.ShowWorkshopNotification(updatedEntries);
    }

    public async Task<bool> AutoUpdateEntry(InstalledEntry entry)
    {
        // Query the latest version
        IOperationResult<IGetEntryLatestReleaseByIdResult> latestReleaseResult = await _client.GetEntryLatestReleaseById.ExecuteAsync(entry.Id);

        if (latestReleaseResult.Data?.Entry?.LatestRelease is not IRelease latestRelease)
            return false;
        if (latestRelease.Id == entry.ReleaseId)
            return false;

        _logger.Information("Auto-updating entry {Entry} to version {Version}", entry, latestRelease.Version);

        EntryInstallResult updateResult = await _workshopService.InstallEntry(entry, latestRelease, new Progress<StreamProgress>(), CancellationToken.None);

        // This happens during installation too but not on our reference of the entry
        if (updateResult.IsSuccess)
            entry.ApplyRelease(latestRelease);

        _logger.Information("Auto-update result: {Result}", updateResult);

        return updateResult.IsSuccess;
    }

    /// <inheritdoc />
    public void DisableNotifications()
    {
        _showNotifications.Value = false;
        _showNotifications.Save();
    }
}