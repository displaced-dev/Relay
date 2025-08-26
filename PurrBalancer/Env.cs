using System.Text;

namespace PurrBalancer;

public static class Env
{
    static readonly Dictionary<string, string> _env = new();

    static Env()
    {
        const string ENV_FILE = ".env";

        if (!File.Exists(ENV_FILE)) return;

        var lines = File.ReadAllLines(ENV_FILE);
        string? key = null;
        StringBuilder? valueBuilder = null;
        bool insideMultiline = false;

        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#') || line.StartsWith(';'))
                continue;

            if (!insideMultiline)
            {
                var index = line.IndexOf('=');
                if (index <= 0) continue;

                key = line[..index].Trim();
                var value = line[(index + 1)..].Trim();

                if (value.StartsWith('"') && !value.EndsWith("\""))
                {
                    insideMultiline = true;
                    valueBuilder = new StringBuilder();
                    valueBuilder.AppendLine(value[1..]); // remove opening "
                }
                else
                {
                    _env[key] = value.Trim('"');
                }
            }
            else
            {
                // Keep collecting multiline value
                if (line.EndsWith('\"'))
                {
                    insideMultiline = false;
                    valueBuilder!.AppendLine(line[..^1]); // remove closing "
                    _env[key!] = valueBuilder.ToString().Trim();
                    key = null;
                    valueBuilder = null;
                }
                else
                {
                    valueBuilder!.AppendLine(line);
                }
            }
        }

    }

    static string? GetToken(string key)
    {
        var token = Environment.GetEnvironmentVariable(key, EnvironmentVariableTarget.Process);

        if (!string.IsNullOrEmpty(token))
            return token;

        token = Environment.GetEnvironmentVariable(key, EnvironmentVariableTarget.User);

        if (!string.IsNullOrEmpty(token))
            return token;

        token = Environment.GetEnvironmentVariable(key, EnvironmentVariableTarget.Machine);

        if (!string.IsNullOrEmpty(token))
            return token;

        return null;
    }

    public static bool TryGetInt(string key, out int value)
    {
        if (TryGetValue(key, out var stringValue) && int.TryParse(stringValue, out value))
            return true;

        value = 0;
        return false;
    }

    public static bool TryGetValue(string key, out string? value)
    {
        var token = GetToken(key);

        if (!string.IsNullOrEmpty(token))
        {
            value = token;
            return true;
        }

        return _env.TryGetValue(key, out value);
    }

    public static string TryGetValueOrDefault(string key, string defaultValue)
    {
        if (TryGetValue(key, out var value))
            return value ?? defaultValue;
        return defaultValue;
    }

    public static int TryGetIntOrDefault(string key, int defaultValue)
    {
        if (TryGetInt(key, out var value))
            return value;
        return defaultValue;
    }
}
