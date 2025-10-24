using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace MCMAA.Tests.GoldenTests
{
    /// <summary>
    /// Normalization helpers for golden comparison.
    /// - Removes volatile fields (timestamps, run ids)
    /// - Canonicalizes JSON (sorted keys, stable ordering for arrays where applicable)
    /// </summary>
    public static class GoldenNormalization
    {
        // Add names of keys that are known to vary per-run and should be stripped before comparison
        private static readonly HashSet<string> VolatileKeys = new(StringComparer.OrdinalIgnoreCase)
        {
            "timestamp", "time", "generated_at", "run_id", "id", "request_id", "duration_ms"
        };

        public static string NormalizeJson(string json)
        {
            using var doc = JsonDocument.Parse(json);
            var normalized = NormalizeElement(doc.RootElement);
            var options = new JsonSerializerOptions
            {
                WriteIndented = false
            };
            return JsonSerializer.Serialize(normalized, options);
        }

        private static object NormalizeElement(JsonElement el)
        {
            return el.ValueKind switch
            {
                JsonValueKind.Object => NormalizeObject(el),
                JsonValueKind.Array => NormalizeArray(el),
                JsonValueKind.String => NormalizeString(el.GetString()!),
                JsonValueKind.Number => NormalizeNumber(el),
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Null => null,
                _ => el.ToString()
            };
        }

        private static object NormalizeObject(JsonElement obj)
        {
            var dict = new SortedDictionary<string, object?>(StringComparer.Ordinal);
            foreach (var prop in obj.EnumerateObject())
            {
                if (VolatileKeys.Contains(prop.Name))
                    continue;

                // Recursively normalize
                dict[prop.Name] = NormalizeElement(prop.Value);
            }
            return dict;
        }

        private static object NormalizeArray(JsonElement arr)
        {
            var list = arr.EnumerateArray().Select(NormalizeElement).ToList();

            // Heuristic: if array elements are primitive strings or numbers, sort to canonical order
            if (list.All(i => i is string || i is int || i is long || i is double || i is decimal))
            {
                var sorted = list.Select(i => i?.ToString()).OrderBy(s => s, StringComparer.Ordinal).ToList<object?>();
                return sorted;
            }

            // For arrays of objects, attempt to sort by a stable key if present (e.g., 'name' or 'id')
            if (list.All(i => i is SortedDictionary<string, object?>))
            {
                var objList = list.Cast<SortedDictionary<string, object?>>();
                var sorted = objList.OrderBy(o =>
                {
                    if (o.ContainsKey("name") && o["name"] != null) return o["name"].ToString();
                    if (o.ContainsKey("id") && o["id"] != null) return o["id"].ToString();
                    return JsonSerializer.Serialize(o);
                }, StringComparer.Ordinal).ToList<object?>();
                return sorted;
            }

            // Otherwise keep order
            return list;
        }

        private static object NormalizeString(string s)
        {
            // Trim, collapse whitespace, normalize newlines
            if (s is null) return s!;
            var normalized = s.Replace("\r\n", "\n").Trim();
            while (normalized.Contains("  "))
                normalized = normalized.Replace("  ", " ");
            return normalized;
        }

        private static object NormalizeNumber(JsonElement el)
        {
            if (el.TryGetInt64(out var l)) return l;
            if (el.TryGetDouble(out var d)) return Math.Round(d, 6);
            return el.GetRawText();
        }
    }
}