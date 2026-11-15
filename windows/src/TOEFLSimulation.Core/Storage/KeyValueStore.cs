using System.Text.Json;
using System.Text.Json.Nodes;
using TOEFLSimulation.Core.Json;

namespace TOEFLSimulation.Core.Storage;

/// <summary>
/// The Windows counterpart of the Mac app's <c>UserDefaults</c>/<c>@AppStorage</c>: one JSON file of key/value pairs
/// in the app's data directory. Keys are the same as on the Mac (see <see cref="StorageKeys"/>). Values the Mac app
/// stores as JSON-encoded <c>Data</c> are stored here as nested JSON (same shape as the Mac encoding).
/// Thread-safe; every change is written to disk immediately with an atomic replace.
/// </summary>
public sealed class KeyValueStore
{
    public const string DefaultFileName = "settings.json";

    private readonly object gate = new();
    /// <summary>Each value as compact JSON text.</summary>
    private readonly Dictionary<string, string> values;
    private readonly string? path;

    /// <summary>Opens (or creates on first write) <c>settings.json</c> in <paramref name="dataDirectory"/>.</summary>
    public KeyValueStore(string dataDirectory, string fileName = DefaultFileName)
    {
        Directory.CreateDirectory(dataDirectory);
        path = Path.Combine(dataDirectory, fileName);
        values = Load(path);
    }

    private KeyValueStore()
    {
        values = [];
    }

    /// <summary>A store that is never written to disk (tests, previews).</summary>
    public static KeyValueStore InMemory() => new();

    /// <summary>The backing file, or null for an in-memory store.</summary>
    public string? FilePath => path;

    /// <summary>Raised after a key changes (on the thread that changed it).</summary>
    public event EventHandler<string>? Changed;

    public bool Contains(string key)
    {
        lock (gate) return values.ContainsKey(key);
    }

    public IReadOnlyList<string> Keys
    {
        get { lock (gate) return [.. values.Keys]; }
    }

    private JsonNode? Node(string key)
    {
        lock (gate) return values.TryGetValue(key, out var json) ? JsonNode.Parse(json) : null;
    }

    public string? GetString(string key) =>
        Node(key) is JsonValue value && value.TryGetValue<string>(out var s) ? s : null;

    public void SetString(string key, string? value) => SetNode(key, value is null ? null : JsonValue.Create(value));

    /// <summary>UserDefaults <c>stringArray(forKey:)</c>: null when missing or not an array of strings.</summary>
    public IReadOnlyList<string>? GetStringArray(string key)
    {
        if (Node(key) is not JsonArray array) return null;
        var result = new List<string>(array.Count);
        foreach (var item in array)
        {
            if (item is JsonValue v && v.TryGetValue<string>(out var s)) result.Add(s);
            else return null;
        }
        return result;
    }

    public void SetStringArray(string key, IEnumerable<string> value) =>
        SetNode(key, new JsonArray(value.Select(s => (JsonNode?)JsonValue.Create(s)).ToArray()));

    public bool? GetBool(string key) =>
        Node(key) is JsonValue value && value.TryGetValue<bool>(out var b) ? b : null;

    public void SetBool(string key, bool value) => SetNode(key, JsonValue.Create(value));

    public double? GetDouble(string key) =>
        Node(key) is JsonValue value && value.TryGetValue<double>(out var d) ? d : null;

    public void SetDouble(string key, double value) => SetNode(key, JsonValue.Create(value));

    /// <summary>Decodes a JSON value (Swift <c>JSONDecoder().decode(T.self, from: defaults.data(forKey:))</c>).
    /// Returns default when the key is missing or the value does not decode (as <c>try?</c> does).</summary>
    public T? GetJson<T>(string key)
    {
        string? json;
        lock (gate)
        {
            if (!values.TryGetValue(key, out json)) return default;
        }
        try
        {
            return SwiftJson.Deserialize<T>(json);
        }
        catch (JsonException)
        {
            return default;
        }
        catch (NotSupportedException)
        {
            return default;
        }
    }

    /// <summary>Encodes a value with Swift-compatible JSON and stores it.</summary>
    public void SetJson<T>(string key, T value) => SetRaw(key, SwiftJson.Serialize(value));

    /// <summary>Stores raw JSON text (for example data exported from the Mac app). The text must be valid JSON.</summary>
    public void SetRawJson(string key, string json)
    {
        using var document = JsonDocument.Parse(json);
        SetRaw(key, json);
    }

    /// <summary>The stored JSON text for a key, or null.</summary>
    public string? GetRawJson(string key)
    {
        lock (gate) return values.TryGetValue(key, out var json) ? json : null;
    }

    public void Remove(string key)
    {
        lock (gate)
        {
            if (!values.Remove(key)) return;
            Save();
        }
        Changed?.Invoke(this, key);
    }

    private void SetNode(string key, JsonNode? node)
    {
        if (node is null) Remove(key);
        else SetRaw(key, node.ToJsonString());
    }

    private void SetRaw(string key, string json)
    {
        lock (gate)
        {
            values[key] = json;
            Save();
        }
        Changed?.Invoke(this, key);
    }

    private static Dictionary<string, string> Load(string path)
    {
        if (!File.Exists(path)) return [];
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllBytes(path));
            if (document.RootElement.ValueKind != JsonValueKind.Object) return [];
            var result = new Dictionary<string, string>();
            foreach (var property in document.RootElement.EnumerateObject()) result[property.Name] = property.Value.GetRawText();
            return result;
        }
        catch (JsonException)
        {
            // A damaged file is kept aside rather than overwritten silently.
            try { File.Copy(path, path + ".corrupt", overwrite: true); } catch (IOException) { }
            return [];
        }
    }

    /// <summary>Writes the whole file atomically (temp file, then replace). Caller holds the lock.</summary>
    private void Save()
    {
        if (path is null) return;
        var temp = path + ".tmp";
        using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping, SkipValidation = true }))
            {
                writer.WriteStartObject();
                foreach (var (key, json) in values.OrderBy(pair => pair.Key, StringComparer.Ordinal))
                {
                    writer.WritePropertyName(key);
                    writer.WriteRawValue(json, skipInputValidation: true);
                }
                writer.WriteEndObject();
            }
            stream.Flush(flushToDisk: true);
        }
        File.Move(temp, path, overwrite: true);
    }
}

/// <summary>The persistence keys, identical to the Mac app's UserDefaults keys.</summary>
public static class StorageKeys
{
    /// <summary>[TOEFLExamResult] — Score History (TOEFLHistoryStore).</summary>
    public const string PracticeResults = "toefl.practice.results.v1";
    /// <summary>[String] set keys, oldest first — question exposure history (TOEFLQuestionBank).</summary>
    public const string QuestionExposure = "toefl.question-exposure.v1";
    /// <summary>Complete the Words drill progress (WordsDrillStore).</summary>
    public const string WordsDrill = "toefl.words-drill.v1";
    /// <summary>[ReadingDrillStore.Record] — Reading practice history.</summary>
    public const string ReadingDrill = "toefl.reading-drill.v1";
    /// <summary>[String] sorted mastered vocabulary IDs (flash cards).</summary>
    public const string FlashcardsMastered = "toefl-simulation.flashcards.mastered.v1";
    /// <summary>String "deck" or "infinite" (@AppStorage flash-card mode).</summary>
    public const string FlashcardsMode = "toefl-simulation.flashcards.mode";
}
