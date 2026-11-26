using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace ItsFrosti.SpeedrunningMod.RunTracking
{
    internal static class PortalLoadRuntime
    {
        private static readonly PortalLoadPolicy Policy = new PortalLoadPolicy();
        internal static PrecursorTeleporter CurrentTeleporter;
        private static Player player;
        private static Pickupable quartz, coral, tank;
        private static bool aquariumPair, tankRemoved, warned;
        private static int exceptionFrame = -1;
        private static int inventoryMoveDepth;

        internal static void BeginPortal()
        {
            PrecursorTeleporter source = CurrentTeleporter;
            Reset();
            if (!SingleplayerRunRuntimeHost.IsTimerRunning) return;
            try
            {
                player = Player.main;
                if (player == null || source == null) throw new InvalidOperationException("Portal source is unavailable.");
                // The game's own aquarium volume identifies either end of this pair.
                aquariumPair = PrisonManager.IsInsideAquarium(source.transform.position) ||
                    PrisonManager.IsInsideAquarium(source.warpToPos);
                bool pdaOpen = IsPdaOpen();
                Inventory inventory = Inventory.main;
                if (inventory == null) throw new InvalidOperationException("Inventory is unavailable.");
                InventoryItem equipped = inventory.equipment.GetItemInSlot("Tank");
                bool eligible = pdaOpen && inventory.container.GetCount(TechType.Quartz) == 1 &&
                    inventory.container.GetCount(TechType.JeweledDiskPiece) == 1 &&
                    inventory.container.GetCount(TechType.HatchingEnzymes) >= 1 &&
                    equipped != null && equipped.item != null && equipped.item.GetTechType() == TechType.DoubleTank;
                if (eligible)
                {
                    quartz = inventory.container.GetItems(TechType.Quartz)[0].item;
                    coral = inventory.container.GetItems(TechType.JeweledDiskPiece)[0].item;
                    tank = equipped.item;
                }
                Policy.Begin(CategoryAllowsRemoval(), pdaOpen, eligible);
            }
            catch (Exception error) { Fail(error); }
        }

        private static bool CategoryAllowsRemoval()
        {
            return Utils.GetLegacyGameMode() == GameMode.Creative || !aquariumPair ||
                KnownTech.Contains(TechType.HatchingEnzymes);
        }
        private static bool IsPdaOpen()
        {
            PDA pda = player.GetPDA();
            if (pda == null) throw new InvalidOperationException("PDA state is unavailable.");
            // Opening and Closing are both active PDA use, too.
            return pda.state != PDA.State.Closed;
        }
        internal static void BeginLoading(Player instance)
        {
            if (!Policy.Active || instance != player) return;
            Policy.BeginLoading();
            Refresh(false);
        }
        internal static void EndLoading()
        {
            Policy.EndLoading();
            SingleplayerRunRuntimeHost.SetPortalLoadRemoval(false);
        }
        internal static void Refresh() { Refresh(true); }
        private static void Refresh(bool checkNativeWait)
        {
            if (!Policy.Active) return;
            try
            {
                if (!SingleplayerRunRuntimeHost.IsTimerRunning || player == null || Player.main != player)
                {
                    Reset();
                    return;
                }
                if (player.liveMixin == null) throw new InvalidOperationException("Player health is unavailable.");
                if (!player.liveMixin.IsAlive())
                {
                    Policy.Fail();
                    EndLoading();
                    return;
                }
                if (Policy.Loading && checkNativeWait && !player.IsInvoking("CheckTeleportationComplete"))
                {
                    EndLoading();
                    return;
                }
                Inventory inventory = Inventory.main;
                if (inventory == null) throw new InvalidOperationException("Inventory is unavailable.");
                bool enzymesPresent = inventory.container.GetCount(TechType.HatchingEnzymes) >= 1;
                bool tankInBag = tank != null && inventory.container.Contains(tank) &&
                    inventory.equipment.GetItemInSlot("Tank") == null;
                Policy.RecordTankUnequip(tankRemoved && tankInBag);
                bool inventoryValid = tankInBag && inventory.container.GetCount(TechType.Quartz) == 0 &&
                    inventory.container.GetCount(TechType.JeweledDiskPiece) == 0;
                bool wasReady = Policy.ExceptionReady;
                Policy.Observe(CategoryAllowsRemoval(), IsPdaOpen(), enzymesPresent, inventoryValid);
                if (!wasReady && Policy.ExceptionReady) exceptionFrame = Time.frameCount;
                // The click performing the final required action is not a later input.
                if (Policy.ExceptionReady && Time.frameCount != exceptionFrame && HasMouseInput()) Policy.MouseInput();
                SingleplayerRunRuntimeHost.SetPortalLoadRemoval(Policy.CanRemove);
            }
            catch (Exception error) { Fail(error); }
        }
        private static bool HasMouseInput()
        {
            for (int i = 0; i < 7; i++) if (Input.GetMouseButtonDown(i)) return true;
            return Input.mouseScrollDelta.sqrMagnitude > 0f;
        }
        internal static bool IsTrackedDrop(Inventory inventory, Pickupable item)
        {
            return Policy.Active && inventory == Inventory.main && item != null && (item == quartz || item == coral);
        }
        internal static void Dropped(Pickupable item, bool success)
        {
            if (!Policy.Active) return;
            try
            {
                bool confirmed = success && item != null && !Inventory.main.container.Contains(item) && !item.attached;
                if (item == quartz) Policy.RecordQuartzDrop(confirmed);
                if (item == coral) Policy.RecordCoralDrop(confirmed);
                Refresh();
            }
            catch (Exception error) { Fail(error); }
        }
        internal static void BeginInventoryMove() { inventoryMoveDepth++; }
        internal static void EndInventoryMove()
        {
            if (inventoryMoveDepth > 0) inventoryMoveDepth--;
            Refresh();
        }
        internal static Pickupable CaptureTankRemoval(Equipment equipment, string slot)
        {
            if (!Policy.Active || inventoryMoveDepth == 0 || tank == null || Inventory.main == null || equipment != Inventory.main.equipment || slot != "Tank") return null;
            try
            {
                InventoryItem item = equipment.GetItemInSlot(slot);
                return item != null && item.item == tank ? tank : null;
            }
            catch (Exception error) { Fail(error); return null; }
        }
        internal static void TankRemoved(Pickupable captured, InventoryItem result)
        {
            if (captured != null && captured == tank && result != null && result.item == tank) tankRemoved = true;
            Refresh();
        }
        internal static void NativeFailure(Exception error)
        {
            if (error != null && Policy.Active) Fail(error);
        }
        internal static void Fail(Exception error)
        {
            Policy.Fail();
            SingleplayerRunRuntimeHost.SetPortalLoadRemoval(false);
            if (!warned && !Plugin.IsShuttingDown)
            {
                warned = true;
                Plugin.Logger.Warning("Portal load removal disabled; timer continues: " + error.Message);
            }
        }
        internal static void Reset()
        {
            SingleplayerRunRuntimeHost.SetPortalLoadRemoval(false);
            Policy.Reset();
            CurrentTeleporter = null;
            player = null;
            quartz = coral = tank = null;
            aquariumPair = tankRemoved = warned = false;
            exceptionFrame = -1;
            inventoryMoveDepth = 0;
        }
    }

    [HarmonyPatch(typeof(PrecursorTeleporter), "BeginTeleportPlayer")]
    internal static class PortalSourceHook
    {
        private static void Prefix(PrecursorTeleporter __instance, out PrecursorTeleporter __state)
        {
            __state = PortalLoadRuntime.CurrentTeleporter;
            PortalLoadRuntime.CurrentTeleporter = __instance;
        }
        private static Exception Finalizer(Exception __exception, PrecursorTeleporter __state)
        {
            PortalLoadRuntime.CurrentTeleporter = __state;
            if (__exception != null) PortalLoadRuntime.NativeFailure(__exception);
            return __exception;
        }
    }
    [HarmonyPatch(typeof(Player), "WaitForTeleportation")]
    internal static class PortalWaitHook
    {
        private static void Prefix(Player __instance) { PortalLoadRuntime.BeginLoading(__instance); }
        private static Exception Finalizer(Exception __exception)
        {
            if (__exception != null) PortalLoadRuntime.NativeFailure(__exception);
            else PortalLoadRuntime.Refresh();
            return __exception;
        }
    }
    [HarmonyPatch(typeof(PrecursorTeleporter), "TeleportationComplete")]
    internal static class PortalCompleteHook
    {
        private static void Prefix() { PortalLoadRuntime.EndLoading(); }
    }
    [HarmonyPatch(typeof(Inventory), "InternalDropItem")]
    internal static class PortalDropHook
    {
        private static void Prefix(Inventory __instance, Pickupable __0, out bool __state)
        {
            __state = PortalLoadRuntime.IsTrackedDrop(__instance, __0);
        }
        private static void Postfix(Pickupable __0, bool __result, bool __state)
        {
            if (__state) PortalLoadRuntime.Dropped(__0, __result);
        }
        private static Exception Finalizer(Exception __exception) { PortalLoadRuntime.NativeFailure(__exception); return __exception; }
    }
    [HarmonyPatch(typeof(Equipment), "RemoveItem", new[] { typeof(string), typeof(bool), typeof(bool) })]
    internal static class PortalUnequipHook
    {
        private static void Prefix(Equipment __instance, string __0, out Pickupable __state)
        {
            __state = PortalLoadRuntime.CaptureTankRemoval(__instance, __0);
        }
        private static void Postfix(Pickupable __state, InventoryItem __result)
        {
            if (__state != null) PortalLoadRuntime.TankRemoved(__state, __result);
        }
        private static Exception Finalizer(Exception __exception) { PortalLoadRuntime.NativeFailure(__exception); return __exception; }
    }
    [HarmonyPatch]
    internal static class PortalInventoryRefreshHook
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            foreach (MethodInfo method in typeof(Inventory).GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
                if (method.Name == "AddOrSwap" || method.Name == "ExecuteItemAction") yield return method;
        }
        private static void Prefix(MethodBase __originalMethod)
        {
            if (__originalMethod.Name == "AddOrSwap") PortalLoadRuntime.BeginInventoryMove();
        }
        private static void Postfix() { PortalLoadRuntime.Refresh(); }
        private static Exception Finalizer(MethodBase __originalMethod, Exception __exception)
        {
            if (__originalMethod.Name == "AddOrSwap") PortalLoadRuntime.EndInventoryMove();
            PortalLoadRuntime.NativeFailure(__exception);
            return __exception;
        }
    }
    [HarmonyPatch]
    internal static class PortalPdaRefreshHook
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(PDA), "Open");
            yield return AccessTools.Method(typeof(PDA), "Close");
            yield return AccessTools.Method(typeof(PDA), "Update");
        }
        private static void Postfix() { PortalLoadRuntime.Refresh(); }
        private static Exception Finalizer(Exception __exception) { PortalLoadRuntime.NativeFailure(__exception); return __exception; }
    }
}
