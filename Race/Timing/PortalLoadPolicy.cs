namespace ItsFrosti.SpeedrunningMod.RunTracking
{
    // Observations only. This policy never changes inventory, input or teleportation.
    internal sealed class PortalLoadPolicy
    {
        internal bool Active { get; private set; }
        internal bool Loading { get; private set; }
        internal bool ExceptionReady { get; private set; }
        private bool allowed, pdaOpen, eligible, quartzDropped, coralDropped, tankUnequipped;
        private bool interrupted, failed;
        internal bool CanRemove
        {
            get { return Active && Loading && allowed && !failed && !interrupted && (!pdaOpen || ExceptionReady); }
        }
        internal void Begin(bool categoryAllowed, bool pdaIsOpen, bool exceptionEligible)
        {
            Reset();
            Active = true;
            allowed = categoryAllowed;
            pdaOpen = pdaIsOpen;
            eligible = pdaIsOpen && exceptionEligible;
        }
        internal void BeginLoading() { if (Active) Loading = true; }
        internal void EndLoading() { Loading = false; }
        internal void RecordQuartzDrop(bool success) { if (Active && eligible && success) quartzDropped = true; }
        internal void RecordCoralDrop(bool success) { if (Active && eligible && success) coralDropped = true; }
        internal void RecordTankUnequip(bool success) { if (Active && eligible && success) tankUnequipped = true; }
        internal void Observe(bool categoryAllowed, bool pdaIsOpen, bool enzymesInInventory, bool exceptionInventoryValid)
        {
            allowed = categoryAllowed;
            pdaOpen = pdaIsOpen;
            if (!enzymesInInventory) eligible = false;
            if (ExceptionReady && (!enzymesInInventory || !exceptionInventoryValid)) interrupted = true;
            if (eligible && quartzDropped && coralDropped && tankUnequipped && enzymesInInventory && exceptionInventoryValid)
                ExceptionReady = true;
        }
        internal void MouseInput() { if (ExceptionReady) interrupted = true; }
        internal void Fail() { failed = true; }
        internal void Reset()
        {
            Active = Loading = ExceptionReady = false;
            allowed = pdaOpen = eligible = quartzDropped = coralDropped = tankUnequipped = interrupted = failed = false;
        }
    }
}
