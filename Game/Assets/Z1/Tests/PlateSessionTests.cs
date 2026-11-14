using Anseong.Z1;
using NUnit.Framework;
using UnityEngine;

public class PlateSessionTests
{
    static PlateSession Crew(Variant mode=Variant.Drone)
    {
        var s=new PlateSession(mode);Assert.IsTrue(s.Join(17,Seat.Mech));Assert.IsTrue(s.Join(0,Seat.Support));
        s.Command(17,1,1,Request.Ready);s.Command(0,1,1,Request.Ready);return s;
    }
    static void Brace(PlateSession s)
    {
        s.Pose(17,s.State.epoch,2,new Vector3(-4,24,7.5f),new Vector3(4,24,3),true,0);
        for(int i=0;i<4;i++)s.Tick(.1f,i*.1);
    }
    [Test] public void SupportHostDoesNotTakeEitherMechHand()
    {var s=Crew(); Assert.IsTrue(s.Owns(17,Seat.Mech));Assert.IsFalse(s.Pose(0,1,2,Vector3.zero,Vector3.zero,true,0));}
    [Test] public void DuplicateSeatAndThirdPeerAreRejected()
    {var s=Crew();Assert.IsFalse(s.Join(3,Seat.Mech));Assert.IsFalse(s.Join(4,Seat.Support));Assert.IsFalse(s.Join(9,(Seat)99));}
    [Test] public void BothPlayersMustReady()
    {var s=new PlateSession(Variant.Drone);s.Join(17,Seat.Mech);s.Join(0,Seat.Support);Brace(s);Assert.AreEqual(PlatePhase.Locked,s.State.phase);}
    [Test] public void SupportCannotReleaseBeforeMechPush()
    {var s=Crew();s.Support(0,1,2,true,false,0);Assert.IsFalse(s.Command(0,1,3,Request.Release));}
    [TestCase(Variant.Drone)] [TestCase(Variant.Control)]
    public void OnePhysicalEventSupportsBothVariants(Variant mode)
    {
        var s=Crew(mode);Brace(s);
        Assert.AreEqual(PlatePhase.Supported,s.State.phase);
        for(int i=0;i<14;i++){s.Support(0,1,3+i,true,true,.4+i*.1);s.Tick(.1f,.4+i*.1);}
        Assert.IsTrue(s.Command(0,1,30,Request.Release));
        Assert.IsFalse(s.Command(0,1,30,Request.Release));Assert.IsFalse(s.Command(0,1,31,Request.Release));
        for(int i=0;i<25;i++)s.Tick(.1f,2+i*.1);
        Assert.AreEqual(PlatePhase.Open,s.State.phase);Assert.AreEqual(1,s.State.releases);
    }
    [Test] public void ControlNeedsScanButDroneDoesNot()
    {var s=Crew(Variant.Control);Brace(s);s.Support(0,1,2,true,false,.4);Assert.IsFalse(s.Command(0,1,3,Request.Release));}
    [Test] public void MechCannotReleaseOrSupportReset()
    {var s=Crew();Brace(s);Assert.IsFalse(s.Command(17,1,3,Request.Release));Assert.IsFalse(s.Command(0,1,2,Request.Reset));}
    [Test] public void LosingTrackingCancelsPartialPush()
    {var s=Crew();s.Pose(17,1,2,new Vector3(-4,24,7.5f),new Vector3(4,24,3),true,0);s.Tick(.1f,.1);s.Pose(17,1,3,Vector3.zero,Vector3.zero,false,.2);s.Tick(.1f,.2);Assert.AreEqual(0,s.State.contact);}
    [Test] public void BracedPlateDoesNotRequireHoldingArm()
    {var s=Crew();Brace(s);s.Pose(17,1,3,Vector3.zero,Vector3.zero,false,1);s.Tick(.1f,1);Assert.AreEqual(PlatePhase.Supported,s.State.phase);}
    [Test] public void MissingPoseExpiresAndCannotPush()
    {var s=Crew();s.Pose(17,1,2,new Vector3(-4,24,7.5f),new Vector3(4,24,3),true,0);s.Tick(.1f,1);Assert.IsFalse(s.State.tracking);Assert.AreEqual(0,s.State.contact);}
    [Test] public void ReconnectPreservesBraceButRequiresBothReadyAndRejectsOldEpoch()
    {var s=Crew();Brace(s);s.Leave(0);Assert.AreEqual(PlatePhase.Supported,s.State.phase);Assert.IsFalse(s.State.Running);s.Join(99,Seat.Support);Assert.IsFalse(s.Command(99,1,50,Request.Ready));s.Command(99,2,51,Request.Ready);s.Command(17,2,52,Request.Ready);Assert.IsTrue(s.State.Running);}
    [Test] public void ResetInvalidatesQueuedReleaseAndClearsCheckpoint()
    {var s=Crew();Brace(s);s.Command(17,1,3,Request.Reset);Assert.AreEqual(2,s.State.epoch);Assert.AreEqual(PlatePhase.Locked,s.State.phase);Assert.IsFalse(s.Command(0,1,40,Request.Release));Assert.IsFalse(s.State.Running);}
    [Test] public void NonfiniteAndOutOfReachHandsAreNotContact()
    {var s=Crew();s.Pose(17,1,2,new Vector3(float.NaN,0,0),new Vector3(4,24,7.5f),true,0);Assert.IsFalse(s.State.tracking);Assert.IsFalse(PlateSession.HandValid(new Vector3(100,24,8),true));}
    [Test] public void DisconnectPausesRetraction()
    {var s=Crew();Brace(s);s.Support(0,1,2,true,false,.4);s.Command(0,1,3,Request.Release);s.Tick(.1f,.4);var progress=s.State.travel;s.Leave(0);s.Tick(.1f,.5);Assert.AreEqual(progress,s.State.travel);}
    [Test] public void InterruptedScanLosesOnlyIncompleteProgress()
    {var s=Crew(Variant.Control);s.Support(0,1,2,true,true,0);s.Tick(.1f,.1);Assert.Greater(s.State.scan,0);s.Support(0,1,3,false,false,.2);s.Tick(.1f,.2);Assert.AreEqual(0,s.State.scan);}
}
