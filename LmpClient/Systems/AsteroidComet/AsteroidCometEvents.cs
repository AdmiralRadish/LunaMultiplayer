using LmpClient.Base;
using LmpClient.Extensions;
using LmpClient.Systems.Lock;
using LmpClient.Systems.SettingsSys;
using LmpClient.Systems.VesselProtoSys;
using LmpCommon.Locks;

namespace LmpClient.Systems.AsteroidComet
{
    public class AsteroidCometEvents : SubSystem<AsteroidCometSystem>
    {
        /// <summary>
        /// Try to get asteroid lock
        /// </summary>
        public void LockReleased(LockDefinition lockDefinition)
        {
            if (lockDefinition.Type == LockType.AsteroidComet)
            {
                System.TryGetCometAsteroidLock();
            }
        }

        /// <summary>
        /// Try to get asteroid lock when loading a level
        /// </summary>
        public void LevelLoaded(GameScenes data)
        {
            System.TryGetCometAsteroidLock();
        }

        public void StartTrackingCometOrAsteroid(Vessel potato)
        {
            LunaLog.Log($"Started to track comet/asteroid {potato.id}");
            VesselProtoSystem.Singleton.MessageSender.SendVesselMessage(potato, true);
        }

        public void StopTrackingCometOrAsteroid(Vessel potato)
        {
            LunaLog.Log($"Stopped to track comet/asteroid {potato.id}");
            VesselProtoSystem.Singleton.MessageSender.SendVesselMessage(potato, true);
        }

        /// <summary>
        /// This event is called when accepting a recoverasset contract or when an asteroid spawns
        /// </summary>
        public void NewVesselCreated(Vessel vessel)
        {
            if (vessel.IsCometOrAsteroid())
                VesselProtoSystem.Singleton.MessageSender.SendVesselMessage(vessel, true);
        }

        /// <summary>
        /// Called by KSP when any vessel crosses an SOI boundary.
        /// For asteroid/comet lineage vessels we send a proto immediately so the server
        /// records the correct SOI-relative orbit rather than the stale heliocentric one.
        /// This prevents the heliocentric→SOI-relative conversion from producing a
        /// different orbit on each client rejoin as game time advances.
        /// </summary>
        public void VesselSOIChanged(GameEvents.HostedFromToAction<Vessel, CelestialBody> data)
        {
            var vessel = data.host;
            if (vessel == null || !vessel.IsCometOrAsteroid()) return;

            // Only the client that holds the controlling lock should author the proto update.
            var playerName = SettingsSystem.CurrentSettings.PlayerName;
            var hasAuthority = LockSystem.LockQuery.AsteroidCometLockBelongsToPlayer(playerName)
                || LockSystem.LockQuery.ControlLockBelongsToPlayer(vessel.id, playerName);
            if (!hasAuthority) return;

            LunaLog.Log($"[LMP]: Asteroid/comet {vessel.id} crossed SOI boundary ({data.from?.name} → {data.to?.name}). Sending proto to sync SOI-relative orbit.");
            VesselProtoSystem.Singleton.MessageSender.SendVesselMessage(vessel, true);
        }
    }
}
