using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Dicebound.Tactics;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Dicebound.Persistence
{
    // 只解锁确实完成过的主线文本，不保存永久战斗数值。
    public sealed class TacticalChronicle
    {
        public bool[] completed = new bool[5];
        public List<string> witnesses = new List<string>();

        private const string FileName = "tactical-chronicle.json";
        private const int MaximumBytes = 16384;
        private static readonly object FileGate = new object();
        private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);

        // 不存在的现场档案是一份正常空档案。损坏、类型错误及读写错误均返回 false。
        public static bool TryLoad(string directory, out TacticalChronicle chronicle, out string error)
        {
            lock (FileGate)
            {
                return Read(directory, out chronicle, out error);
            }
        }

        // 调用方必须先提交主旅程，再在实际主战胜利时记录本幕；支路不得调用。
        public bool TryRecord(string directory, int chapter, IEnumerable<string> heroIds, out string error)
        {
            error = null;
            if (chapter < 0 || chapter >= 5)
            {
                error = "现场档案的幕编号无效。";
                return false;
            }
            if (heroIds == null)
            {
                error = "现场档案缺少同行者阵容。";
                return false;
            }
            List<string> ids = heroIds.Take(4).ToList();
            if (ids.Count != 3 || ids.Distinct(StringComparer.Ordinal).Count() != 3 || ids.Any(id => !KnownHero(id)))
            {
                error = "现场档案只能记录三名不同的正式同行者。";
                return false;
            }

            lock (FileGate)
            {
                // 每次写入都重读磁盘。即使调用方握着旧空对象，也不覆盖后来损坏的原件。
                if (!Read(directory, out TacticalChronicle recorded, out error)) return false;
                recorded.completed[chapter] = true;
                foreach (string id in ids)
                    if (!recorded.witnesses.Contains(id)) recorded.witnesses.Add(id);

                string pending = null;
                try
                {
                    string path = Path.Combine(directory, FileName);
                    Directory.CreateDirectory(directory);
                    pending = path + ".pending-" + Guid.NewGuid().ToString("N");
                    var root = new JObject
                    {
                        ["format"] = "dicebound-tactical-chronicle",
                        ["version"] = 1,
                        ["completed"] = new JArray(recorded.completed),
                        ["witnesses"] = new JArray(recorded.witnesses)
                    };
                    byte[] bytes = Utf8.GetBytes(root.ToString(Formatting.Indented));
                    using (var stream = new FileStream(pending, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    {
                        stream.Write(bytes, 0, bytes.Length);
                        stream.Flush(true);
                    }
                    if (File.Exists(path)) File.Replace(pending, path, path + ".previous");
                    else File.Move(pending, path);
                    completed = (bool[])recorded.completed.Clone();
                    witnesses = new List<string>(recorded.witnesses);
                    return true;
                }
                catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is ArgumentException || e is NotSupportedException)
                {
                    error = "现场档案未能写入，原文件已保留；本次主旅程不受影响。";
                    return false;
                }
                finally
                {
                    if (pending != null)
                    {
                        try { if (File.Exists(pending)) File.Delete(pending); }
                        catch (IOException) { }
                        catch (UnauthorizedAccessException) { }
                    }
                }
            }
        }

        private static bool Read(string directory, out TacticalChronicle chronicle, out string error)
        {
            chronicle = null;
            error = null;
            try
            {
                if (string.IsNullOrWhiteSpace(directory))
                {
                    error = "现场档案目录无效。";
                    return false;
                }
                string path = Path.Combine(directory, FileName);
                if (!File.Exists(path))
                {
                    chronicle = new TacticalChronicle();
                    return true;
                }
                if (new FileInfo(path).Length > MaximumBytes) return Corrupt(out error);
                byte[] bytes = File.ReadAllBytes(path);
                if (bytes.Length > MaximumBytes) return Corrupt(out error);
                string text = Utf8.GetString(bytes);
                using (var reader = new JsonTextReader(new StringReader(text))
                {
                    MaxDepth = 8,
                    DateParseHandling = DateParseHandling.None
                })
                {
                    var root = JObject.Load(reader, new JsonLoadSettings
                    {
                        DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error
                    });
                    if (reader.Read() || root.Properties().Count() != 4 ||
                        root["format"]?.Type != JTokenType.String || (string)root["format"] != "dicebound-tactical-chronicle" ||
                        root["version"]?.Type != JTokenType.Integer || (int)root["version"] != 1 ||
                        !(root["completed"] is JArray flags) || flags.Count != 5 || flags.Any(flag => flag.Type != JTokenType.Boolean) ||
                        !(root["witnesses"] is JArray people) || people.Count > TacticalContent.Heroes.Length || people.Any(person => person.Type != JTokenType.String))
                        return Corrupt(out error);

                    var decoded = new TacticalChronicle
                    {
                        completed = flags.Select(flag => (bool)flag).ToArray(),
                        witnesses = people.Select(person => (string)person).ToList()
                    };
                    if (decoded.witnesses.Any(id => !KnownHero(id)) ||
                        decoded.witnesses.Distinct(StringComparer.Ordinal).Count() != decoded.witnesses.Count ||
                        (decoded.completed.Any(flag => flag) ? decoded.witnesses.Count < 3 : decoded.witnesses.Count != 0))
                        return Corrupt(out error);
                    chronicle = decoded;
                    return true;
                }
            }
            catch (Exception e) when (e is JsonException || e is ArgumentException || e is InvalidCastException || e is FormatException || e is OverflowException)
            {
                return Corrupt(out error);
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is NotSupportedException)
            {
                error = "现场档案无法读取，原文件已保留。";
                return false;
            }
        }

        private static bool KnownHero(string id)
        {
            return id != null && TacticalContent.GetHero(id) != null;
        }

        private static bool Corrupt(out string error)
        {
            error = "现场档案的格式、章节或同行者记录无效，原文件已保留。";
            return false;
        }
    }
}
