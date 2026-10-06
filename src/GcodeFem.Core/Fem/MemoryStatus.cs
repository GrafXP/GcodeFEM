using System.Runtime.InteropServices;

namespace GcodeFem.Core.Fem;

/// <summary>How much memory a solve can still claim on this machine.</summary>
public static partial class MemoryStatus
{
    /// <summary>
    /// Physical is the RAM that is free right now. Commit is what this process can still allocate at
    /// all: RAM plus page file, minus what every process already holds. A native allocation beyond
    /// the commit limit does not fail politely, it takes the process down, so solvers check it first.
    /// Outside Windows only the physical figure is known and Commit repeats it.
    /// </summary>
    public static (long Physical, long Commit) Available()
    {
        if (OperatingSystem.IsWindows())
        {
            var status = new MemoryStatusEx { Length = (uint)Marshal.SizeOf<MemoryStatusEx>() };
            if (GlobalMemoryStatusEx(ref status)) return ((long)status.AvailablePhysical, (long)status.AvailableCommit);
        }
        var memory = GC.GetGCMemoryInfo();
        var free = memory.TotalAvailableMemoryBytes - memory.MemoryLoadBytes;
        if (memory.MemoryLoadBytes <= 0 || free <= 0) free = memory.TotalAvailableMemoryBytes / 2; // no collection has run yet
        return (free, free);
    }

    [StructLayout(LayoutKind.Sequential)]
    struct MemoryStatusEx
    {
        public uint Length, MemoryLoad;
        public ulong TotalPhysical, AvailablePhysical, CommitLimit, AvailableCommit, TotalVirtual, AvailableVirtual, AvailableExtendedVirtual;
    }

    [LibraryImport("kernel32")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GlobalMemoryStatusEx(ref MemoryStatusEx status);
}
