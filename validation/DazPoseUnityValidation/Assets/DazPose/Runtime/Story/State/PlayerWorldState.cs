using System;
using System.Collections.Generic;
namespace DazPose.Story {
 public enum PlayerPosture { Unknown, Standing, Sitting, Lying, Kneeling }
 public enum PlayerHands { Free, Occupied, Restricted }
 public enum PlayerAnkles { Free, Restricted }
 public enum PlayerOccupancy { Free, Occupied }
 [Serializable] public sealed class PlayerWorldState {
  public PlayerPosture posture=PlayerPosture.Unknown;
  public PlayerHands hands;
  public PlayerAnkles ankles;
  public PlayerOccupancy oral,anal,genital;
  public bool shoes,socks,shirt,pants,bra,underwear;
  public static readonly string[] ClothingSlots={"shoes","socks","shirt","pants","bra","underwear"};
  public int ClothingCount { get { int count=0; foreach(var slot in ClothingSlots) if(GetClothing(slot)) count++; return count; } }
  public string ClothingDescription { get { var worn=new List<string>(); foreach(var slot in ClothingSlots) if(GetClothing(slot)) worn.Add(slot); return string.Join(", ",worn); } }
  public bool GetClothing(string slot) {
   switch(slot) {
    case "shoes":return shoes; case "socks":return socks; case "shirt":return shirt;
    case "pants":return pants; case "bra":return bra; case "underwear":return underwear;
    default:throw new ArgumentException("Unknown clothing slot: "+slot);
   }
  }
  public void SetClothing(string slot,bool value) {
   switch(slot) {
    case "shoes":shoes=value;break; case "socks":socks=value;break; case "shirt":shirt=value;break;
    case "pants":pants=value;break; case "bra":bra=value;break; case "underwear":underwear=value;break;
    default:throw new ArgumentException("Unknown clothing slot: "+slot);
   }
  }
 }
}