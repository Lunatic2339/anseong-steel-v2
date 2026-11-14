using System;
using System.Collections.Generic;
using UnityEngine;

namespace Anseong.Z1
{
    public enum Seat { Mech, Support }
    public enum Variant { Drone = 3, Control = 4 }
    public enum PlatePhase { Locked, Supported, Opening, Open }
    public enum Request { Ready, Release, Reset }

    [Serializable] public class PlateState
    {
        public int epoch = 1;
        public PlatePhase phase;
        public bool mechPresent, supportPresent, mechReady, supportReady, tracking;
        public float contact, scan, travel;
        public int releases;
        public Vector3 left = new Vector3(-6, 24, 3), right = new Vector3(6, 24, 3);
        public Vector3 drone = new Vector3(14, 28, 3);
        public bool Running => mechPresent && supportPresent && mechReady && supportReady;
    }

    /// <summary>Server-only rules; transport role and gameplay seat are independent.</summary>
    public sealed class PlateSession
    {
        sealed class Peer { public Seat seat; public long lastSequence = -1; }
        readonly Dictionary<ulong, Peer> peers = new Dictionary<ulong, Peer>();
        public readonly Variant Mode;
        public PlateState State { get; private set; } = new PlateState();
        double lastPose = double.NegativeInfinity;
        double lastSupport = double.NegativeInfinity;
        bool scanHeld, aimed;

        public PlateSession(Variant mode) { Mode = mode; }
        public bool Join(ulong id, Seat seat)
        {
            if (!Enum.IsDefined(typeof(Seat), seat) || peers.ContainsKey(id)) return false;
            foreach (var p in peers.Values) if (p.seat == seat) return false;
            peers.Add(id, new Peer { seat = seat });
            if (seat == Seat.Mech) State.mechPresent = true; else State.supportPresent = true;
            return true;
        }
        public bool Owns(ulong id, Seat seat) => peers.TryGetValue(id, out var p) && p.seat == seat;
        public void Leave(ulong id)
        {
            if (!peers.TryGetValue(id, out var p)) return;
            peers.Remove(id);
            if (p.seat == Seat.Mech) State.mechPresent = false; else State.supportPresent = false;
            // Invalidate queued commands; preserve physical plate/checkpoint progress.
            State.epoch++; State.mechReady = State.supportReady = false;
            State.tracking = false; State.contact = 0; scanHeld = aimed = false;
            lastPose = lastSupport = double.NegativeInfinity;
        }
        bool Accept(ulong id, int epoch, long sequence, out Peer peer)
        {
            if (!peers.TryGetValue(id, out peer) || epoch != State.epoch || sequence <= peer.lastSequence) return false;
            peer.lastSequence = sequence;
            return true;
        }
        public bool Command(ulong id, int epoch, long sequence, Request request)
        {
            if (!Accept(id, epoch, sequence, out var p)) return false;
            if (request == Request.Ready)
            {
                if (p.seat == Seat.Mech) State.mechReady = true; else State.supportReady = true;
                return true;
            }
            if (request == Request.Reset && p.seat == Seat.Mech)
            {
                State = new PlateState { epoch = State.epoch + 1, mechPresent = State.mechPresent, supportPresent = State.supportPresent };
                scanHeld = aimed = false; lastPose = lastSupport = double.NegativeInfinity;
                return true;
            }
            if (request != Request.Release || p.seat != Seat.Support || !State.Running ||
                State.phase != PlatePhase.Supported || !aimed || (Mode == Variant.Control && State.scan < 1)) return false;
            State.phase = PlatePhase.Opening; State.releases++;
            return true;
        }
        public static bool Finite(Vector3 v) => float.IsFinite(v.x) && float.IsFinite(v.y) && float.IsFinite(v.z);
        public static bool HandValid(Vector3 hand, bool left) => Finite(hand) && Vector3.Distance(hand, new Vector3(left ? -6 : 6, 27, 0)) <= 14;
        public static bool Touches(Vector3 p) => p.x >= -5.7f && p.x <= 5.7f && p.y >= 19.3f && p.y <= 28.7f && p.z >= 6.8f && p.z <= 8.8f;
        public bool Pose(ulong id, int epoch, long sequence, Vector3 left, Vector3 right, bool tracked, double now)
        {
            if (!Owns(id, Seat.Mech) || !Accept(id, epoch, sequence, out _) || !double.IsFinite(now)) return false;
            State.tracking = tracked && HandValid(left, true) && HandValid(right, false);
            if (State.tracking) { State.left = left; State.right = right; }
            lastPose = now;
            return true;
        }
        // aimedAtTarget is determined by the server's raycast, never by a client success flag.
        public bool Support(ulong id, int epoch, long sequence, bool aimedAtTarget, bool scanning, double now)
        {
            if (!Owns(id, Seat.Support) || !Accept(id, epoch, sequence, out _) || !double.IsFinite(now)) return false;
            aimed = aimedAtTarget; scanHeld = scanning; lastSupport = now;
            return true;
        }
        public void Tick(float dt, double now)
        {
            if (!float.IsFinite(dt) || dt <= 0 || dt > .2f || !double.IsFinite(now)) return;
            if (now - lastPose > .5) State.tracking = false;
            if (now - lastSupport > .5) { aimed = false; scanHeld = false; }
            if (!State.Running) { State.contact = 0; return; }
            if (State.phase == PlatePhase.Locked)
            {
                State.contact = State.tracking && (Touches(State.left) || Touches(State.right)) ? State.contact + dt : 0;
                if (State.contact >= .3f) State.phase = PlatePhase.Supported;
            }
            if (Mode == Variant.Control && State.scan < 1)
                State.scan = scanHeld && aimed ? Mathf.Min(1, State.scan + dt / 1.2f) : 0;
            if (State.phase == PlatePhase.Opening)
            {
                State.travel = Mathf.Min(1, State.travel + dt / 2f);
                if (State.travel >= 1) State.phase = PlatePhase.Open;
            }
        }
    }
}
