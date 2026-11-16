using System.Globalization;
using SavorReader.Core.Models;

namespace SavorReader.Core.Services;

public sealed class SettingsService
{
    private readonly LibraryDatabase _database;

    public SettingsService(LibraryDatabase database)
    {
        _database = database;
    }

    public async Task<ReaderSettings> GetReaderSettingsAsync(CancellationToken cancellationToken = default)
    {
        var values = await GetValuesAsync(cancellationToken);
        var theme = values.TryGetValue("reader.theme", out var themeValue)
            && Enum.TryParse<ReaderTheme>(themeValue, true, out var parsedTheme)
                ? parsedTheme
                : ReaderTheme.Day;
        var font = values.TryGetValue("reader.font", out var fontValue)
            && Enum.TryParse<ReaderFont>(fontValue, true, out var parsedFont)
                ? parsedFont
                : ReaderFont.Serif;

        return new ReaderSettings
        {
            Theme = theme,
            Font = font,
            FontSize = ParseDouble(values, "reader.fontSize", 18),
            LineHeight = ParseDouble(values, "reader.lineHeight", 1.7),
            Margin = ParseDouble(values, "reader.margin", 48),
        }.Clamp();
    }

    public async Task SaveReaderSettingsAsync(
        ReaderSettings settings,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var clamped = settings.Clamp();
        await SetManyAsync(new Dictionary<string, string>
        {
            ["reader.theme"] = clamped.Theme.ToString(),
            ["reader.font"] = clamped.Font.ToString(),
            ["reader.fontSize"] = clamped.FontSize.ToString(CultureInfo.InvariantCulture),
            ["reader.lineHeight"] = clamped.LineHeight.ToString(CultureInfo.InvariantCulture),
            ["reader.margin"] = clamped.Margin.ToString(CultureInfo.InvariantCulture),
        }, cancellationToken);
    }

    public async Task<string> GetLibraryViewModeAsync(CancellationToken cancellationToken = default)
    {
        var value = await GetValueAsync("library.view", cancellationToken);
        return string.Equals(value, "list", StringComparison.OrdinalIgnoreCase) ? "list" : "grid";
    }

    public Task SetLibraryViewModeAsync(string mode, CancellationToken cancellationToken = default) =>
        SetValueAsync("library.view", string.Equals(mode, "list", StringComparison.OrdinalIgnoreCase) ? "list" : "grid", cancellationToken);

    public async Task<bool> GetNavigationPaneOpenAsync(CancellationToken cancellationToken = default)
    {
        var value = await GetValueAsync("ui.navigationPaneOpen", cancellationToken);
        return !string.Equals(value, "false", StringComparison.OrdinalIgnoreCase);
    }

    public Task SetNavigationPaneOpenAsync(bool isOpen, CancellationToken cancellationToken = default) =>
        SetValueAsync("ui.navigationPaneOpen", isOpen ? "true" : "false", cancellationToken);

    public async Task<string?> GetValueAsync(string key, CancellationToken cancellationToken = default)
    {
        await using var connection = await _database.OpenConnectionAsync(cancellationToken);
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT Value FROM Settings WHERE Key = $key;";
        command.Parameters.AddWithValue("$key", key);
        return await command.ExecuteScalarAsync(cancellationToken) as string;
    }

    public async Task SetValueAsync(string key, string value, CancellationToken cancellationToken = default)
    {
        await using var connection = await _database.OpenConnectionAsync(cancellationToken);
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO Settings(Key, Value) VALUES($key, $value)
            ON CONFLICT(Key) DO UPDATE SET Value = excluded.Value;
            """;
        command.Parameters.AddWithValue("$key", key);
        command.Parameters.AddWithValue("$value", value);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task<Dictionary<string, string>> GetValuesAsync(CancellationToken cancellationToken)
    {
        await using var connection = await _database.OpenConnectionAsync(cancellationToken);
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT Key, Value FROM Settings;";

        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            values[reader.GetString(0)] = reader.GetString(1);
        }

        return values;
    }

    private async Task SetManyAsync(
        IReadOnlyDictionary<string, string> values,
        CancellationToken cancellationToken)
    {
        await using var connection = await _database.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        foreach (var (key, value) in values)
        {
            using var command = connection.CreateCommand();
            command.Transaction = (Microsoft.Data.Sqlite.SqliteTransaction)transaction;
            command.CommandText = """
                INSERT INTO Settings(Key, Value) VALUES($key, $value)
                ON CONFLICT(Key) DO UPDATE SET Value = excluded.Value;
                """;
            command.Parameters.AddWithValue("$key", key);
            command.Parameters.AddWithValue("$value", value);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    private static double ParseDouble(
        IReadOnlyDictionary<string, string> values,
        string key,
        double fallback) =>
        values.TryGetValue(key, out var value)
        && double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
        && double.IsFinite(parsed)
            ? parsed
            : fallback;
}
