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
            if (potato == null || !potato.IsCometOrAsteroid())
                return;

            var isUntouched = potato.IsUntouchedSpaceObject();
            var hasAuthority = isUntouched
                ? LockSystem.LockQuery.AsteroidCometLockBelongsToPlayer(SettingsSystem.CurrentSettings.PlayerName)
                : LockSystem.LockQuery.ControlLockBelongsToPlayer(potato.id, SettingsSystem.CurrentSettings.PlayerName);

            if (!hasAuthority)
                return;

            LunaLog.Log($"Started to track comet/asteroid {potato.id}");
            VesselProtoSystem.Singleton.MessageSender.SendVesselMessage(potato, true);
        }

        public void StopTrackingCometOrAsteroid(Vessel potato)
        {
            if (potato == null || !potato.IsCometOrAsteroid())
                return;

            var isUntouched = potato.IsUntouchedSpaceObject();
            var hasAuthority = isUntouched
                ? LockSystem.LockQuery.AsteroidCometLockBelongsToPlayer(SettingsSystem.CurrentSettings.PlayerName)
                : LockSystem.LockQuery.ControlLockBelongsToPlayer(potato.id, SettingsSystem.CurrentSettings.PlayerName);

            if (!hasAuthority)
                return;

            LunaLog.Log($"Stopped to track comet/asteroid {potato.id}");
            VesselProtoSystem.Singleton.MessageSender.SendVesselMessage(potato, true);
        }

        /// <summary>
        /// This event is called when accepting a recoverasset contract or when an asteroid spawns
        /// </summary>
        public void NewVesselCreated(Vessel vessel)
        {
            if (vessel == null || !vessel.IsCometOrAsteroid())
                return;

            var isUntouched = vessel.IsUntouchedSpaceObject();
            var hasAuthority = isUntouched
                ? LockSystem.LockQuery.AsteroidCometLockBelongsToPlayer(SettingsSystem.CurrentSettings.PlayerName)
                : LockSystem.LockQuery.ControlLockBelongsToPlayer(vessel.id, SettingsSystem.CurrentSettings.PlayerName);

            if (!hasAuthority)
                return;

            VesselProtoSystem.Singleton.MessageSender.SendVesselMessage(vessel, true);
        }
    }
}
