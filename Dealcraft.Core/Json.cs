using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Dealcraft.Core;

/// <summary>
/// One JSON object, built key by key and spelled out as a single line.
/// </summary>
/// <remarks>
/// <para>
/// Hand-rolled, and deliberately so. The core targets netstandard2.1 and takes
/// no package references, so there is no serializer to reach for; and a ledger
/// row is a flat record of readings rather than an object graph, so reflection
/// would buy nothing. What it would cost is control over the two things that
/// matter here: the key order, which is the order a reader wants to read the
/// row in, and the culture, because a machine whose decimal separator is a
/// comma must not write a ledger that <c>jq</c> cannot parse.
/// </para>
/// <para>
/// Members keep their insertion order, and a key given twice appears twice —
/// this does not deduplicate, because a row that accidentally wrote the same
/// key from two readings should be visible rather than silently half-lost.
/// </para>
/// </remarks>
public sealed class JsonObject
{
    private readonly List<string> members = new();

    /// <summary>A string, or <c>null</c> when there is nothing to say.</summary>
    public JsonObject Text(string key, string? value) =>
        Put(key, value is null ? "null" : Quote(value));

    public JsonObject Number(string key, int value) =>
        Put(key, value.ToString(CultureInfo.InvariantCulture));

    public JsonObject Number(string key, int? value) =>
        value.HasValue ? Number(key, value.Value) : Nothing(key);

    /// <summary>
    /// A float, as shortly as it can be written without losing a bit of it.
    /// NaN and the infinities have no JSON spelling, so they are written as
    /// <c>null</c>: a reading the game gave as nonsense is a reading we do not
    /// have, and saying so is better than emitting a token no parser accepts.
    /// </summary>
    public JsonObject Number(string key, float value) =>
        Put(key, Spell(value));

    public JsonObject Number(string key, float? value) =>
        value.HasValue ? Number(key, value.Value) : Nothing(key);

    public JsonObject Flag(string key, bool value) =>
        Put(key, value ? "true" : "false");

    public JsonObject Flag(string key, bool? value) =>
        value.HasValue ? Flag(key, value.Value) : Nothing(key);

    /// <summary>
    /// A key that is present and explicitly empty. The ledger writes these
    /// rather than leaving the key out, so a reader can tell "the game did not
    /// give us this" from "this version of the mod did not write it".
    /// </summary>
    public JsonObject Nothing(string key) => Put(key, "null");

    public JsonObject Nest(string key, JsonObject? value) =>
        Put(key, value is null ? "null" : value.ToString());

    public JsonObject List(string key, IEnumerable<JsonObject>? values)
    {
        if (values is null)
        {
            return Nothing(key);
        }

        var items = new List<string>();
        foreach (JsonObject value in values)
        {
            items.Add(value.ToString());
        }

        return Put(key, "[" + string.Join(",", items) + "]");
    }

    public JsonObject Texts(string key, IEnumerable<string>? values)
    {
        if (values is null)
        {
            return Nothing(key);
        }

        var items = new List<string>();
        foreach (string value in values)
        {
            items.Add(Quote(value));
        }

        return Put(key, "[" + string.Join(",", items) + "]");
    }

    public override string ToString() => "{" + string.Join(",", members) + "}";

    private JsonObject Put(string key, string value)
    {
        members.Add(Quote(key) + ":" + value);
        return this;
    }

    private static string Spell(float value) =>
        float.IsNaN(value) || float.IsInfinity(value)
            ? "null"
            : value.ToString("R", CultureInfo.InvariantCulture);

    private static string Quote(string value)
    {
        var quoted = new StringBuilder(value.Length + 2);
        quoted.Append('"');

        foreach (char character in value)
        {
            switch (character)
            {
                case '"': quoted.Append("\\\""); break;
                case '\\': quoted.Append("\\\\"); break;
                case '\b': quoted.Append("\\b"); break;
                case '\f': quoted.Append("\\f"); break;
                case '\n': quoted.Append("\\n"); break;
                case '\r': quoted.Append("\\r"); break;
                case '\t': quoted.Append("\\t"); break;
                default:
                    if (character < ' ')
                    {
                        quoted.Append("\\u").Append(((int)character).ToString("x4", CultureInfo.InvariantCulture));
                    }
                    else
                    {
                        quoted.Append(character);
                    }

                    break;
            }
        }

        quoted.Append('"');
        return quoted.ToString();
    }
}
