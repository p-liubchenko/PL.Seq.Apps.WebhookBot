using System;
using System.Collections;
using System.Collections.Generic;

using Seq.Apps;
using Seq.Apps.LogEvents;

namespace PL.Seq.Apps.WebhookBot
{
    /// <summary>
    /// Flattens a Seq <see cref="Event{LogEventData}"/> into a plain
    /// dictionary/list/scalar object graph that the Liquid engine can consume.
    ///
    /// Every event property is exposed as a top-level template variable, so an
    /// alert grouped by <c>@timestamp as time</c> can be referenced as
    /// <c>{{ time }}</c>. The full set is also available under
    /// <c>{{ properties.* }}</c>, and event metadata under <c>{{ evt.* }}</c>.
    /// </summary>
    internal static class EventModel
    {
        public static IReadOnlyDictionary<string, object> Build(Event<LogEventData> evt)
        {
            var data = evt.Data;

            var properties = new Dictionary<string, object>();
            if (data.Properties != null)
            {
                foreach (var kv in data.Properties)
                    properties[kv.Key] = Normalize(kv.Value);
            }

            // Start from the properties so they are addressable at the top level
            // (e.g. {{ time }}), then layer on the well-known metadata.
            var model = new Dictionary<string, object>(properties);

            model["properties"] = properties;
            model["evt"] = new Dictionary<string, object>
            {
                ["Id"] = evt.Id,
                ["Timestamp"] = data.LocalTimestamp,
                ["TimestampUtc"] = evt.Timestamp,
                ["Level"] = data.Level.ToString(),
                ["Message"] = data.RenderedMessage,
                ["MessageTemplate"] = data.MessageTemplate,
                ["Exception"] = data.Exception,
                ["EventType"] = evt.EventType,
                ["Properties"] = properties
            };

            return model;
        }

        /// <summary>
        /// Recursively converts Seq's read-only property values into the mutable
        /// dictionaries / lists / scalars that Fluid handles natively (dictionary
        /// values support both <c>a.b</c> and <c>a["b"]</c> access).
        /// </summary>
        private static object Normalize(object value)
        {
            switch (value)
            {
                case null:
                    return null;
                case string s:
                    return s;
                case IReadOnlyDictionary<string, object> readOnlyDict:
                {
                    var map = new Dictionary<string, object>();
                    foreach (var kv in readOnlyDict)
                        map[kv.Key] = Normalize(kv.Value);
                    return map;
                }
                case IDictionary<string, object> dict:
                {
                    var map = new Dictionary<string, object>();
                    foreach (var kv in dict)
                        map[kv.Key] = Normalize(kv.Value);
                    return map;
                }
                case IEnumerable enumerable:
                {
                    var list = new List<object>();
                    foreach (var item in enumerable)
                        list.Add(Normalize(item));
                    return list;
                }
                default:
                    return value;
            }
        }
    }
}
