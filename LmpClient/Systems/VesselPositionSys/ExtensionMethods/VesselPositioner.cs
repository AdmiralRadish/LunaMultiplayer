using LmpClient.Systems.TimeSync;
using LmpCommon;
using System.Diagnostics.CodeAnalysis;
using UnityEngine;

namespace LmpClient.Systems.VesselPositionSys.ExtensionMethods
{
    public static class VesselPositioner
    {
        public static void SetVesselPosition(this Vessel vessel, VesselPositionUpdate update, VesselPositionUpdate target, float percentage)
        {
            if (vessel == null || update == null || target == null) return;

            var lerpedBody = percentage < 0.5 ? update.Body : target.Body;

            ApplyOrbitInterpolation(vessel, update, target, lerpedBody, percentage);

            //Do not use CoM. It's not needed and it generate issues when you patch the protovessel with it as it generate weird commnet lines
            //It's important to set the static pressure as otherwise the vessel situation is not updated correctly when
            //Vessel.updateSituation() is called in the Vessel.LateUpdate(). Same applies for landed and splashed
            vessel.staticPressurekPa = FlightGlobals.getStaticPressure(target.LatLonAlt[2], lerpedBody);
            vessel.heightFromTerrain = target.HeightFromTerrain;

            ApplyInterpolationsToVessel(vessel, update, target, lerpedBody, percentage);

            vessel.protoVessel.UpdatePositionValues(vessel);
        }

        private static void ApplyOrbitInterpolation(Vessel vessel, VesselPositionUpdate update, VesselPositionUpdate target, CelestialBody lerpedBody, float percentage)
        {
            // For orbiting vessels: snap orbital elements once per update segment,
            // then let KSP propagate via Kepler on intermediate frames.  Calling
            // UpdateFromStateVectors every FixedUpdate fought KSP's propagator and
            // caused visible "ticking" / jumping.  SetOrbit copies elements directly
            // without the lossy state-vector round-trip.
            if (vessel.situation > Vessel.Situations.FLYING)
            {
                if (percentage <= 0f)
                {
                    vessel.orbit.SetOrbit(
                        target.KspOrbit.inclination,
                        target.KspOrbit.eccentricity,
                        target.KspOrbit.semiMajorAxis,
                        target.KspOrbit.LAN,
                        target.KspOrbit.argumentOfPeriapsis,
                        target.KspOrbit.meanAnomalyAtEpoch,
                        target.KspOrbit.epoch,
                        target.KspOrbit.referenceBody);
                }
                return;
            }

            var currentPos = update.KspOrbit.getRelativePositionAtUT(TimeSyncSystem.UniversalTime);
            var targetPos = target.KspOrbit.getRelativePositionAtUT(TimeSyncSystem.UniversalTime);

            var currentVel = update.KspOrbit.getOrbitalVelocityAtUT(TimeSyncSystem.UniversalTime);
            var targetVel = target.KspOrbit.getOrbitalVelocityAtUT(TimeSyncSystem.UniversalTime);

            var lerpedPos = Vector3d.Lerp(currentPos, targetPos, percentage);
            var lerpedVel = Vector3d.Lerp(currentVel, targetVel, percentage);

            //This call will update the orbit PARAMETERS (ecc, sma, inc, etc) based on the vectors you pass as parameters
            //Bear in mind that this method will NOT reposition the vessel!!
            vessel.orbit.UpdateFromStateVectors(lerpedPos, lerpedVel, lerpedBody, TimeSyncSystem.UniversalTime);
        }

        private static void ApplyInterpolationsToVessel(Vessel vessel, VesselPositionUpdate update, VesselPositionUpdate target, CelestialBody lerpedBody, float percentage)
        {
            var currentSurfaceRelRotation = Quaternion.Slerp(update.SurfaceRelRotation, target.SurfaceRelRotation, percentage);

            //If you don't set srfRelRotation and vessel is packed it won't change it's rotation
            vessel.srfRelRotation = currentSurfaceRelRotation;

            vessel.Landed = percentage < 0.5 ? update.Landed : target.Landed;
            vessel.Splashed = percentage < 0.5 ? update.Splashed : target.Splashed;

            if (vessel.situation > Vessel.Situations.FLYING)
            {
                vessel.latitude = target.LatLonAlt[0];
                vessel.longitude = target.LatLonAlt[1];
                vessel.altitude = target.LatLonAlt[2];

                //For unpacked orbiting vessels, make rigidbodies kinematic so Unity's physics
                //engine cannot move them between frames.  Without this, gravity/forces shift
                //the rigidbodies each FixedUpdate and the orbit-derived position snaps them
                //back, creating visible ticking.  Kinematic bodies follow transforms directly.
                //This also prevents physics torques from drifting the vessel's attitude.
                if (vessel.loaded && !vessel.packed)
                {
                    for (var i = 0; i < vessel.parts.Count; i++)
                    {
                        if (vessel.parts[i].rb && !vessel.parts[i].rb.isKinematic)
                            vessel.parts[i].rb.isKinematic = true;
                    }
                }

                //Position vessel + parts from the orbit each frame for smooth Keplerian motion.
                //Use Planetarium.GetUniversalTime() to stay consistent with KSP's own clock.
                var orbitRotation = (Quaternion)lerpedBody.rotation * currentSurfaceRelRotation;
                var orbitPosition = vessel.orbit.getPositionAtUT(Planetarium.GetUniversalTime());
                SetVesselPositionAndRotation(vessel, orbitPosition, orbitRotation);
                return;
            }

            vessel.latitude = LunaMath.Lerp(update.LatLonAlt[0], target.LatLonAlt[0], percentage);
            vessel.longitude = LunaMath.Lerp(update.LatLonAlt[1], target.LatLonAlt[1], percentage);
            vessel.altitude = LunaMath.Lerp(update.LatLonAlt[2], target.LatLonAlt[2], percentage);

            var rotation = (Quaternion)lerpedBody.rotation * currentSurfaceRelRotation;
            var position = lerpedBody.GetWorldSurfacePosition(vessel.latitude, vessel.longitude, vessel.altitude);

            SetVesselPositionAndRotation(vessel, position, rotation);
        }

        /// <summary>
        /// Here we set the position and the rotation of every part at once, this is much more optimized than calling SetRotation and SetPosition
        /// </summary>
        [SuppressMessage("ReSharper", "ForCanBeConvertedToForeach")]
        private static void SetVesselPositionAndRotation(Vessel vessel, Vector3d position, Quaternion rotation)
        {
            if (!vessel.loaded)
            {
                vessel.vesselTransform.position = position;
                vessel.vesselTransform.rotation = rotation;
            }
            else
            {
                for (var i = 0; i < vessel.parts.Count; i++)
                {
                    var part = vessel.parts[i];
                    var partRotation = rotation * part.orgRot;
                    part.partTransform.rotation = partRotation;

                    if (vessel.packed || part.physicalSignificance == Part.PhysicalSignificance.FULL)
                    {
                        // Use the interpolated rotation for part offsets — vessel.vesselTransform.rotation
                        // is stale (previous frame) and causes rotational position lag on large vessels
                        var partPosition = position + rotation * part.orgPos;
                        part.partTransform.position = partPosition;
                    }

                    // For unpacked parts with rigidbodies, sync rb directly so the physics engine
                    // doesn't fight LMP's positioning on the next step (setting only transform.position
                    // on a non-kinematic Rigidbody causes visible oscillation as physics snaps it back)
                    if (!vessel.packed && part.rb)
                    {
                        part.rb.rotation = partRotation;
                        if (part.physicalSignificance == Part.PhysicalSignificance.FULL)
                            part.rb.position = part.partTransform.position;
                    }

                    //We always need to set the part velocity (and it's rigidbody velocity)! Otherwise during dockings it won't be possible to dock
                    part.ResumeVelocity();
                }
            }
        }
    }
}
