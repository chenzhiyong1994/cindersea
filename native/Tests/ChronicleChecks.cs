using System;
using System.IO;
using System.Linq;
using System.Text;
using Dicebound.Persistence;
using Dicebound.Tactics;
using Newtonsoft.Json.Linq;

static class ChronicleChecks
{
    static int count;
    static void Require(bool ok, string text) { count++; if (!ok) throw new Exception(text); }
    static void Main(string[] args)
    {
        string output = args.Length > 0 ? args[0] : Path.Combine(Path.GetTempPath(), "dicebound-chronicle-tests", Guid.NewGuid().ToString("N"));
        string directory = Path.Combine(output, "profile");
        string path = Path.Combine(directory, "tactical-chronicle.json");
        Require(TacticalChronicle.TryLoad(directory, out var chronicle, out var error) && error == null, "missing is empty valid profile");
        Require(chronicle.completed.Length == 5 && chronicle.completed.All(flag => !flag) && chronicle.witnesses.Count == 0, "empty defaults");
        Require(!File.Exists(path), "load missing does not create file");
        string[] first = { "sixuan", "lingfeng", "cangling" };
        string[] second = { "shangshuo", "ruanzhuo", "yanzhuying" };
        Require(chronicle.TryRecord(directory, 0, first, out error), "first write");
        byte[] firstBytes = File.ReadAllBytes(path);
        Require(TacticalChronicle.TryLoad(directory, out var read, out error) && read.completed[0] && read.witnesses.SequenceEqual(first), "first roundtrip");
        Require(read.completed.Skip(1).All(flag => !flag), "first victory unlocks only its own chapter");
        var stale = new TacticalChronicle();
        Require(stale.TryRecord(directory, 2, second, out error), "stale instance merges disk");
        Require(stale.completed[0] && stale.completed[2] && !stale.completed[1] && stale.witnesses.Count == 6, "merge preserves prior actual entries");
        Require(File.ReadAllBytes(path + ".previous").SequenceEqual(firstBytes), "atomic previous profile preserved");
        Require(stale.TryRecord(directory, 2, second, out error) && stale.witnesses.Count == 6, "repeat is idempotent");
        byte[] validBytes = File.ReadAllBytes(path);
        Require(!stale.TryRecord(directory, -1, first, out error), "negative chapter rejected");
        Require(!stale.TryRecord(directory, 5, first, out error), "overflow chapter rejected");
        Require(!stale.TryRecord(directory, 1, first.Take(2), out error), "two-person input rejected");
        Require(!stale.TryRecord(directory, 1, first.Concat(second.Take(1)), out error), "four-person input rejected");
        Require(!stale.TryRecord(directory, 1, new[] { "sixuan", "sixuan", "lingfeng" }, out error), "duplicate roster rejected");
        Require(!stale.TryRecord(directory, 1, new[] { "sixuan", "lingfeng", "invented" }, out error), "unknown hero rejected");
        Require(!stale.TryRecord(directory, 1, null, out error), "null roster rejected");
        Require(File.ReadAllBytes(path).SequenceEqual(validBytes), "invalid requests leave file intact");
        foreach (var hero in TacticalContent.Heroes) Require(!string.IsNullOrWhiteSpace(TacticalStory.WitnessStatement(hero.id)), "all fixed statements present");
        Require(TacticalStory.WitnessStatement("invented") == "", "unknown statement empty");

        JObject original = JObject.Parse(Encoding.UTF8.GetString(validBytes));
        Action<JObject>[] changes = {
            root => root["version"] = "1", root => root["version"] = 2,
            root => root["format"] = 42, root => root["format"] = "other",
            root => root["completed"] = new JArray(true, false, false, false),
            root => root["completed"] = new JArray(true, false, false, false, false, false),
            root => root["completed"][0] = "true", root => root["completed"][0] = 1,
            root => root["completed"][0] = null, root => root["witnesses"] = null,
            root => root["witnesses"][0] = "unknown", root => root["witnesses"][0] = "SIXUAN",
            root => root["witnesses"][0] = 5, root => root["witnesses"][0] = null,
            root => root["witnesses"][1] = root["witnesses"][0],
            root => root["witnesses"] = new JArray("sixuan"),
            root => root["completed"] = new JArray(false, false, false, false, false),
            root => root["extra"] = true, root => root.Remove("completed")
        };
        foreach (var change in changes)
        {
            var broken = (JObject)original.DeepClone(); change(broken);
            TestCorruption(stale, directory, path, Encoding.UTF8.GetBytes(broken.ToString()));
        }
        TestCorruption(stale, directory, path, Encoding.UTF8.GetBytes("[]"));
        TestCorruption(stale, directory, path, Encoding.UTF8.GetBytes("{broken"));
        TestCorruption(stale, directory, path, Encoding.UTF8.GetBytes(original.ToString() + "{}"));
        TestCorruption(stale, directory, path, Encoding.UTF8.GetBytes(original.ToString().Replace("\"version\": 1", "\"version\": 1, \"version\": 1")));
        TestCorruption(stale, directory, path, new byte[] { 0xff, 0xfe, 0xc0, 0xaf });
        TestCorruption(stale, directory, path, Enumerable.Repeat((byte)' ', 16385).ToArray());
        File.WriteAllBytes(path, validBytes);
        using (var locked = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            Require(!TacticalChronicle.TryLoad(directory, out read, out error) && error != null, "locked file read fails");
            Require(!stale.TryRecord(directory, 4, first, out error) && error != null, "locked file record fails");
        }
        Require(File.ReadAllBytes(path).SequenceEqual(validBytes), "locked primary kept");
        Require(!Directory.GetFiles(directory, "*.pending-*").Any(), "no own pending files left");
        string blocked = Path.Combine(output, "directory-blocker");
        File.WriteAllText(blocked, "retain this blocker", new UTF8Encoding(false));
        var failedWrite = new TacticalChronicle();
        Require(!failedWrite.TryRecord(Path.Combine(blocked, "nested"), 0, first, out error) && error != null, "write failure is reported");
        Require(failedWrite.completed.All(flag => !flag) && failedWrite.witnesses.Count == 0, "failed write does not commit in-memory unlock");
        Require(File.ReadAllText(blocked) == "retain this blocker", "failed write preserves blocking file");
        Console.WriteLine("PASS: " + count + " chronicle assertions; atomic writes, strict types, corrupted-file preservation and actual chapter unlocks.");
    }

    static void TestCorruption(TacticalChronicle stale, string directory, string path, byte[] bytes)
    {
        File.WriteAllBytes(path, bytes);
        Require(!TacticalChronicle.TryLoad(directory, out var read, out var error) && read == null && error != null, "corruption detected");
        Require(!stale.TryRecord(directory, 4, new[] { "sixuan", "lingfeng", "cangling" }, out error) && error != null, "stale instance refuses corrupt overwrite");
        Require(File.ReadAllBytes(path).SequenceEqual(bytes), "corrupt original unchanged");
    }
}
