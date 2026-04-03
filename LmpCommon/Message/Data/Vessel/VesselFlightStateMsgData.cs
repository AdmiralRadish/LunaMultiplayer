using Lidgren.Network;
using LmpCommon.Message.Types;
using System;

namespace LmpCommon.Message.Data.Vessel
{
    public class VesselFlightStateMsgData : VesselBaseMsgData
    {
        private const int SignedControlBits = 12;
        private const int UnitControlBits = 10;
        private const float ZeroThreshold = 0.0001f;

        [System.Flags]
        private enum FlightStateFieldFlags : ushort
        {
            None = 0,
            MainThrottle = 1 << 0,
            WheelThrottle = 1 << 1,
            WheelThrottleTrim = 1 << 2,
            X = 1 << 3,
            Y = 1 << 4,
            Z = 1 << 5,
            Pitch = 1 << 6,
            Roll = 1 << 7,
            Yaw = 1 << 8,
            PitchTrim = 1 << 9,
            RollTrim = 1 << 10,
            YawTrim = 1 << 11,
            WheelSteer = 1 << 12,
            WheelSteerTrim = 1 << 13,
        }

        [System.Flags]
        private enum FlightStateBoolFlags : byte
        {
            None = 0,
            KillRot = 1 << 0,
            GearUp = 1 << 1,
            GearDown = 1 << 2,
            Headlight = 1 << 3,
        }

        /// <inheritdoc />
        internal VesselFlightStateMsgData() { }
        public override VesselMessageType VesselMessageType => VesselMessageType.Flightstate;

        //Avoid using reference types in this message as it can generate allocations and is sent VERY often.
        public int SubspaceId;
        public float PingSec;
        public float MainThrottle;
        public float WheelThrottleTrim;
        public float X;
        public float Y;
        public float Z;
        public bool KillRot;
        public bool GearUp;
        public bool GearDown;
        public bool Headlight;
        public float WheelThrottle;
        public float Pitch;
        public float Roll;
        public float Yaw;
        public float PitchTrim;
        public float RollTrim;
        public float YawTrim;
        public float WheelSteer;
        public float WheelSteerTrim;

        public override string ClassName { get; } = nameof(VesselFlightStateMsgData);

        internal override void InternalSerialize(NetOutgoingMessage lidgrenMsg)
        {
            base.InternalSerialize(lidgrenMsg);

            lidgrenMsg.Write(SubspaceId);
            lidgrenMsg.Write(PingSec);

            var fieldFlags = BuildFieldFlags();
            var boolFlags = BuildBoolFlags();

            lidgrenMsg.Write((ushort)fieldFlags);
            lidgrenMsg.Write((byte)boolFlags);

            WriteUnitIfPresent(lidgrenMsg, fieldFlags, FlightStateFieldFlags.MainThrottle, MainThrottle);
            WriteSignedIfPresent(lidgrenMsg, fieldFlags, FlightStateFieldFlags.WheelThrottle, WheelThrottle);
            WriteSignedIfPresent(lidgrenMsg, fieldFlags, FlightStateFieldFlags.WheelThrottleTrim, WheelThrottleTrim);
            WriteSignedIfPresent(lidgrenMsg, fieldFlags, FlightStateFieldFlags.X, X);
            WriteSignedIfPresent(lidgrenMsg, fieldFlags, FlightStateFieldFlags.Y, Y);
            WriteSignedIfPresent(lidgrenMsg, fieldFlags, FlightStateFieldFlags.Z, Z);
            WriteSignedIfPresent(lidgrenMsg, fieldFlags, FlightStateFieldFlags.Pitch, Pitch);
            WriteSignedIfPresent(lidgrenMsg, fieldFlags, FlightStateFieldFlags.Roll, Roll);
            WriteSignedIfPresent(lidgrenMsg, fieldFlags, FlightStateFieldFlags.Yaw, Yaw);
            WriteSignedIfPresent(lidgrenMsg, fieldFlags, FlightStateFieldFlags.PitchTrim, PitchTrim);
            WriteSignedIfPresent(lidgrenMsg, fieldFlags, FlightStateFieldFlags.RollTrim, RollTrim);
            WriteSignedIfPresent(lidgrenMsg, fieldFlags, FlightStateFieldFlags.YawTrim, YawTrim);
            WriteSignedIfPresent(lidgrenMsg, fieldFlags, FlightStateFieldFlags.WheelSteer, WheelSteer);
            WriteSignedIfPresent(lidgrenMsg, fieldFlags, FlightStateFieldFlags.WheelSteerTrim, WheelSteerTrim);
        }

        internal override void InternalDeserialize(NetIncomingMessage lidgrenMsg)
        {
            base.InternalDeserialize(lidgrenMsg);

            SubspaceId = lidgrenMsg.ReadInt32();
            PingSec = lidgrenMsg.ReadFloat();

            var fieldFlags = (FlightStateFieldFlags)lidgrenMsg.ReadUInt16();
            var boolFlags = (FlightStateBoolFlags)lidgrenMsg.ReadByte();

            MainThrottle = ReadUnitIfPresent(lidgrenMsg, fieldFlags, FlightStateFieldFlags.MainThrottle);
            WheelThrottle = ReadSignedIfPresent(lidgrenMsg, fieldFlags, FlightStateFieldFlags.WheelThrottle);
            WheelThrottleTrim = ReadSignedIfPresent(lidgrenMsg, fieldFlags, FlightStateFieldFlags.WheelThrottleTrim);
            X = ReadSignedIfPresent(lidgrenMsg, fieldFlags, FlightStateFieldFlags.X);
            Y = ReadSignedIfPresent(lidgrenMsg, fieldFlags, FlightStateFieldFlags.Y);
            Z = ReadSignedIfPresent(lidgrenMsg, fieldFlags, FlightStateFieldFlags.Z);
            KillRot = boolFlags.HasFlag(FlightStateBoolFlags.KillRot);
            GearUp = boolFlags.HasFlag(FlightStateBoolFlags.GearUp);
            GearDown = boolFlags.HasFlag(FlightStateBoolFlags.GearDown);
            Headlight = boolFlags.HasFlag(FlightStateBoolFlags.Headlight);
            Pitch = ReadSignedIfPresent(lidgrenMsg, fieldFlags, FlightStateFieldFlags.Pitch);
            Roll = ReadSignedIfPresent(lidgrenMsg, fieldFlags, FlightStateFieldFlags.Roll);
            Yaw = ReadSignedIfPresent(lidgrenMsg, fieldFlags, FlightStateFieldFlags.Yaw);
            PitchTrim = ReadSignedIfPresent(lidgrenMsg, fieldFlags, FlightStateFieldFlags.PitchTrim);
            RollTrim = ReadSignedIfPresent(lidgrenMsg, fieldFlags, FlightStateFieldFlags.RollTrim);
            YawTrim = ReadSignedIfPresent(lidgrenMsg, fieldFlags, FlightStateFieldFlags.YawTrim);
            WheelSteer = ReadSignedIfPresent(lidgrenMsg, fieldFlags, FlightStateFieldFlags.WheelSteer);
            WheelSteerTrim = ReadSignedIfPresent(lidgrenMsg, fieldFlags, FlightStateFieldFlags.WheelSteerTrim);
        }

        internal override int InternalGetMessageSize()
        {
            return base.InternalGetMessageSize() + sizeof(int) + sizeof(float) * 15 + sizeof(bool) * 4;
        }

        private FlightStateFieldFlags BuildFieldFlags()
        {
            var flags = FlightStateFieldFlags.None;

            AddIfNonZero(ref flags, FlightStateFieldFlags.MainThrottle, MainThrottle);
            AddIfNonZero(ref flags, FlightStateFieldFlags.WheelThrottle, WheelThrottle);
            AddIfNonZero(ref flags, FlightStateFieldFlags.WheelThrottleTrim, WheelThrottleTrim);
            AddIfNonZero(ref flags, FlightStateFieldFlags.X, X);
            AddIfNonZero(ref flags, FlightStateFieldFlags.Y, Y);
            AddIfNonZero(ref flags, FlightStateFieldFlags.Z, Z);
            AddIfNonZero(ref flags, FlightStateFieldFlags.Pitch, Pitch);
            AddIfNonZero(ref flags, FlightStateFieldFlags.Roll, Roll);
            AddIfNonZero(ref flags, FlightStateFieldFlags.Yaw, Yaw);
            AddIfNonZero(ref flags, FlightStateFieldFlags.PitchTrim, PitchTrim);
            AddIfNonZero(ref flags, FlightStateFieldFlags.RollTrim, RollTrim);
            AddIfNonZero(ref flags, FlightStateFieldFlags.YawTrim, YawTrim);
            AddIfNonZero(ref flags, FlightStateFieldFlags.WheelSteer, WheelSteer);
            AddIfNonZero(ref flags, FlightStateFieldFlags.WheelSteerTrim, WheelSteerTrim);

            return flags;
        }

        private FlightStateBoolFlags BuildBoolFlags()
        {
            var flags = FlightStateBoolFlags.None;
            if (KillRot) flags |= FlightStateBoolFlags.KillRot;
            if (GearUp) flags |= FlightStateBoolFlags.GearUp;
            if (GearDown) flags |= FlightStateBoolFlags.GearDown;
            if (Headlight) flags |= FlightStateBoolFlags.Headlight;
            return flags;
        }

        private static void AddIfNonZero(ref FlightStateFieldFlags flags, FlightStateFieldFlags field, float value)
        {
            if (Math.Abs(value) > ZeroThreshold)
                flags |= field;
        }

        private static void WriteSignedIfPresent(NetOutgoingMessage lidgrenMsg, FlightStateFieldFlags flags, FlightStateFieldFlags field, float value)
        {
            if (flags.HasFlag(field))
                lidgrenMsg.WriteSignedSingle(Math.Max(-1f, Math.Min(1f, value)), SignedControlBits);
        }

        private static void WriteUnitIfPresent(NetOutgoingMessage lidgrenMsg, FlightStateFieldFlags flags, FlightStateFieldFlags field, float value)
        {
            if (flags.HasFlag(field))
                lidgrenMsg.WriteUnitSingle(Math.Max(0f, Math.Min(1f, value)), UnitControlBits);
        }

        private static float ReadSignedIfPresent(NetIncomingMessage lidgrenMsg, FlightStateFieldFlags flags, FlightStateFieldFlags field)
        {
            return flags.HasFlag(field) ? lidgrenMsg.ReadSignedSingle(SignedControlBits) : 0f;
        }

        private static float ReadUnitIfPresent(NetIncomingMessage lidgrenMsg, FlightStateFieldFlags flags, FlightStateFieldFlags field)
        {
            return flags.HasFlag(field) ? lidgrenMsg.ReadUnitSingle(UnitControlBits) : 0f;
        }
    }
}
