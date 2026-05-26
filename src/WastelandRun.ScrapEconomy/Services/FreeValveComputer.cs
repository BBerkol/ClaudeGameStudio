using System;
using System.Collections.Generic;

namespace WastelandRun.ScrapEconomy
{
    /// <summary>
    /// Computes and caches the free purge valve result for a Chopshop node visit.
    /// Per TR-card-019 and ADR-0003: deterministic from (runSeed ^ nodeIndex), ~33% probability,
    /// stable on re-entry within the same session.
    ///
    /// Owned by the Scrap Economy system (pending Scrap Economy ADR).
    /// Card System is a read-only consumer of the resolved boolean per ADR-0006.
    /// </summary>
    public interface IFreeValveComputer
    {
        /// <summary>
        /// Returns true (~33% of calls) when the free purge valve applies for this node.
        /// Result is cached per (runSeed, nodeIndex) — re-entry returns the same value without re-rolling.
        /// </summary>
        bool Compute(int runSeed, int nodeIndex);
    }

    /// <summary>
    /// Default implementation of <see cref="IFreeValveComputer"/>.
    /// One instance should be shared across all Chopshop node visits within a run session.
    /// </summary>
    public sealed class FreeValveComputer : IFreeValveComputer
    {
        private const double ValveProbability = 0.33;

        private readonly Dictionary<(int, int), bool> _cache = new Dictionary<(int, int), bool>();

        /// <inheritdoc />
        public bool Compute(int runSeed, int nodeIndex)
        {
            var key = (runSeed, nodeIndex);
            if (_cache.TryGetValue(key, out bool cached))
                return cached;

            // Entry-point seeded call per ADR-0003 Rules 1–2: constructs exactly one System.Random
            // per call. XOR derivation per ADR-0003 Rule 3 — XOR cannot overflow, no unchecked needed.
            bool result = new System.Random(runSeed ^ nodeIndex).NextDouble() < ValveProbability;
            _cache[key] = result;
            return result;
        }
    }
}
