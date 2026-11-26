using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace ItsFrosti.SpeedrunningMod.Seeds.Ruleset8
{
    internal static class Ruleset8SeedPool
    {
        internal const int RulesetVersion = GenerationRules.CurrentVersion;
        private static readonly object Sync = new object();
        private static readonly Random Random = new Random();
        private static readonly Dictionary<GameMode, string[]> Pools = new Dictionary<GameMode, string[]>();

        // Keep Hardcore unavailable even after its list is populated.
        internal static bool IsEnabled(GameMode mode) { return mode == GameMode.Survival || mode == GameMode.Creative; }
        internal static bool HasSeeds(GameMode mode) { return IsEnabled(mode) && Load(mode).Length != 0; }

        internal static bool Contains(GameMode mode, string seed)
        {
            if (!IsEnabled(mode)) return false;
            long numeric;
            if (!long.TryParse(seed, NumberStyles.Integer, CultureInfo.InvariantCulture, out numeric)) return false;
            return Array.IndexOf(Load(mode), numeric.ToString(CultureInfo.InvariantCulture)) >= 0;
        }

        internal static string Choose(GameMode mode)
        {
            string[] seeds = IsEnabled(mode) ? Load(mode) : new string[0];
            if (seeds.Length == 0) throw new IOException(mode + " has no filtered race seeds yet.");
            lock (Sync)
            {
                return seeds[Random.Next(seeds.Length)];
            }
        }

        internal static string[] Load(GameMode mode)
        {
            string[] cached;
            if (Pools.TryGetValue(mode, out cached)) return cached;
            string resource = mode == GameMode.Survival ? "RaceSeeds.csv" :
                mode == GameMode.Creative ? "CreativeRaceSeeds.csv" :
                mode == GameMode.Hardcore ? "HardcoreRaceSeeds.csv" : null;
            if (resource == null) return new string[0];

            List<string> values = new List<string>();
            HashSet<string> unique = new HashSet<string>(StringComparer.Ordinal);
            string[] lines;
            using (Stream stream = typeof(Plugin).Assembly.GetManifestResourceStream(resource))
            using (StreamReader reader = new StreamReader(stream))
                lines = reader.ReadToEnd().Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < lines.Length; i++)
            {
                string value = (lines[i] ?? string.Empty).Trim().TrimStart('\ufeff');
                long numeric;
                if (value.Length == 0 || !long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out numeric))
                {
                    continue;
                }

                value = numeric.ToString(CultureInfo.InvariantCulture);
                if (unique.Add(value))
                {
                    values.Add(value);
                }
            }

            string[] seeds = values.ToArray();
            Pools[mode] = seeds;
            Plugin.Logger.Info("Loaded " + seeds.Length + " unique Ruleset 8 " + mode + " race seeds.");
            return seeds;
        }
    }
}
