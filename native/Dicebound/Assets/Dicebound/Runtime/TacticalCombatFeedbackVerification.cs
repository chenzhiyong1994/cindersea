using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Dicebound.Persistence;
using Dicebound.Tactics;
using UnityEngine;

namespace Dicebound.Presentation
{
    /// <summary>Focused actual-Player checks. Every checkpoint comes from legal ascent actions.</summary>
    public sealed class TacticalCombatFeedbackVerification : MonoBehaviour
    {
        [Serializable] private sealed class ActionEvidence
        {
            public string name, hero, skill;
            public uint seed;
            public int preparationActions, revisionBefore, revisionAfter, damageNumbers, shieldNumbers, healNumbers, contacts, impulses, impactCallbacks;
            public bool reduced, interrupted, savedBeforeAnimation, exactlyOnce, savedStateMatches, cameraRestored, hitFlashAtContact, contactSynchronized, localHoldObserved, naturalNumberLifetime, cleanup;
            public float maxImpulsePixels, numberLifetimeSeconds, impactHoldSeconds;
        }
        [Serializable] private sealed class Report
        {
            public string mode = "combat-feedback-focus";
            public bool complete, isolated = true, legalCheckpoints = true, fullJourneyVerification = false;
            public int width, height, errors, legalPreparationActions, tacticalAudioClips;
            public List<string> captures = new List<string>();
            public List<ActionEvidence> actions = new List<ActionEvidence>();
        }
        private sealed class Fixture
        {
            public TacticalState before;
            public TacticalRequest request;
            public List<string> trace;
        }
        private static string directory;
        private TacticalDirector game;
        private string failure;
        private readonly Report report = new Report();
        private readonly Dictionary<string, Fixture> bridges = new Dictionary<string, Fixture>();
        private static readonly string[][] Teams = { new[] { "sixuan", "lingfeng", "cangling" }, new[] { "yanzhuying", "shangshuo", "sixuan" } };

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Boot()
        {
            var args = Environment.GetCommandLineArgs();
            if (!args.Contains("--dicebound-combat-feedback-verify")) return;
            int index = Array.IndexOf(args, "--dicebound-save-dir");
            if (index < 0 || index + 1 >= args.Length) throw new InvalidOperationException("Combat feedback verification requires an explicit fresh save directory.");
            directory = Path.GetFullPath(args[index + 1]);
            string normal = Path.GetFullPath(Application.persistentDataPath);
            if (string.Equals(directory.TrimEnd('\\', '/'), normal.TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase) ||
                File.Exists(Path.Combine(directory, "tactical-journey.json")) || File.Exists(Path.Combine(directory, "tactical-chronicle.json")))
                throw new InvalidOperationException("Combat feedback verification cannot use existing or normal player archives.");
            Directory.CreateDirectory(directory);
            new GameObject("Focused combat feedback verification").AddComponent<TacticalCombatFeedbackVerification>();
        }
        private void OnEnable() { Application.logMessageReceived += OnLog; }
        private void OnDisable() { Application.logMessageReceived -= OnLog; }
        private void OnLog(string message, string stack, LogType type)
        {
            if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
            report.errors++; failure = message + "\n" + stack;
        }
        private void Update()
        {
            if (failure == null) return;
            File.WriteAllText(Path.Combine(directory, "combat-feedback-failed.txt"), failure);
            File.WriteAllText(Path.Combine(directory, "combat-feedback-verification.json"), JsonUtility.ToJson(report, true));
            Application.Quit(1);
        }
        private IEnumerator Start()
        {
            yield return null;
            game = TacticalDirector.Instance;
            if (!game) throw new InvalidOperationException("Combat feedback verification has no live TacticalDirector.");
            Application.runInBackground = true;
            yield return Guarded(Verify());
        }
        private IEnumerator Guarded(IEnumerator routine)
        {
            var stack = new Stack<IEnumerator>(); stack.Push(routine);
            while (stack.Count > 0)
            {
                bool moved = false; object current = null;
                try { moved = stack.Peek().MoveNext(); if (moved) current = stack.Peek().Current; }
                catch (Exception e) { failure = e.ToString(); Debug.LogException(e); }
                if (failure != null) yield break;
                if (!moved) { (stack.Pop() as IDisposable)?.Dispose(); continue; }
                if (current is IEnumerator nested) stack.Push(nested); else yield return current;
            }
        }
        private IEnumerator Verify()
        {
            report.width = Screen.width; report.height = Screen.height;
            report.tacticalAudioClips = game.Audio.TacticalClipCount;
            if (report.tacticalAudioClips != 23) throw new InvalidOperationException("Combat feedback audio resources did not load: " + report.tacticalAudioClips);
            foreach (string hero in new[] { "sixuan", "lingfeng", "cangling", "yanzhuying", "shangshuo" })
            {
                var strike = FindSkillFixture(hero, "strike");
                yield return Drive(hero + "-strike", strike, false, false, false);
                string signature = hero == "sixuan" ? "balance" : hero == "lingfeng" ? "cleave" : hero == "cangling" ? "tide" : hero == "yanzhuying" ? "moon" : "anchor";
                var special = FindSkillFixture(hero, signature);
                yield return Drive(hero + "-" + signature, special, false, false, true);
                yield return null;
            }
            var brace = FindSkillFixture("sixuan", "brace");
            yield return Drive("brace-shield", brace, false, false, true);
            var enemy = FindBlockedEnemyFixture();
            yield return Drive("enemy-blocked-turn", enemy, false, false, true);
            yield return Drive("battle-ending-shield", FindBattleEndingFixture(), false, false, true);
            var heavy = FindSkillFixture("lingfeng", "cleave");
            yield return Drive("reduced-heavy", heavy, true, false, true);
            yield return Drive("paused-heavy", heavy, false, true, true);
            float deadline = Time.realtimeSinceStartup + 8;
            while (report.captures.Any(name => !File.Exists(Path.Combine(directory, name))) && Time.realtimeSinceStartup < deadline) yield return null;
            foreach (string name in report.captures)
                if (!File.Exists(Path.Combine(directory, name))) throw new IOException("Player screenshot missing: " + name);
            report.complete = true;
            File.WriteAllText(Path.Combine(directory, "combat-feedback-verification.json"), JsonUtility.ToJson(report, true));
            Debug.Log("DICEBOUND_COMBAT_FEEDBACK complete=true actions=" + report.actions.Count + " output=" + directory);
            Application.Quit(0);
        }
        private IEnumerator Drive(string name, Fixture fixture, bool reduced, bool interrupt, bool capture)
        {
            var before = fixture.before;
            var request = fixture.request;
            if (before.phase != "battle" || before.journeyMode != "ascent" || !TacticalRules.Preview(before, request).ok)
                throw new InvalidOperationException(name + ": fixture is not a legal ascent battle action.");
            game.LoadCombatFeedbackCheckpoint(before, reduced);
            game.Stage.enabled = false; game.HoverExit();
            yield return new WaitForEndOfFrame();
            var expected = TacticalRules.Act(before, request);
            // Retain a modest user view change across action framing and cleanup.
            game.Stage.ZoomView(.6f); game.Stage.PanView(new Vector2(14, -9));
            var camera = game.Stage.Camera;
            Vector3 position = camera.transform.position; float zoom = camera.orthographicSize;
            var evidence = new ActionEvidence { name = name, hero = request.unitId, skill = request.skillId, seed = before.seed,
                preparationActions = fixture.trace.Count, revisionBefore = before.revision, reduced = reduced, interrupted = interrupt };
            report.actions.Add(evidence); report.legalPreparationActions += fixture.trace.Count;
            File.WriteAllLines(Path.Combine(directory, name + "-legal-preparation.txt"), fixture.trace);
            int contacts = game.Stage.ContactCount, impulses = game.Stage.CameraImpulseCount, impact = game.Stage.SkillVfx.ImpactCount;
            int damage = game.Stage.CombatNumbers ? game.Stage.CombatNumbers.DamageCount : 0;
            int shield = game.Stage.CombatNumbers ? game.Stage.CombatNumbers.ShieldCount : 0;
            int heal = game.Stage.CombatNumbers ? game.Stage.CombatNumbers.HealCount : 0;
            int expectedDamage = 0, expectedShield = 0, expectedHeal = 0;
            foreach (var old in before.units.Where(u => u.hp > 0))
            {
                if (expected.phase == "reward" && old.team == "hero") continue;
                var next = TacticalRules.FindUnit(expected, old.id);
                if (next.hp < old.hp) expectedDamage++; else if (next.hp > old.hp) expectedHeal++;
                if (next.block != old.block) expectedShield++;
            }
            if (capture && name == "lingfeng-cleave") Capture(name + "-before");
            if (request.type == "skill")
            {
                game.Select(request.unitId); game.ChooseSkill(request.skillId); game.CellPress(request.x, request.y);
                if (game.PendingTarget == null || !TacticalRules.Preview(game.State, game.PendingTarget).ok)
                    throw new InvalidOperationException(name + ": live controls failed to lock the legal skill.");
                game.Stage.enabled = true; game.Commit();
            }
            else { game.Stage.enabled = true; game.Request(request); }
            AssertState(name, expected); evidence.savedBeforeAnimation = game.Busy;
            if (!game.Busy) throw new InvalidOperationException(name + ": action did not enter presentation/input lock.");
            game.Request(request); AssertState(name + " duplicate while busy", expected);
            bool contactSeen = false, windupCaptured = false, aftermathCaptured = false, interrupted = false;
            float firstNumbers = -1, lastNumbers = -1, firstHold = -1, lastHold = -1;
            float deadline = Time.realtimeSinceStartup + 14;
            while (game.Busy)
            {
                if (Time.realtimeSinceStartup > deadline) throw new TimeoutException(name + ": action presentation did not finish.");
                var vfx = game.Stage.SkillVfx;
                var numbers = game.Stage.CombatNumbers;
                evidence.maxImpulsePixels = Mathf.Max(evidence.maxImpulsePixels, game.Stage.CameraImpulsePixels);
                if (capture && !reduced && !interrupt && name != "brace-shield" && name != "battle-ending-shield" && !windupCaptured && vfx.Active && vfx.Progress >= .10f && vfx.ImpactCount == impact)
                { Capture(name + "-windup"); windupCaptured = true; }
                bool skillContact = request.type == "skill" && vfx.ImpactCount > impact;
                bool enemyContact = request.type == "endTurn" && game.Stage.ContactCount > contacts;
                if ((skillContact || enemyContact) && !contactSeen)
                {
                    contactSeen = true;
                    if (enemyContact) evidence.hitFlashAtContact = HasHitFlash();
                    if (skillContact)
                    {
                        evidence.contactSynchronized = Mathf.Abs(vfx.Progress - vfx.ImpactProgress) < .001f && Time.realtimeSinceStartup - vfx.LastImpactRealtime < .09f;
                        if (expectedDamage > 0 || expectedShield > 0)
                        {
                            int shown = (numbers.DamageCount - damage) + (numbers.ShieldCount - shield) + (numbers.HealCount - heal);
                            if (shown != expectedDamage + expectedShield + expectedHeal)
                                throw new InvalidOperationException(name + ": committed numbers were not presented at the authored contact.");
                        }
                        if (expectedDamage > 0)
                        {
                            evidence.hitFlashAtContact = HasHitFlash();
                            if (!evidence.hitFlashAtContact) throw new InvalidOperationException(name + ": target hit flash was not synchronized to contact.");
                        }
                        if (!evidence.contactSynchronized) throw new InvalidOperationException(name + ": contact missed the authored skill impact frame.");
                    }
                    if (capture) Capture(name + "-contact");
                }
                if (skillContact && Mathf.Abs(vfx.Progress - vfx.ImpactProgress) < .001f)
                {
                    if (firstHold < 0) firstHold = Time.realtimeSinceStartup;
                    lastHold = Time.realtimeSinceStartup;
                }
                if (numbers && numbers.ActiveCount > 0)
                {
                    if (firstNumbers < 0) firstNumbers = Time.realtimeSinceStartup;
                    lastNumbers = Time.realtimeSinceStartup;
                    if (capture && !reduced && !interrupt && name != "battle-ending-shield" && !aftermathCaptured && Time.realtimeSinceStartup - firstNumbers >= .16f)
                    { Capture(name + "-numbers"); aftermathCaptured = true; }
                }
                if (interrupt && contactSeen && firstNumbers >= 0 && Time.realtimeSinceStartup - firstNumbers >= .12f)
                {
                    game.SetPresentationPaused(true); interrupted = true;
                    AssertState(name + " paused", expected);
                    if (game.Busy || vfx.Active || (numbers && numbers.ActiveCount != 0) || game.Stage.CameraImpulsePixels != 0)
                        throw new InvalidOperationException(name + ": pause did not clear presentation, numbers and camera impulse.");
                    if (HasHitFlash()) throw new InvalidOperationException(name + ": pause left an actor hit flash active.");
                    game.SetPresentationPaused(false);
                    yield return new WaitForSecondsRealtime(.12f);
                    AssertState(name + " resumed", expected);
                    break;
                }
                yield return null;
            }
            evidence.revisionAfter = game.State.revision;
            evidence.exactlyOnce = evidence.revisionAfter == before.revision + 1;
            AssertState(name + " after presentation", expected); evidence.savedStateMatches = true;
            evidence.damageNumbers = game.Stage.CombatNumbers.DamageCount - damage;
            evidence.shieldNumbers = game.Stage.CombatNumbers.ShieldCount - shield;
            evidence.healNumbers = game.Stage.CombatNumbers.HealCount - heal;
            evidence.contacts = game.Stage.ContactCount - contacts;
            evidence.impulses = game.Stage.CameraImpulseCount - impulses;
            evidence.impactCallbacks = game.Stage.SkillVfx.ImpactCount - impact;
            evidence.cameraRestored = Vector3.Distance(position, camera.transform.position) < .003f && Mathf.Abs(zoom - camera.orthographicSize) < .003f;
            evidence.cleanup = !game.Busy && !game.Stage.SkillVfx.Active && game.Stage.CombatNumbers.ActiveCount == 0 && game.Stage.CameraImpulsePixels == 0;
            evidence.numberLifetimeSeconds = firstNumbers < 0 ? 0 : lastNumbers - firstNumbers;
            evidence.impactHoldSeconds = firstHold < 0 ? 0 : lastHold - firstHold;
            evidence.localHoldObserved = !reduced && evidence.impactHoldSeconds >= .02f;
            evidence.naturalNumberLifetime = interrupt || firstNumbers < 0 || evidence.numberLifetimeSeconds >= (reduced ? .32f : .70f);
            if (!evidence.exactlyOnce || !evidence.cameraRestored || !evidence.cleanup || !evidence.naturalNumberLifetime)
                throw new InvalidOperationException(name + ": final presentation invariants failed: " + JsonUtility.ToJson(evidence));
            if (!interrupt && (evidence.damageNumbers != expectedDamage || evidence.shieldNumbers != expectedShield || evidence.healNumbers != expectedHeal))
                throw new InvalidOperationException(name + ": numbers differ from committed per-unit health/shield changes.");
            if (request.type == "skill" && evidence.impactCallbacks != 1) throw new InvalidOperationException(name + ": skill impact callback did not occur exactly once.");
            if ((expectedDamage > 0 || request.type == "endTurn") && evidence.contacts <= 0) throw new InvalidOperationException(name + ": expected a visible contact reaction.");
            if (reduced && (evidence.impulses != 0 || evidence.maxImpulsePixels != 0)) throw new InvalidOperationException(name + ": reduced motion produced a camera impulse.");
            if (!reduced && expectedDamage > 0 && (evidence.impulses <= 0 || evidence.maxImpulsePixels <= 0)) throw new InvalidOperationException(name + ": damage had no measured camera response.");
            if (name == "lingfeng-cleave" && !evidence.localHoldObserved) throw new InvalidOperationException(name + ": authored local hit hold was not observed.");
            if (interrupt && !interrupted) throw new InvalidOperationException(name + ": interruption never reached live contact feedback.");
            if (name == "battle-ending-shield" && (evidence.healNumbers != 0 || evidence.shieldNumbers != 0 || evidence.contacts != expectedDamage))
                throw new InvalidOperationException(name + ": postbattle restoration was presented as combat damage, blocking or healing.");
            game.Stage.enabled = false; game.HoverExit();
            if (capture && (reduced || interrupt || name == "battle-ending-shield")) Capture(name + "-after");
            yield return null;
        }
        private bool HasHitFlash()
        {
            return game.Stage.GetComponentsInChildren<TacticalActor>(true).SelectMany(actor => actor.GetComponentsInChildren<MeshRenderer>(true))
                .Any(renderer => renderer.sharedMaterial && renderer.sharedMaterial.HasProperty("_ImpactMix") && renderer.sharedMaterial.GetFloat("_ImpactMix") > .01f);
        }
        private void AssertState(string name, TacticalState expected)
        {
            string encoded = TacticalCodec.Encode(expected);
            if (game.State.revision != expected.revision || TacticalCodec.Encode(game.State) != encoded)
                throw new InvalidOperationException(name + ": live state differs from the single legal rules result.");
            if (!new TacticalStore(directory).TryLoad(out var saved, out var error) || TacticalCodec.Encode(saved) != encoded)
                throw new InvalidOperationException(name + ": disk checkpoint differs from the committed result: " + error);
        }
        private void Capture(string name)
        {
            string file = name + ".png";
            report.captures.Add(file); ScreenCapture.CaptureScreenshot(Path.Combine(directory, file));
        }
        private Fixture FindSkillFixture(string hero, string skill)
        {
            var team = Teams.First(t => t.Contains(hero));
            foreach (uint seed in new uint[] { 71, 113, 29, 211, 47 })
            {
                var basis = Bridge(seed, team);
                if (basis == null) continue;
                var state = basis.before.Clone(); var trace = new List<string>(basis.trace);
                for (int i = 0; i < 140 && state.phase == "battle"; i++)
                {
                    var unit = TacticalRules.FindUnit(state, hero);
                    if (unit == null || unit.hp <= 0) break;
                    var candidate = SkillCandidate(state, hero, skill);
                    if (candidate != null) return new Fixture { before = state, request = candidate, trace = trace };
                    var move = Approach(state, hero, skill);
                    var request = move ?? new TacticalRequest { type = "endTurn" };
                    Advance(ref state, request, trace);
                }
            }
            throw new InvalidOperationException("No legal bridge fixture for " + hero + "/" + skill + "; no stats, skills or terrain were altered.");
        }
        private Fixture Bridge(uint seed, string[] team)
        {
            string key = seed + ":" + string.Join(",", team);
            if (bridges.TryGetValue(key, out var found)) return found;
            var state = TacticalRules.NewAscent(seed, team); var trace = new List<string>();
            for (int i = 0; i < 1200 && state.phase != "defeat" && state.phase != "victory"; i++)
            {
                if (state.phase == "battle" && state.chapter == 2)
                { found = new Fixture { before = state, trace = trace }; bridges[key] = found; return found; }
                var request = TacticalRules.Suggest(state);
                if (state.phase == "map")
                {
                    // Prefer the first reachable chapter-2 battle over a nonbattle node.
                    var battle = TacticalRules.AvailableNodes(state).FirstOrDefault(n => n.chapter == 2 && (n.kind == "battle" || n.kind == "elite"));
                    if (battle != null) request = new TacticalRequest { type = "node", choice = battle.id };
                }
                if (request == null || !TacticalRules.Preview(state, request).ok) break;
                Advance(ref state, request, trace);
            }
            bridges[key] = null; return null;
        }
        private static TacticalRequest SkillCandidate(TacticalState state, string hero, string skill)
        {
            var definition = TacticalContent.GetSkill(skill);
            foreach (var cell in TacticalRules.SkillTargets(state, hero, skill))
            {
                if (definition.kind == "attack" || definition.kind == "bind")
                {
                    if (!state.units.Any(u => u.hp > 0 && u.team == "enemy" && u.x == cell.x && u.y == cell.y)) continue;
                }
                return new TacticalRequest { type = "skill", unitId = hero, skillId = skill, x = cell.x, y = cell.y };
            }
            return null;
        }
        private static TacticalRequest Approach(TacticalState state, string hero, string skill)
        {
            var unit = TacticalRules.FindUnit(state, hero); var cells = TacticalRules.MoveCells(state, hero);
            if (cells.Count == 0) return null;
            foreach (var cell in cells)
            {
                var move = new TacticalRequest { type = "move", unitId = hero, x = cell.x, y = cell.y };
                if (SkillCandidate(TacticalRules.Act(state, move), hero, skill) != null) return move;
            }
            var enemies = state.units.Where(u => u.team == "enemy" && u.hp > 0).ToArray();
            int range = TacticalContent.GetSkill(skill).range;
            var goals = state.terrain.Where(c => TacticalRules.Walkable(state, c.x, c.y) &&
                (skill != "tide" || c.kind == "mist") && enemies.Any(e => Math.Abs(e.x - c.x) + Math.Abs(e.y - c.y) <= range)).ToArray();
            if (goals.Length == 0 && skill == "tide") goals = state.terrain.Where(c => c.kind == "mist").ToArray();
            if (goals.Length == 0) return null;
            int width = TacticalRules.BoardWidth(state), height = TacticalRules.BoardHeight(state);
            var distance = Enumerable.Repeat(999, width * height).ToArray(); var used = new bool[distance.Length];
            Func<int, int, bool> open = (x, y) => TacticalRules.Walkable(state, x, y) && TacticalRules.ObjectAt(state, x, y) == null &&
                !state.units.Any(u => u.hp > 0 && u.id != hero && u.x == x && u.y == y);
            foreach (var goal in goals) if (open(goal.x, goal.y)) distance[goal.y * width + goal.x] = 0;
            for (int i = 0; i < distance.Length; i++)
            {
                int index = -1;
                for (int j = 0; j < distance.Length; j++) if (!used[j] && (index < 0 || distance[j] < distance[index])) index = j;
                if (index < 0 || distance[index] >= 999) break;
                used[index] = true; int x = index % width, y = index / width;
                foreach (var offset in new[] { new Vector2Int(1, 0), new Vector2Int(-1, 0), new Vector2Int(0, 1), new Vector2Int(0, -1) })
                {
                    int nx = x + offset.x, ny = y + offset.y;
                    if (!open(nx, ny)) continue;
                    int next = ny * width + nx;
                    distance[next] = Math.Min(distance[next], distance[index] + TacticalRules.MoveCost(state, x, y));
                }
            }
            var best = cells.OrderBy(c => distance[c.y * width + c.x]).ThenBy(c => TacticalRules.TerrainAt(state, c.x, c.y) == "pipe" ? 1 : 0).First();
            return distance[best.y * width + best.x] < distance[unit.y * width + unit.x]
                ? new TacticalRequest { type = "move", unitId = hero, x = best.x, y = best.y } : null;
        }
        private Fixture FindBlockedEnemyFixture()
        {
            foreach (uint seed in new uint[] { 71, 113, 29, 211, 47 })
            {
                var basis = Bridge(seed, Teams[0]); if (basis == null) continue;
                var state = basis.before.Clone(); var trace = new List<string>(basis.trace);
                for (int i = 0; i < 100 && state.phase == "battle"; i++)
                {
                    var attacks = TacticalRules.EnemyIntents(state).Where(intent => intent.text.Contains("攻击")).ToArray();
                    foreach (var intent in attacks)
                    {
                        var target = TacticalRules.FindUnit(state, intent.targetId);
                        foreach (var caster in state.units.Where(u => u.team == "hero" && u.hp > 0 && u.skills.Contains("brace")))
                        {
                            var guard = new TacticalRequest { type = "skill", unitId = caster.id, skillId = "brace", x = target.x, y = target.y };
                            if (!TacticalRules.Preview(state, guard).ok) continue;
                            Advance(ref state, guard, trace); break;
                        }
                    }
                    var end = new TacticalRequest { type = "endTurn" };
                    var after = TacticalRules.Act(state, end);
                    if (state.units.Any(u => u.team == "hero" && u.hp > 0 && u.block > TacticalRules.FindUnit(after, u.id).block))
                        return new Fixture { before = state, request = end, trace = trace };
                    var request = TacticalRules.Suggest(state);
                    if (request == null) break;
                    Advance(ref state, request, trace);
                }
            }
            throw new InvalidOperationException("No legal brace/blocked enemy-turn fixture was reached.");
        }
        private Fixture FindBattleEndingFixture()
        {
            foreach (uint seed in new uint[] { 71, 113, 29, 211, 47 })
            {
                var basis = Bridge(seed, Teams[0]); if (basis == null) continue;
                var state = basis.before.Clone(); var trace = new List<string>(basis.trace);
                for (int i = 0; i < 160 && state.phase == "battle"; i++)
                {
                    foreach (var caster in state.units.Where(u => u.team == "hero" && u.hp > 0 && u.skills.Contains("brace")))
                    {
                        var guard = new TacticalRequest { type = "skill", unitId = caster.id, skillId = "brace", x = caster.x, y = caster.y };
                        if (!TacticalRules.Preview(state, guard).ok) continue;
                        var guarded = TacticalRules.Act(state, guard);
                        foreach (var attacker in guarded.units.Where(u => u.team == "hero" && u.hp > 0))
                            foreach (string skill in attacker.skills.Where(id => TacticalContent.GetSkill(id).kind == "attack" || TacticalContent.GetSkill(id).kind == "bind"))
                                foreach (var target in TacticalRules.SkillTargets(guarded, attacker.id, skill))
                                {
                                    var strike = new TacticalRequest { type = "skill", unitId = attacker.id, skillId = skill, x = target.x, y = target.y };
                                    if (TacticalRules.Act(guarded, strike).phase != "reward") continue;
                                    Advance(ref state, guard, trace);
                                    return new Fixture { before = state, request = strike, trace = trace };
                                }
                    }
                    var request = TacticalRules.Suggest(state);
                    if (request == null) break;
                    Advance(ref state, request, trace);
                }
            }
            throw new InvalidOperationException("No legal battle-ending hit with an existing allied shield was reached.");
        }
        private static void Advance(ref TacticalState state, TacticalRequest request, List<string> trace)
        {
            if (!TacticalRules.Preview(state, request).ok) throw new InvalidOperationException("Illegal fixture preparation action.");
            trace.Add(state.revision + ":" + state.phase + ":" + request.type + ":" + request.unitId + ":" + request.skillId + ":" + request.choice + ":" + request.x + "," + request.y);
            state = TacticalRules.Act(state, request);
        }
    }

    public sealed partial class TacticalDirector
    {
        internal void LoadCombatFeedbackCheckpoint(TacticalState checkpoint, bool reduced)
        {
            if (!Environment.GetCommandLineArgs().Contains("--dicebound-combat-feedback-verify")) throw new InvalidOperationException("Combat feedback checkpoints require the isolated verification flag.");
            if (Busy || checkpoint == null) throw new InvalidOperationException("Cannot replace a busy or missing combat feedback checkpoint.");
            // Suppress preference writes before changing presentation preferences. This
            // flag does not opt into the unrelated full-journey TacticalVerification.
            qa = true; ReducedMotion = reduced;
            var next = checkpoint.Clone();
            if (!store.TrySave(next, out var error)) throw new IOException("Combat feedback checkpoint was not saved: " + error);
            CloseOverlay(); State = next; SaveError = null;
            selected = null; skill = null; pending = null; hovered = null;
            Render();
        }
    }
}
