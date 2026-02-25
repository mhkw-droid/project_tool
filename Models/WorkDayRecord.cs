namespace TaskTool.Models;

public class WorkDayRecord
{
    public string Day { get; set; } = string.Empty;
    public DateTime? ComeLocal { get; set; }
    public DateTime? GoLocal { get; set; }
}
