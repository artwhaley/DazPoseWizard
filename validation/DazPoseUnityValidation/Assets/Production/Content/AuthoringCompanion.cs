using DazPose.Performer;
using UnityEngine;
namespace DazPose.Content {
 public sealed class AuthoringCompanion : PerformanceSequence {
  [SerializeField] public Transform home,walkMark,playerHead,actorFocus;
  [SerializeField] public PerformerPose standingPose;
  [SerializeField] public PerformerSeat seat;
  [SerializeField] public PerformerAction celebration;
  [SerializeField] public PerformerSpell spell;
  [SerializeField] public PerformerAura aura;
  public override async Awaitable ExecuteAsync(PerformanceRun run) {
   var actor=run.Performer;
   if(actor.IsSeated) await actor.StandUpAsync();
   if(actor.VisibilityState==PerformerVisibilityState.Hidden) await actor.DissolveInAtAsync(home,1.2f,standingPose);
   else { await actor.TeleportToAsync(home,standingPose); await actor.PoseAsync(standingPose); }
   await actor.OutfitAsync("first-outfit");
   await actor.OutfitAsync("first-outfit"); // intentional already-equipped no-op
   await actor.TryRemoveLayerAsync();
   await actor.TryRemoveLayerAsync();
   await actor.TryRemoveLayerAsync(); // intentional nothing-left no-op
   await actor.TryAddLayerAsync();
   await actor.TryAddLayerAsync();
   await actor.TryAddLayerAsync(); // intentional fully-dressed no-op
   await actor.SetHairAsync("none");
   await actor.SetHairAsync("first-outfit/EmikoHair_59875");
   actor.LookAt(playerHead);
   run.Player.Track(actorFocus,.8f);
   actor.Aura(aura);
   await run.Delay(3);
   await actor.WalkToAsync(walkMark,LocomotionArrivalBehavior.Hold);
   await actor.TurnToAsync(playerHead);
   await run.Delay(2);
   await actor.SitAtAsync(seat);
   await run.Delay(5);
   await actor.StandUpAsync();
   await actor.PerformAsync(celebration);
   await actor.CastAsync(spell,actorFocus);
   actor.ClearAura();
   await actor.TeleportToAsync(home,standingPose);
   await run.Delay(2);
   run.Player.StopTracking();
   actor.ClearGaze();
   await actor.DissolveOutAsync(1.2f);
  }
 }
}