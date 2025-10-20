using System.Collections.Concurrent;

namespace Tilith.Core.Models;

public sealed class XpCooldownTracker
{
    private readonly TimeSpan _cooldownDuration;
    private readonly ConcurrentDictionary<ulong, DateTime> _cooldowns = new();

    public XpCooldownTracker(TimeSpan cooldownDuration)
    {
        _cooldownDuration = cooldownDuration;
    }

    public bool TryConsumeXpCooldown(ulong userId, DateTime now)
    {
        if ( _cooldowns.TryGetValue(userId, out var lastXp) )
        {
            if ( now - lastXp < _cooldownDuration )
                return false;
        }

        _cooldowns[userId] = now;
        return true;
    }

    public void Clear()
    {
        _cooldowns.Clear();
    }
}