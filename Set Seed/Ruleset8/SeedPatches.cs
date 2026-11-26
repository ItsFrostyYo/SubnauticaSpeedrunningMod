using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;

namespace ItsFrosti.SpeedrunningMod.Seeds.Ruleset8
{
    // Install through the loader-owned Harmony instance. Original game methods
    // stay intact, and their native behavior runs whenever no race seed is active.
    internal static class SeedPatches
    {
        internal static void Install()
        {
            Patch(typeof(RandomStart), "GetRandomStartPoint", "StartPoint");
            Patch(typeof(CSVEntitySpawner), "GetPrefabForSlot", "SlotPrefab");
            Patch(typeof(EntitySlotsPlaceholder), "Spawn", "Placeholder");
            Patch(typeof(EntitySlot), "SpawnVirtualEntities", "LiveSlot");
            Patch(typeof(TechFragment), "GetRandomTech", "Fragment");
            Patch(typeof(SpawnRandom), "Start", "SpawnRandomPrefix");
            Patch(typeof(SpawnRandomChild), "Start", "SpawnChild");
            Patch(typeof(RandomModel), "Start", "Model");
            Patch(typeof(Stalker), "CheckLoseTooth", "Tooth");
            Patch(typeof(EcoRegion), "FindNearestTarget", "EcoTarget");

            Scope(typeof(SpawnStoredLoot), "SpawnRandomStoredItems");
            Scope(typeof(DelayedSpawn), "Spawn");
            Scope(typeof(Creature), "Start");
            Scope(typeof(ReefbackLife), "Initialize");
            Scope(typeof(BreakableResource), "BreakIntoResources");
            Scope(typeof(Drillable), "OnDrill");
            Scope(typeof(CrashedShipExploder), "SetExplodeTime");
            Scope(typeof(ScaleVariance), "Start");
            Patch(typeof(SaveLoadManager), "InitializeNewGame", "InitializeGame");
            Patch(typeof(Utils), "SetSavegameDir", null, "SaveDirectory");
            Patch(typeof(EscapePod), "StartAtPosition", "PodPosition");

            string[] types = {
                "AggressiveToPilotingVehicle", "AggressiveWhenSeeTarget", "AttackCyclops",
                "AvoidObstacles", "AvoidTerrain", "CollectShiny", "CrawlerAttackLastTarget",
                "Creature", "FleeWhenScared", "MoveOnGround", "MoveOnSurface",
                "MoveTowardsTarget", "MushroomAttack", "RangedAttackLastTarget", "Scareable",
                "School", "SeaDragonMeleeAttack", "Stalker", "SwimInGroup", "SwimInSchool",
                "SwimRandom", "TreaderMoveOnSurface"
            };
            foreach (string name in types)
            {
                Type type = typeof(Creature).Assembly.GetType(name, true);
                foreach (MethodInfo method in type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                {
                    if (method.IsAbstract || method.GetMethodBody() == null || (name == "Stalker" && method.Name == "CheckLoseTooth")) continue;
                    bool usesRandom = false;
                    foreach (CodeInstruction instruction in PatchProcessor.GetOriginalInstructions(method))
                    {
                        var called = instruction.operand as MethodInfo;
                        if (called != null && called.DeclaringType == typeof(UnityEngine.Random)) { usesRandom = true; break; }
                    }
                    if (!usesRandom) continue;
                    Plugin.Harmony.Patch(method, transpiler: new HarmonyMethod(typeof(SeedPatches), "CreatureRandom"));
                }
            }
        }

        private static void Patch(Type type, string name, string prefix, string postfix = null)
        {
            MethodInfo target = AccessTools.DeclaredMethod(type, name);
            if (target == null) throw new MissingMethodException(type.FullName, name);
            Plugin.Harmony.Patch(target,
                prefix: prefix == null ? null : new HarmonyMethod(typeof(SeedPatches), prefix),
                postfix: postfix == null ? null : new HarmonyMethod(typeof(SeedPatches), postfix));
        }

        private static void Scope(Type type, string name)
        {
            MethodInfo target = AccessTools.DeclaredMethod(type, name);
            if (target == null) throw new MissingMethodException(type.FullName, name);
            Plugin.Harmony.Patch(target, prefix: new HarmonyMethod(typeof(SeedPatches), "BeginScope"),
                finalizer: new HarmonyMethod(typeof(SeedPatches), "EndScope"));
        }

        private static bool StartPoint(RandomStart __instance, ref Vector3 __result)
        {
            if (!SeedRuntime.Active) return true;
            __result = SeedGeneration.GetRandomStartPoint(__instance); return false;
        }
        private static bool SlotPrefab(CSVEntitySpawner __instance, IEntitySlot __0, bool __1, ref EntitySlot.Filler __result)
        {
            if (!SeedRuntime.Active) return true;
            __result = SeedGeneration.GetPrefabForSlot(__instance, __0, __1); return false;
        }
        private static bool Placeholder(EntitySlotsPlaceholder __instance)
        {
            if (!SeedRuntime.Active || __instance.slotsData == null) return true;
            SeedGeneration.SpawnPlaceholder(__instance); return false;
        }
        private static bool LiveSlot(EntitySlot __instance)
        {
            if (!SeedRuntime.Active || LargeWorldStreamer.main == null || LargeWorldStreamer.main.cellManager == null) return true;
            SeedGeneration.SpawnLiveSlot(__instance); return false;
        }
        private static bool Fragment(TechFragment __instance, ref TechType __result)
        {
            if (!SeedRuntime.Active) return true;
            __result = SeedGeneration.GetFragmentTech(__instance); return false;
        }
        private static bool SpawnRandomPrefix(SpawnRandom __instance)
        {
            if (!SeedRuntime.Active || __instance.prefabs == null || __instance.prefabs.Length == 0) return true;
            SeedGeneration.StartSpawnRandom(__instance); return false;
        }
        private static bool SpawnChild(SpawnRandomChild __instance)
        {
            if (!SeedRuntime.Active || __instance.gridTestSpawn || __instance.prefabs == null || __instance.prefabs.Length == 0) return true;
            SeedGeneration.StartSpawnRandomChild(__instance); return false;
        }
        private static bool Model(RandomModel __instance)
        {
            if (!SeedRuntime.Active || __instance.modelVariants == null || __instance.modelVariants.Length == 0) return true;
            SeedGeneration.StartRandomModel(__instance); return false;
        }
        private static bool Tooth(Stalker __instance, GameObject __0)
        {
            if (!SeedRuntime.Active) return true;
            SeedGeneration.CheckStalkerTooth(__instance, __0); return false;
        }
        private static bool EcoTarget(EcoRegion __instance, EcoTargetType __0, Vector3 __1,
            EcoRegion.TargetFilter __2, ref float __3, ref IEcoTarget __4)
        {
            if (!SeedRuntime.Active) return true;
            SeedGeneration.FindNearestEcoTarget(__instance, __0, __1, __2, ref __3, ref __4); return false;
        }

        private static void InitializeGame() { SeedRuntime.BindCurrentSave(); }
        private static void SaveDirectory() { if (!Utils.GetContinueMode()) SeedRuntime.ActivatePendingForNewGame(); }
        private static void PodPosition(ref Vector3 __0) { __0 = SeedGeneration.ForceLifepodPosition(__0); }

        private static void BeginScope(Component __instance, MethodBase __originalMethod, out UnityRandomScopeState __state)
        {
            __state = null;
            if (!SeedRuntime.Active) return;
            string domain;
            bool eventScope = false;
            switch (__originalMethod.DeclaringType.Name)
            {
                case "SpawnStoredLoot": domain = "stored-loot"; break;
                case "DelayedSpawn": domain = "delayed-spawn"; eventScope = true; break;
                case "Creature": SeedRuntime.RegisterCreatureOrigin((Creature)__instance); domain = "creature-initialization"; break;
                case "ReefbackLife": domain = "reefback-ecosystem"; break;
                case "BreakableResource": domain = "breakable-resource"; eventScope = true; break;
                case "Drillable": domain = "drillable-resource"; eventScope = true; break;
                case "CrashedShipExploder": domain = "aurora-explosion-time"; break;
                default: domain = "scale-variance"; break;
            }
            __state = UnityRandomScope.Begin(eventScope ? SeedRuntime.DeriveComponentEvent(__instance, domain) : SeedRuntime.DeriveComponent(__instance, domain));
        }

        private static Exception EndScope(Exception __exception, UnityRandomScopeState __state)
        {
            UnityRandomScope.End(__state);
            return __exception;
        }

        private static IEnumerable<CodeInstruction> CreatureRandom(IEnumerable<CodeInstruction> instructions)
        {
            foreach (CodeInstruction instruction in instructions)
            {
                var call = instruction.operand as MethodInfo;
                string hook = null;
                if (instruction.opcode == OpCodes.Call && call != null && call.DeclaringType == typeof(UnityEngine.Random))
                {
                    switch (call.Name)
                    {
                        case "get_value": hook = "NextCreatureAIValue"; break;
                        case "get_onUnitSphere": hook = "NextCreatureAIOnUnitSphere"; break;
                        case "get_insideUnitSphere": hook = "NextCreatureAIInsideUnitSphere"; break;
                        case "get_insideUnitCircle": hook = "NextCreatureAIInsideUnitCircle"; break;
                        case "Range": hook = call.ReturnType == typeof(int) ? "NextCreatureAIRangeInt" : "NextCreatureAIRangeFloat"; break;
                    }
                }
                if (hook == null) { yield return instruction; continue; }
                // Preserve jump labels and exception boundaries on the argument load.
                instruction.opcode = OpCodes.Ldarg_0;
                instruction.operand = null;
                yield return instruction;
                yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(SeedGeneration), hook));
            }
        }
    }
}
