using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TOEFLSimulation.Core.Json;

/// <summary>
/// JSON settings that match Swift's <c>JSONEncoder</c>/<c>JSONDecoder</c> defaults as the Mac app uses them:
/// camelCase keys, enum raw values, optional (nil) values omitted, dates as seconds since
/// 2001-01-01 UTC, and UUIDs as upper-case strings.
/// </summary>
public static class SwiftJson
{
    public static JsonSerializerOptions Options { get; } = Create();

    private static JsonSerializerOptions Create()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        };
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false));
        options.Converters.Add(new SwiftDateConverter());
        options.Converters.Add(new SwiftUuidConverter());
        options.MakeReadOnly(populateMissingResolver: true);
        return options;
    }

    public static T? Deserialize<T>(string json) => JsonSerializer.Deserialize<T>(json, Options);
    public static T? Deserialize<T>(ReadOnlySpan<byte> utf8) => JsonSerializer.Deserialize<T>(utf8, Options);
    public static T? Deserialize<T>(JsonElement element) => element.Deserialize<T>(Options);
    public static string Serialize<T>(T value) => JsonSerializer.Serialize(value, Options);
    public static JsonElement SerializeToElement<T>(T value) => JsonSerializer.SerializeToElement(value, Options);
}

/// <summary>Swift's default <c>Date</c> coding: a number of seconds since 2001-01-01 00:00:00 UTC.</summary>
public sealed class SwiftDateConverter : JsonConverter<DateTimeOffset>
{
    public static readonly DateTimeOffset ReferenceDate = new(2001, 1, 1, 0, 0, 0, TimeSpan.Zero);

    public static DateTimeOffset FromReferenceSeconds(double seconds) => ReferenceDate.AddTicks((long)Math.Round(seconds * TimeSpan.TicksPerSecond));
    public static double ToReferenceSeconds(DateTimeOffset date) => (date.UtcTicks - ReferenceDate.UtcTicks) / (double)TimeSpan.TicksPerSecond;

    public override DateTimeOffset Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        FromReferenceSeconds(reader.GetDouble());

    public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options) =>
        writer.WriteNumberValue(ToReferenceSeconds(value));
}

/// <summary>Swift's <c>UUID</c> coding: an upper-case hyphenated string.</summary>
public sealed class SwiftUuidConverter : JsonConverter<Guid>
{
    public override Guid Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        Guid.Parse(reader.GetString() ?? throw new JsonException("UUID string expected."), CultureInfo.InvariantCulture);

    public override void Write(Utf8JsonWriter writer, Guid value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToString("D").ToUpperInvariant());
}
