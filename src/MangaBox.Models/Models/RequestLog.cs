namespace MangaBox.Models;

[Table("mb_request_log")]
public class RequestLog : DbObject
{
    [Column("profile_id")]
    public required Guid? ProfileId { get; set; }

    [Column("start_time")]
    public required DateTime StartTime { get; set; }

    [Column("end_time")]
    public required DateTime EndTime { get; set; }

    [Column("url")]
    public required string Url { get; set; }

    [Column("code")]
    public required int Code { get; set; }

    [Column("body")]
    public required string? Body { get; set; }

    [Column("stack_trace")]
    public required string? StackTrace { get; set; }
}
