using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
namespace DazPose.Story {
 // JsonUtility silently truncates deep inline recursive DTOs. Inspect only JSON nesting
 // before deserialization; JsonUtility remains the one field/value serializer.
 internal static class StoryJsonNestingGuard {
  internal const int MaximumNestedBlocks=8;
  readonly struct Container {
   internal readonly char End;
   internal readonly bool Steps;
   internal Container(char end,bool steps) { End=end; Steps=steps; }
  }
  public static void Validate(string json,string story) {
   var stack=new Stack<Container>();
   int stepArrays=0;
   string property=null,lastStep=null,lastOp=null;
   for(int i=0;i<json.Length;i++) {
    char c=json[i];
    if(c=='"') {
     string text=ReadString(json,ref i);
     int next=i+1; while(next<json.Length && char.IsWhiteSpace(json[next])) next++;
     if(next<json.Length && json[next]==':') { property=text; i=next; }
     else {
      if(property=="id") lastStep=text;
      if(property=="op") lastOp=text;
      property=null;
     }
    } else if(c=='[' || c=='{') {
     int content=i+1; while(content<json.Length && char.IsWhiteSpace(json[content])) content++;
     bool steps=c=='[' && (property=="steps" || property=="elseSteps") && content<json.Length && json[content]!=']';
     if(steps && ++stepArrays>MaximumNestedBlocks+1)
      throw new StoryProgramException(story,lastStep,lastOp,"At most 8 nested step blocks are supported by JsonUtility. Flatten this block; deeper input would be truncated.");
     stack.Push(new Container(c=='[' ? ']' : '}',steps)); property=null;
    } else if(c==']' || c=='}') {
     if(stack.Count==0 || stack.Peek().End!=c) throw new StoryProgramException(story,lastStep,lastOp,"Unbalanced JSON containers.");
     if(stack.Pop().Steps) stepArrays--;
     property=null;
    } else if(c==',') property=null;
   }
   if(stack.Count!=0) throw new StoryProgramException(story,lastStep,lastOp,"Unbalanced JSON containers.");
  }
  static string ReadString(string json,ref int position) {
   var text=new StringBuilder();
   for(position++;position<json.Length;position++) {
    char c=json[position];
    if(c=='"') return text.ToString();
    if(c!='\\') { text.Append(c); continue; }
    if(++position>=json.Length) break;
    switch(json[position]) {
     case '"':text.Append('"');break; case '\\':text.Append('\\');break; case '/':text.Append('/');break;
     case 'b':text.Append('\b');break; case 'f':text.Append('\f');break; case 'n':text.Append('\n');break;
     case 'r':text.Append('\r');break; case 't':text.Append('\t');break;
     case 'u':
      if(position+4>=json.Length || !ushort.TryParse(json.Substring(position+1,4),NumberStyles.HexNumber,CultureInfo.InvariantCulture,out var code))
       throw new ArgumentException("Invalid JSON unicode escape.");
      text.Append((char)code); position+=4; break;
     default:throw new ArgumentException("Invalid JSON escape.");
    }
   }
   throw new ArgumentException("Unterminated JSON string.");
  }
 }
}