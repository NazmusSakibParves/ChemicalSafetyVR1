using System;
using UnityEngine;

namespace ChemicalSafety.Presentable
{
    /// <summary>
    /// Previous / Next scenario buttons (client, 2026-11-12): the learner may leave a scenario before it is finished and go to the one before or
    /// after it. This is the single list of scenarios and the request channel; the director does the jump (fade, reload the scene, start at
    /// the scenario) and the marks keep every answer already given (see <c>SessionResults.TryGetAnswered</c>).
    /// New scenarios (SC19-22): raise <see cref="Count"/> and add the scenario to <c>RunScenarioByNumber</c> in the director, nothing else.
    /// </summary>
    public static class ScenarioNav
    {
        /// <summary>Master switch: false = no Previous / Next buttons at all, the flow is exactly the old one.</summary>
        public const bool Enabled = true;

        /// <summary>Number of scenarios the buttons can go through (the last built scenario).</summary>
        public const int Count = 18;

        /// <summary>The scenario that is running now (1-based). Kept up to date by the director from its stage names.</summary>
        public static int Current { get; set; } = 1;

        /// <summary>Set when a jump was requested and the scene is reloading; read once by the director at the start of the new scene.</summary>
        public static int JumpTarget { get; set; }

        public static bool CanPrevious => Enabled && Current > 1;
        public static bool CanNext => Enabled && Current < Count;

        /// <summary>Raised by the HUD buttons with the scenario number to go to.</summary>
        public static event Action<int> JumpRequested;

        public static void RequestPrevious()
        {
            if (CanPrevious)
                JumpRequested?.Invoke(Current - 1);
        }

        public static void RequestNext()
        {
            if (CanNext)
                JumpRequested?.Invoke(Current + 1);
        }

        /// <summary>Returns the pending jump target (0 = none) and clears it.</summary>
        public static int TakeJump()
        {
            var t = JumpTarget;
            JumpTarget = 0;
            return t;
        }

        // Editor Play Mode may keep statics between plays (domain reload off): every new play starts clean.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Current = 1;
            JumpTarget = 0;
            JumpRequested = null;
        }
    }
}
