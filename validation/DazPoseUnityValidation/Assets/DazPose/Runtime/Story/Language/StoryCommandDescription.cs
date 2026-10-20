using System;
using System.Linq;
namespace DazPose.Story.Language {
 public static class StoryCommandDescription {
  public static string Describe(StoryCommand command) {
   string Arg(string name)=>command.Arguments.TryGetValue(name,out var value) ? value.ToString() : "";
   switch(command.Verb) {
    case "Lara.StartHiddenAt":return "Place Lara hidden at "+Arg("location");
    case "PlayerBody.StartHiddenAt":return "Place the prepared male player body hidden at "+Arg("location");
    case "Camera.StartAt":return "Place the camera at "+Arg("location");
    case "Lara.SetOutfit":return "Dress Lara in "+Arg("outfit")+" while keeping her hair";
    case "Lara.SetHair":return "Set Lara's hair to "+Arg("hair");
    case "Lara.ClearHair":return "Remove Lara's hair";
    case "Lara.Outfit.RemoveOneLayer":return "Remove Lara's outermost visible clothing layer";
    case "Lara.Outfit.AddOneLayer":return "Restore Lara's next clothing layer";
    case "Lara.StartIdle":return "Use Lara's configured normal idle state";
    case "Lara.DissolveIn":return "Reveal Lara at "+Arg("location");
    case "Lara.DissolveTo":return "Dissolve Lara to "+Arg("location");
    case "Camera.Track":return "Keep the camera following "+Arg("performer");
    case "Lara.LocomoteTo":return "Lara walks to "+Arg("location");
    case "Lara.PersistentPose":return "Blend Lara into the "+Arg("pose")+" pose";
    case "Director.DrawAndExecute":return "Ask the Director to choose and execute a matching card";
    case "Director.Execute":return "Execute card "+Arg("card");
    case "Lara.PersistentSitPose":return "Blend Lara into seated pose "+Arg("pose")+" while retaining seat ownership";
    case "Lara.Grip":return "Acquire the prepared contact target "+Arg("target");
    case "MotionDriver.PlayFunscript":return "Start motion program "+Arg("program");
    case "MotionDriver.Stop":return "Stop motion playback immediately";
    case "UI.ShowAction":return "Show the in-session action "+Arg("label");
    case "UI.HideAction":return "Remove the in-session action "+Arg("action");
    case "UI.WaitFor":return "Wait for your "+Arg("action")+" action";
    case "Equipment.Activate":return "Activate prepared equipment "+Arg("equipment");
    case "Toy.Vibrate":return "Send authored vibration intensity";
    case "Lara.Say":return "Lara says: "+Arg("text");
    case "Lara.MagicShader":return "Set Lara's visual mode to "+Arg("profile");
    case "Wait":return "Wait "+Arg("seconds")+" seconds";
    case "Lara.SitAt":return "Lara sits at "+Arg("location");
    case "Lara.PlayOneShotGesture":return "Lara performs the "+Arg("gesture")+" gesture";
    case "Popup.Show":return "Open the "+Arg("panel")+" editor";
    case "Popup.ShowNeededEquipment":return "Ask you to prepare "+Arg("items");
    case "Popup.RemoveClothingItem":return "Ask you to remove your "+Arg("item");
    case "Popup.Ready":return "Wait for your Ready confirmation";
    case "World.Clothing.MarkRemoved":return "Mark your "+Arg("item")+" as removed";
    case "World.Clothing.Clear":return "Mark all clothing as removed";
    case "SubmissionScore.Calculate":return "Calculate and save the submission score";
    case "Session.SelectPreparedEquipment":return "Select up to "+Arg("count")+" compatible owned equipment items";
    case "PlayerBody.DissolveIn":return "Reveal the prepared player body";
    case "PlayerBody.DissolveOut":return "Dissolve the player body out";
    default:return "Run "+command.Verb+" with "+string.Join(", ",command.Arguments.Select(p=>p.Key+" "+p.Value));
   }
  }
 }
}