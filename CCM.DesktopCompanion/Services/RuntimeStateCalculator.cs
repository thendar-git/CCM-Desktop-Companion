using CCM.DesktopCompanion.Models;

namespace CCM.DesktopCompanion.Services;

internal sealed class RuntimeStateCalculator
{
    private const int MaxDisplayableRemainingSeconds = 14 * 24 * 60 * 60;

    public DesktopSnapshot ApplyRuntimeState(DesktopSnapshot snapshot, long nowUnix)
    {
        foreach (var cooldown in snapshot.Cooldowns)
        {
            if (!cooldown.IsConcentrationOnly)
            {
                var (readyCharges, nextRemaining) = GetChargeRuntimeState(cooldown, nowUnix);
                cooldown.ReadyChargesNow = readyCharges;
                cooldown.NextChargeRemainingSeconds = nextRemaining;
            }
            cooldown.ConcentrationSimulated = GetConcentrationSimulated(cooldown, nowUnix);
        }

        return snapshot;
    }

    private static (int readyCharges, int? nextRemaining) GetChargeRuntimeState(CooldownRecord cooldown, long now)
    {
        var currentCharges = cooldown.CurrentCharges;
        var maxCharges = cooldown.MaxCharges;
        var durationSeconds = cooldown.DurationSeconds;
        var nextReadyTime = cooldown.ReadyTime;

        if (currentCharges.HasValue)
        {
            currentCharges = Math.Max(0, currentCharges.Value);
        }
        if (maxCharges.HasValue)
        {
            maxCharges = Math.Max(0, maxCharges.Value);
            if (maxCharges.Value == 0)
            {
                maxCharges = null;
            }
        }

        if (currentCharges.HasValue && !maxCharges.HasValue)
        {
            currentCharges = null;
        }

        if (currentCharges.HasValue && maxCharges.HasValue)
        {
            var readyCharges = Math.Min(currentCharges.Value, maxCharges.Value);
            if (readyCharges >= maxCharges.Value)
            {
                return (readyCharges, null);
            }

            if (durationSeconds > 0 && nextReadyTime > 0)
            {
                if (now >= nextReadyTime)
                {
                    var gained = 1 + (int)((now - nextReadyTime) / durationSeconds);
                    readyCharges = Math.Min(maxCharges.Value, readyCharges + Math.Max(0, gained));
                    if (readyCharges >= maxCharges.Value)
                    {
                        return (readyCharges, null);
                    }
                }

                var nextTime = nextReadyTime;
                if (now >= nextTime)
                {
                    var intervals = ((now - nextTime) / durationSeconds) + 1;
                    nextTime += intervals * durationSeconds;
                }

                return (readyCharges, SanitizeRemaining((int)Math.Max(0, nextTime - now)));
            }

            if (nextReadyTime > 0 && now >= nextReadyTime)
            {
                readyCharges = Math.Min(maxCharges.Value, Math.Max(readyCharges, 1));
                if (readyCharges >= maxCharges.Value)
                {
                    return (readyCharges, null);
                }

                return (readyCharges, null);
            }

            return (readyCharges, SanitizeRemaining((int)Math.Max(0, nextReadyTime - now)));
        }

        var remaining = (int)Math.Max(0, nextReadyTime - now);
        if (remaining <= 0)
        {
            return (1, 0);
        }
        return (0, SanitizeRemaining(remaining));
    }

    private static int? GetConcentrationSimulated(CooldownRecord cooldown, long now)
    {
        if (cooldown.ConcentrationCurrent == null || cooldown.ConcentrationMaximum == null)
        {
            return null;
        }

        var scanTime = cooldown.ConcentrationScanTime ?? now;
        var gained = (int)Math.Max(0, now - scanTime) / 360;
        return Math.Min(cooldown.ConcentrationMaximum.Value, cooldown.ConcentrationCurrent.Value + gained);
    }

    private static int? SanitizeRemaining(int remainingSeconds)
    {
        if (remainingSeconds <= 0)
        {
            return 0;
        }

        return remainingSeconds > MaxDisplayableRemainingSeconds ? null : remainingSeconds;
    }
}

