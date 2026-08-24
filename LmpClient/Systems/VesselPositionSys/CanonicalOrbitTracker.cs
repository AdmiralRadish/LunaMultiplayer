using LmpClient.Systems.TimeSync;
using System;
using System.Collections.Concurrent;

namespace LmpClient.Systems.VesselPositionSys
{
    /// <summary>
    /// Keeps the last canonical conic elements per vessel so the physics-integration noise that
    /// Orbit.UpdateFromStateVectors bakes into loaded vessels is NOT streamed to the server.
    /// The server persists whatever we stream, so without this every switch to/from a vessel made
    /// its stored SMA/ECC/INC/LAN/LPE random-walk by meters. Elements are only refreshed when the
    /// orbit genuinely changes (burn, SOI change, decouple...) and MNA/EPH are advanced
    /// analytically on the pristine conic so receivers see the same epoch semantics as before.
    /// </summary>
    public static class CanonicalOrbitTracker
    {
        //Observed re-derivation noise is <=1e-8 relative; real maneuvers are >=1e-6
        private const double RelSmaTolerance = 1e-7;
        private const double EccTolerance = 1e-7;
        private const double AngleToleranceDeg = 1e-5;
        //Looser gate when checking if the last RECEIVED elements still describe the local orbit
        private const double SeedToleranceFactor = 100;

        private static readonly ConcurrentDictionary<Guid, double[]> Cache = new ConcurrentDictionary<Guid, double[]>();

        public static void Clear() => Cache.Clear();

        public static void Remove(Guid vesselId) => Cache.TryRemove(vesselId, out _);

        /// <summary>
        /// Snapshot pristine elements. Call when a vessel goes off rails, before physics noise creeps in
        /// </summary>
        public static void SeedFromVessel(Vessel vessel)
        {
            if (vessel == null || vessel.orbit == null || vessel.orbit.referenceBody == null) return;
            Cache[vessel.id] = GetElements(vessel.orbit);
        }

        /// <summary>
        /// Returns the orbit elements to stream for this vessel: the held canonical conic (with MNA/EPH
        /// advanced to now) unless the orbit genuinely changed, in which case the cache is refreshed.
        /// Array layout matches VesselPositionMsgData.Orbit: inc, ecc, sma, lan, argPe, mna, epoch, refIdx
        /// </summary>
        public static double[] GetElementsToSend(Vessel vessel)
        {
            var current = GetElements(vessel.orbit);

            //Orbit is irrelevant while landed/splashed, don't hold anything
            if (vessel.LandedOrSplashed)
            {
                Cache[vessel.id] = current;
                return current;
            }

            if (Cache.TryGetValue(vessel.id, out var held) && SameConic(held, current, 1))
                return PropagateToEpoch(held, TimeSyncSystem.UniversalTime);

            //Prefer the last raw RECEIVED elements over the locally re-derived ones so interpolation
            //and subspace fix-factor artifacts are not persisted when we inherit an update lock
            var seeded = TryGetReceivedElements(vessel, current) ?? current;
            Cache[vessel.id] = seeded;
            return PropagateToEpoch(seeded, TimeSyncSystem.UniversalTime);
        }

        private static double[] GetElements(Orbit orbit)
        {
            return new[]
            {
                orbit.inclination,
                orbit.eccentricity,
                orbit.semiMajorAxis,
                orbit.LAN,
                orbit.argumentOfPeriapsis,
                orbit.meanAnomalyAtEpoch,
                orbit.epoch,
                orbit.referenceBody.flightGlobalsIndex
            };
        }

        /// <summary>
        /// True when both element sets describe the same conic within noise tolerance (MNA/EPH excluded
        /// as they advance legitimately). Any real thrust or SOI change alters at least one of these.
        /// </summary>
        private static bool SameConic(double[] a, double[] b, double toleranceFactor)
        {
            if ((int)a[7] != (int)b[7]) return false;
            if (Math.Abs(a[2] - b[2]) > RelSmaTolerance * toleranceFactor * Math.Abs(a[2])) return false;
            if (Math.Abs(a[1] - b[1]) > EccTolerance * toleranceFactor) return false;

            var angleTolerance = AngleToleranceDeg * toleranceFactor;
            return AngularDiff(a[0], b[0]) <= angleTolerance &&
                   AngularDiff(a[3], b[3]) <= angleTolerance &&
                   AngularDiff(a[4], b[4]) <= angleTolerance;
        }

        private static double AngularDiff(double a, double b)
        {
            var diff = Math.Abs(a - b) % 360;
            return diff > 180 ? 360 - diff : diff;
        }

        /// <summary>
        /// Advance MNA/EPH deterministically on the pristine conic. This mimics what the old
        /// state-vector re-derivation did to MNA/EPH (dMNA == n*dEPH) minus the element noise.
        /// </summary>
        private static double[] PropagateToEpoch(double[] elements, double ut)
        {
            var result = (double[])elements.Clone();

            var refIdx = (int)elements[7];
            if (refIdx < 0 || refIdx >= FlightGlobals.Bodies.Count) return result;

            var absSma = Math.Abs(elements[2]);
            if (absSma < 1) return result;

            var meanMotion = Math.Sqrt(FlightGlobals.Bodies[refIdx].gravParameter / (absSma * absSma * absSma));
            var mna = elements[5] + meanMotion * (ut - elements[6]);
            if (elements[1] < 1)
                mna %= 2 * Math.PI;

            result[5] = mna;
            result[6] = ut;
            return result;
        }

        private static double[] TryGetReceivedElements(Vessel vessel, double[] current)
        {
            if (!VesselPositionSystem.CurrentVesselUpdate.TryGetValue(vessel.id, out var upd) || upd?.Target == null)
                return null;

            var received = (double[])upd.Target.Orbit.Clone();
            return SameConic(received, current, SeedToleranceFactor) ? received : null;
        }
    }
}
