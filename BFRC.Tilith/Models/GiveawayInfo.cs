namespace BFRC.Tilith.Models;

public class GiveawayInfo
{
    public ulong MessageId { get; set; }
    public ulong ChannelId { get; set; }
    public ulong GuildId { get; set; }
    public required string Prize { get; set; }
    public string? GiveawayImageUrl { get; set; }
    public string? EndedImageUrl { get; set; }
    public DateTimeOffset StartTime { get; set; }
    public DateTimeOffset EndTime { get; set; }
    public DateTimeOffset NextDrawTime { get; set; }
    public TimeSpan DrawInterval { get; set; }
    public int RemainingDraws { get; set; }
    public HashSet<ulong> Winners { get; set; } = [];
}