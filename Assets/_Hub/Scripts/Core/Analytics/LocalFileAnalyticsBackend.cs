using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace Miniverse.Hub.Analytics
{
    /// <summary>
    /// Appends one JSON object per line to Application.persistentDataPath/analytics.jsonl.
    /// No account, SDK, or network call needed — pull the file off a test device with
    /// `adb pull /sdcard/Android/data/&lt;applicationId&gt;/files/analytics.jsonl` (or the
    /// editor's persistentDataPath while testing locally) and count game_launch events per
    /// gameId for "most played", or average game_end.durationSeconds for engagement.
    ///
    /// Hand-rolled JSON writer instead of JsonUtility because JsonUtility can't serialize a
    /// Dictionary, and pulling in a JSON package for four lines of output isn't worth a new
    /// dependency yet.
    /// </summary>
    public class LocalFileAnalyticsBackend : IAnalyticsBackend
    {
        static readonly string LogPath = Path.Combine(Application.persistentDataPath, "analytics.jsonl");
        static readonly object FileLock = new object();

        public void LogEvent(string eventName, IReadOnlyDictionary<string, object> parameters)
        {
            var sb = new StringBuilder();
            sb.Append('{');
            AppendField(sb, "event", eventName, first: true);
            AppendField(sb, "ts", DateTime.UtcNow.ToString("o"));
            foreach (var kv in parameters)
                AppendField(sb, kv.Key, kv.Value);
            sb.Append('}');

            lock (FileLock)
            {
                File.AppendAllText(LogPath, sb.ToString() + "\n");
            }
        }

        static void AppendField(StringBuilder sb, string key, object value, bool first = false)
        {
            if (!first) sb.Append(',');
            sb.Append('"').Append(Escape(key)).Append("\":");
            switch (value)
            {
                case null:
                    sb.Append("null");
                    break;
                case string s:
                    sb.Append('"').Append(Escape(s)).Append('"');
                    break;
                case bool b:
                    sb.Append(b ? "true" : "false");
                    break;
                case float or double:
                    sb.Append(Convert.ToDouble(value).ToString("0.###", CultureInfo.InvariantCulture));
                    break;
                default:
                    sb.Append(Convert.ToString(value, CultureInfo.InvariantCulture));
                    break;
            }
        }

        static string Escape(string s) => s.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }
}
