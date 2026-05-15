using Lidgren.Network;
using LmpCommon.Message.Base;
using LmpCommon.Message.Types;
using System;

namespace LmpCommon.Message.Data.Vessel
{
    public class VesselPositionMsgData : VesselBaseMsgData
    {
        private const int BodyIndexMax = 2048;
        private const int BodyAngleBits = 21;
        private const int BodyNormalBits = 14;
        private const int RotationBits = 15;
        private const int OrbitAngleBits = 20;
        private const int OrbitEccentricityBits = 20;

        [Flags]
        private enum PositionFlags : byte
        {
            None = 0,
            Landed = 1 << 0,
            Splashed = 1 << 1,
            HackingGravity = 1 << 2,
            HasBodyName = 1 << 3,
        }

        /// <inheritdoc />
        internal VesselPositionMsgData() { }
        public override VesselMessageType VesselMessageType => VesselMessageType.Position;

        //Avoid using reference types in this message as it can generate allocations and is sent VERY often.
        public string BodyName;
        public int BodyIndex;
        public int SubspaceId;
        public float PingSec;
        public float HeightFromTerrain;
        public bool Landed;
        public bool Splashed;
        public bool HackingGravity;
        public double[] LatLonAlt = new double[3];
        public double[] VelocityVector = new double[3];
        public double[] NormalVector = new double[3];
        public float[] SrfRelRotation = new float[4];
        public double[] Orbit = new double[8];

        public override string ClassName { get; } = nameof(VesselPositionMsgData);

        internal override void InternalSerialize(NetOutgoingMessage lidgrenMsg)
        {
            base.InternalSerialize(lidgrenMsg);

            var flags = BuildFlags();

            lidgrenMsg.WriteRangedInteger(0, BodyIndexMax, Math.Max(0, Math.Min(BodyIndexMax, BodyIndex)));
            lidgrenMsg.Write(SubspaceId);
            lidgrenMsg.Write(PingSec);
            lidgrenMsg.Write(HeightFromTerrain);
            lidgrenMsg.Write((byte)flags);

            lidgrenMsg.WriteRangedSingle((float)Math.Max(-90d, Math.Min(90d, LatLonAlt[0])), -90f, 90f, BodyAngleBits);
            lidgrenMsg.WriteRangedSingle((float)Math.Max(-180d, Math.Min(180d, LatLonAlt[1])), -180f, 180f, BodyAngleBits);
            lidgrenMsg.Write((float)LatLonAlt[2]);

            for (var i = 0; i < 3; i++)
                lidgrenMsg.Write((float)VelocityVector[i]);

            for (var i = 0; i < 3; i++)
                lidgrenMsg.WriteSignedSingle(ClampToSignedUnit((float)NormalVector[i]), BodyNormalBits);

            for (var i = 0; i < 4; i++)
                lidgrenMsg.WriteSignedSingle(ClampToSignedUnit(SrfRelRotation[i]), RotationBits);

            lidgrenMsg.WriteRangedSingle((float)Math.Max(0d, Math.Min(180d, Orbit[0])), 0f, 180f, OrbitAngleBits);
            lidgrenMsg.WriteRangedSingle((float)Math.Max(0d, Math.Min(16d, Orbit[1])), 0f, 16f, OrbitEccentricityBits);
            lidgrenMsg.Write(Orbit[2]);
            lidgrenMsg.WriteRangedSingle(WrapAngleDegrees(Orbit[3]), 0f, 360f, OrbitAngleBits);
            lidgrenMsg.WriteRangedSingle(WrapAngleDegrees(Orbit[4]), 0f, 360f, OrbitAngleBits);
            lidgrenMsg.WriteRangedSingle((float)Math.Max(-360d, Math.Min(360d, Orbit[5])), -360f, 360f, OrbitAngleBits);
            lidgrenMsg.Write(Orbit[6]);
            lidgrenMsg.WriteRangedInteger(0, BodyIndexMax, Math.Max(0, Math.Min(BodyIndexMax, (int)Math.Round(Orbit[7]))));

            lidgrenMsg.WritePadBits();
            if (flags.HasFlag(PositionFlags.HasBodyName))
                lidgrenMsg.Write(BodyName);
        }

        internal override void InternalDeserialize(NetIncomingMessage lidgrenMsg)
        {
            base.InternalDeserialize(lidgrenMsg);

            BodyIndex = lidgrenMsg.ReadRangedInteger(0, BodyIndexMax);
            SubspaceId = lidgrenMsg.ReadInt32();
            PingSec = lidgrenMsg.ReadFloat();
            HeightFromTerrain = lidgrenMsg.ReadFloat();

            var flags = (PositionFlags)lidgrenMsg.ReadByte();
            Landed = flags.HasFlag(PositionFlags.Landed);
            Splashed = flags.HasFlag(PositionFlags.Splashed);
            HackingGravity = flags.HasFlag(PositionFlags.HackingGravity);

            LatLonAlt[0] = lidgrenMsg.ReadRangedSingle(-90f, 90f, BodyAngleBits);
            LatLonAlt[1] = lidgrenMsg.ReadRangedSingle(-180f, 180f, BodyAngleBits);
            LatLonAlt[2] = lidgrenMsg.ReadFloat();

            for (var i = 0; i < 3; i++)
                VelocityVector[i] = lidgrenMsg.ReadFloat();

            for (var i = 0; i < 3; i++)
                NormalVector[i] = lidgrenMsg.ReadSignedSingle(BodyNormalBits);

            for (var i = 0; i < 4; i++)
                SrfRelRotation[i] = lidgrenMsg.ReadSignedSingle(RotationBits);

            Orbit[0] = lidgrenMsg.ReadRangedSingle(0f, 180f, OrbitAngleBits);
            Orbit[1] = lidgrenMsg.ReadRangedSingle(0f, 16f, OrbitEccentricityBits);
            Orbit[2] = lidgrenMsg.ReadDouble();
            Orbit[3] = lidgrenMsg.ReadRangedSingle(0f, 360f, OrbitAngleBits);
            Orbit[4] = lidgrenMsg.ReadRangedSingle(0f, 360f, OrbitAngleBits);
            Orbit[5] = lidgrenMsg.ReadRangedSingle(-360f, 360f, OrbitAngleBits);
            Orbit[6] = lidgrenMsg.ReadDouble();
            Orbit[7] = lidgrenMsg.ReadRangedInteger(0, BodyIndexMax);

            lidgrenMsg.SkipPadBits();
            if (flags.HasFlag(PositionFlags.HasBodyName) && lidgrenMsg.Position < lidgrenMsg.LengthBits)
                BodyName = lidgrenMsg.ReadString();
            else
                BodyName = string.Empty;
        }

        internal override int InternalGetMessageSize()
        {
            return base.InternalGetMessageSize() + BodyName.GetByteCount() + sizeof(int) * 2 + sizeof(float) * 2 + sizeof(bool) * 3 + sizeof(double) * 3 * 3 +
                sizeof(float) * 4 * 1 + sizeof(double) * 8;
        }

        private PositionFlags BuildFlags()
        {
            var flags = PositionFlags.None;
            if (Landed) flags |= PositionFlags.Landed;
            if (Splashed) flags |= PositionFlags.Splashed;
            if (HackingGravity) flags |= PositionFlags.HackingGravity;
            if (!string.IsNullOrEmpty(BodyName)) flags |= PositionFlags.HasBodyName;
            return flags;
        }

        private static float ClampToSignedUnit(float value)
        {
            return Math.Max(-1f, Math.Min(1f, value));
        }

        private static float WrapAngleDegrees(double value)
        {
            var wrapped = value % 360d;
            if (wrapped < 0)
                wrapped += 360d;
            return (float)wrapped;
        }
    }
}
