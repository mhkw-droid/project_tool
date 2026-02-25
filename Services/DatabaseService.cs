using Microsoft.Data.Sqlite;
using System.IO;

namespace TaskTool.Services;

public class DatabaseService
{
    private readonly LoggerService _logger;
    private readonly string _dbPath = Path.Combine(AppContext.BaseDirectory, "TaskTool.db");
    public string ConnectionString => new SqliteConnectionStringBuilder { DataSource = _dbPath }.ToString();

    public DatabaseService(LoggerService logger) => _logger = logger;

    public void Initialize()
    {
        try
        {
            using var conn = new SqliteConnection(ConnectionString);
            conn.Open();
            var sql = @"
CREATE TABLE IF NOT EXISTS tasks (
    id TEXT PRIMARY KEY,
    title TEXT NOT NULL,
    description TEXT,
    ticket_url TEXT,
    start_local TEXT NULL,
    end_local TEXT NULL,
    status TEXT NOT NULL,
    priority INTEGER NULL,
    tags TEXT,
    outlook_entry_id TEXT,
    ticket_minutes_booked INTEGER NOT NULL DEFAULT 0,
    created_utc TEXT NOT NULL,
    updated_utc TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS time_logs (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    task_id TEXT NOT NULL,
    start_utc TEXT NOT NULL,
    end_utc TEXT NULL,
    note TEXT
);

CREATE TABLE IF NOT EXISTS work_days (
    day TEXT PRIMARY KEY,
    come_local TEXT NULL,
    go_local TEXT NULL
);

CREATE TABLE IF NOT EXISTS breaks (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    day TEXT NOT NULL,
    start_local TEXT NOT NULL,
    end_local TEXT NULL,
    note TEXT
);";
            using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            cmd.ExecuteNonQuery();
        }
        catch (Exception ex)
        {
            _logger.Error($"DB init failed: {ex.Message}");
        }
    }
}
