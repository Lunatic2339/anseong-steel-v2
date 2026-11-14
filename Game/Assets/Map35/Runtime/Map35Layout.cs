using UnityEngine;

namespace Anseong.Map35
{
    // Unity xyz, metres. Map document tables use (x,z;height y).
    public static class Map35Layout
    {
        public static readonly Vector3[] Mech = {
            new Vector3(0,0,-20),new Vector3(0,0,0),new Vector3(0,0,45),new Vector3(0,0,65),
            new Vector3(0,0,130),new Vector3(0,0,170),new Vector3(0,0,250),new Vector3(0,0,285),
            new Vector3(60,0,285),new Vector3(60,0,340),new Vector3(60,0,354),new Vector3(42,0,354),new Vector3(60,0,354)};
        public static readonly string[] MechNames={"M0","M1","M2","M3","M4","M5","M6","M7 WAIT","M8","M9","M10","M11a","M10 return"};
        public static readonly Vector3[] Drone = {
            new Vector3(12,30,-15),new Vector3(8,30,-8),new Vector3(8,30,8),new Vector3(16,30,8),new Vector3(25,30,45),
            new Vector3(-25,35,100),new Vector3(-30,35,170),new Vector3(-40,38,215),new Vector3(-20,38,285),
            new Vector3(60,42,305),new Vector3(130,48,360),new Vector3(130,48,405),new Vector3(130,48,445)};
        public static readonly string[] DroneNames={"D0","door in","door out","D1","D2","D3","D4","D5","D7 WAIT","D8","D9","D10","D11"};
        public static readonly Vector3[] DroneOptional = {new Vector3(-40,38,215),new Vector3(-40,50,215),new Vector3(0,50,245),new Vector3(-20,38,285)};
        public static readonly Vector3[] BossRoute = {new Vector3(0,0,215),new Vector3(115,0,215),new Vector3(115,0,270),new Vector3(115,0,315),new Vector3(60,0,315),new Vector3(60,0,405)};
        public static readonly string[] ControlNames={"K0 Hangar","K1 Departure","K2 Yard","K2b Nerve","K3 Crane yard","K3b Drop side"};
        // K1 moved northwest: a clear point ray from the southwest still showed the plate edge-on.
        // K0 shows the push face; K1 shows the yard and plate status from the opposite side.
        public static readonly Vector3[] Control={new Vector3(-18,32,-35),new Vector3(-65,65,65),new Vector3(-130,100,100),new Vector3(-70,45,215),new Vector3(-95,110,290),new Vector3(135,60,405)};
        public static readonly Vector3[] Look={new Vector3(0,24,-5),new Vector3(0,18,25),new Vector3(0,25,200),new Vector3(-20,34,198),new Vector3(60,25,395),new Vector3(60,30,405)};
        public static readonly float[] VerticalFov={60,60,60,50,60,55};
        public static float Length(Vector3[] points){float n=0;for(int i=1;i<points.Length;i++)n+=Vector3.Distance(points[i-1],points[i]);return n;}
    }
}
