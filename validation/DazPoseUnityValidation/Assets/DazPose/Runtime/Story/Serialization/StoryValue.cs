using System;
namespace DazPose.Story {
 public readonly struct StoryValue {
  public readonly string Type, Text;
  public readonly float Number;
  public readonly bool Flag;
  public StoryValue(string text) { Type="string"; Text=text ?? ""; Number=0; Flag=false; }
  public StoryValue(float number) { if(!float.IsFinite(number)) throw new ArgumentException("Number must be finite."); Type="number"; Number=number; Text=null; Flag=false; }
  public StoryValue(bool flag) { Type="bool"; Flag=flag; Text=null; Number=0; }
  public static void ValidateType(string type) { if(type!="string" && type!="number" && type!="bool") throw new ArgumentException("valueType must be string, number, or bool."); }
 }
}
