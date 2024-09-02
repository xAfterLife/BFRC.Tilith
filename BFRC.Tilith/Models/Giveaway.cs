namespace BFRC.Tilith.Models;

public class Giveaway
{
    public ulong MessageId { get; set; }
    public ulong ChannelId { get; set; }
    public string Prize { get; set; }
    public DateTime EndTime { get; set; }
    public List<ulong> Participants { get; set; } = new();
    public ulong HostId { get; set; }
}