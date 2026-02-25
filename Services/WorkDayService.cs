using Microsoft.Data.Sqlite;
using TaskTool.Models;

namespace TaskTool.Services;

public class WorkDayService
{
    private readonly DatabaseService _db;
    private readonly LoggerService _logger;

    public WorkDayService(DatabaseService db, LoggerService logger)
    {
        _db = db;
        _logger = logger;
    }

    public WorkDayRecord GetOrCreateToday()
    {
        var day = DateTime.Today.ToString("yyyy-MM-dd");
        using var conn = new SqliteConnection(_db.ConnectionString);
        conn.Open();
        using var select = conn.CreateCommand();
        select.CommandText = "SELECT day,come_local,go_local FROM work_days WHERE day=$d";
        select.Parameters.AddWithValue("$d", day);
        using var r = select.ExecuteReader();
        if (r.Read())
        {
            return new WorkDayRecord
            {
                Day = day,
                ComeLocal = DateTime.TryParse(r["come_local"]?.ToString(), out var c) ? c : null,
                GoLocal = DateTime.TryParse(r["go_local"]?.ToString(), out var g) ? g : null
            };
        }

        using var ins = conn.CreateCommand();
        ins.CommandText = "INSERT INTO work_days(day) VALUES ($d)";
        ins.Parameters.AddWithValue("$d", day);
        ins.ExecuteNonQuery();
        return new WorkDayRecord { Day = day };
    }

    public List<BreakRecord> GetBreaks(string day)
    {
        var list = new List<BreakRecord>();
        using var conn = new SqliteConnection(_db.ConnectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM breaks WHERE day=$d ORDER BY start_local";
        cmd.Parameters.AddWithValue("$d", day);
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            list.Add(new BreakRecord
            {
                Id = Convert.ToInt64(r["id"]),
                Day = day,
                StartLocal = DateTime.Parse(r["start_local"].ToString()!),
                EndLocal = DateTime.TryParse(r["end_local"]?.ToString(), out var e) ? e : null,
                Note = r["note"]?.ToString() ?? string.Empty
            });
        }
        return list;
    }

    public void SetCome(DateTime time)
    {
        var record = GetOrCreateToday();
        using var conn = new SqliteConnection(_db.ConnectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE work_days SET come_local=$t WHERE day=$d";
        cmd.Parameters.AddWithValue("$t", time.ToString("s"));
        cmd.Parameters.AddWithValue("$d", record.Day);
        cmd.ExecuteNonQuery();
    }

    public void SetGo(DateTime time)
    {
        var record = GetOrCreateToday();
        using var conn = new SqliteConnection(_db.ConnectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE work_days SET go_local=$t WHERE day=$d";
        cmd.Parameters.AddWithValue("$t", time.ToString("s"));
        cmd.Parameters.AddWithValue("$d", record.Day);
        cmd.ExecuteNonQuery();
    }

    public void StartBreak(string day)
    {
        using var conn = new SqliteConnection(_db.ConnectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "INSERT INTO breaks(day,start_local,note) VALUES ($d,$s,'pause')";
        cmd.Parameters.AddWithValue("$d", day);
        cmd.Parameters.AddWithValue("$s", DateTime.Now.ToString("s"));
        cmd.ExecuteNonQuery();
    }

    public void EndBreak(string day)
    {
        using var conn = new SqliteConnection(_db.ConnectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE breaks SET end_local=$e WHERE id=(SELECT id FROM breaks WHERE day=$d AND end_local IS NULL ORDER BY id DESC LIMIT 1)";
        cmd.Parameters.AddWithValue("$d", day);
        cmd.Parameters.AddWithValue("$e", DateTime.Now.ToString("s"));
        cmd.ExecuteNonQuery();
    }
}
