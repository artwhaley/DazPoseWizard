namespace DazPose.Editor.Studio {
 // Single pending visual request. Source lookup/inspector selection happens before this queue.
 public sealed class AuthoringSelectionQueue {
  public readonly struct Request {
   public readonly AuthoringCueState Cue;public readonly bool After;public readonly int Generation;
   public Request(AuthoringCueState cue,bool after,int generation){Cue=cue;After=after;Generation=generation;}
  }
  Request desired;
  public bool Pending {get;private set;}
  public int Generation {get;private set;}
  public int Discarded {get;private set;}
  public void Select(AuthoringCueState cue,bool after){if(Pending)Discarded++;Generation++;desired=new Request(cue,after,Generation);Pending=true;}
  public void Cancel(){if(Pending)Discarded++;Generation++;Pending=false;}
  public bool TryTake(out Request value){value=desired;if(!Pending)return false;Pending=false;return value.Generation==Generation;}
 }
}
