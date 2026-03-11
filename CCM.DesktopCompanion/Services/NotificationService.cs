using CCM.DesktopCompanion.Models;
using CommunityToolkit.WinUI.Notifications;
using Windows.UI.Notifications;

namespace CCM.DesktopCompanion.Services;

internal sealed class NotificationService
{
    private readonly Dictionary<long, NotificationState> _stateByCooldownId = new();
    private bool _isInitialized;

    public void ProcessSnapshot(IEnumerable<CooldownRecord> cooldowns)
    {
        var materialized = cooldowns.Where(c => c.Enabled).ToList();
        var activeIds = new HashSet<long>();
        foreach (var cooldown in materialized)
        {
            activeIds.Add(cooldown.Id);
            var state = _stateByCooldownId.TryGetValue(cooldown.Id, out var existing)
                ? existing
                : new NotificationState();

            var nextRemaining = cooldown.NextChargeRemainingSeconds ?? 0;
            var readyNow = cooldown.ReadyChargesNow > 0;
            var warningWindow = !readyNow && nextRemaining > 0 && nextRemaining <= 300;

            if (_isInitialized)
            {
                if (warningWindow && !state.FiveMinuteWarningSent)
                {
                    ShowToast("CCM", $"{cooldown.ItemName} for {cooldown.CharacterName} is ready in under 5 minutes.");
                    state.FiveMinuteWarningSent = true;
                }
                if (readyNow && !state.ReadyNotificationSent)
                {
                    ShowToast("CCM", $"{cooldown.ItemName} for {cooldown.CharacterName} is ready now.");
                    state.ReadyNotificationSent = true;
                }
            }

            if (!warningWindow && nextRemaining > 300)
            {
                state.FiveMinuteWarningSent = false;
            }
            if (!readyNow)
            {
                state.ReadyNotificationSent = false;
            }

            _stateByCooldownId[cooldown.Id] = state;
        }

        foreach (var staleId in _stateByCooldownId.Keys.Where(id => !activeIds.Contains(id)).ToList())
        {
            _stateByCooldownId.Remove(staleId);
        }

        _isInitialized = true;
    }

    private sealed class NotificationState
    {
        public bool FiveMinuteWarningSent { get; set; }
        public bool ReadyNotificationSent { get; set; }
    }

    private static void ShowToast(string title, string message)
    {
        try
        {
            var content = new ToastContentBuilder()
                .AddArgument("source", "ccm")
                .AddText(title)
                .AddText(message)
                .GetToastContent();
            var notification = new ToastNotification(content.GetXml());
            ToastNotificationManagerCompat.CreateToastNotifier().Show(notification);
            CompanionLog.Write($"Toast queued. Title={title}; Message={message}");
        }
        catch (Exception ex)
        {
            CompanionLog.Write($"Toast failure: {ex.GetType().Name}: {ex.Message}");
        }
    }
}
