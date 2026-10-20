using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
namespace DazPose.Story.Language {
 // The lexer and AST belong to this frontend only. Executable/domain data never refer to them.
 internal sealed class StoryToken {
  public string text,kind;
  public int line,column;
  public StoryToken(string kind,string text,int line,int column) { this.kind=kind; this.text=text; this.line=line; this.column=column; }
 }
 internal static class StoryLexer {
  public static List<StoryToken> Read(string text,string file) {
   var output=new List<StoryToken>(); int i=0,line=1,column=1;
   while(i<text.Length) {
    char c=text[i]; int start=i,l=line,col=column;
    if(c=='\r' || c==' ' || c=='\t') { i++; column++; continue; }
    if(c=='\n' || c==';') { output.Add(new StoryToken("nl","\n",line,column)); i++; if(c=='\n') { line++; column=1; } else column++; continue; }
    if(c=='/' && i+1<text.Length && text[i+1]=='/') { while(i<text.Length && text[i]!='\n') { i++; column++; } continue; }
    if(c=='"') {
     i++; column++; var value=new StringBuilder(); bool closed=false;
     while(i<text.Length) {
      c=text[i++]; column++;
      if(c=='"') { closed=true; break; }
      if(c=='\n' || c=='\r') throw new StorySourceException(new StorySource(file,l,col),"Unterminated dialogue string.");
      if(c=='\\') {
       if(i==text.Length) break;
       c=text[i++]; column++;
       switch(c) { case 'n':c='\n';break; case 'r':c='\r';break; case 't':c='\t';break; case '"':case '\\':break; default:throw new StorySourceException(new StorySource(file,line,column-1),"Unsupported string escape."); }
      }
      value.Append(c);
     }
     if(!closed) throw new StorySourceException(new StorySource(file,l,col),"Unterminated dialogue string.");
     output.Add(new StoryToken("string",value.ToString(),l,col)); continue;
    }
    if(char.IsDigit(c)) {
     while(i<text.Length && char.IsDigit(text[i])) { i++; column++; }
     if(i+1<text.Length && text[i]=='.' && char.IsDigit(text[i+1])) { i++; column++; while(i<text.Length && char.IsDigit(text[i])) { i++; column++; } }
     output.Add(new StoryToken("number",text.Substring(start,i-start),l,col)); continue;
    }
    if(char.IsLetter(c) || c=='_') {
     while(i<text.Length && (char.IsLetterOrDigit(text[i]) || text[i]=='_' || text[i]=='.' && i+1<text.Length && char.IsLetter(text[i+1]))) { i++; column++; }
     output.Add(new StoryToken("id",text.Substring(start,i-start),l,col)); continue;
    }
    string pair=i+1<text.Length ? text.Substring(i,2) : "";
    if(new[]{"==","!=","<=",">=","&&","||","+=",".."}.Contains(pair)) { output.Add(new StoryToken("punct",pair,l,col)); i+=2; column+=2; continue; }
    if("{}()[],=<>!+-*/%:".Contains(c.ToString())) { output.Add(new StoryToken("punct",c.ToString(),l,col)); i++; column++; continue; }
    throw new StorySourceException(new StorySource(file,l,col),"Unexpected character '"+c+"'.");
   }
   output.Add(new StoryToken("eof","<end>",line,column)); return output;
  }
 }
 internal sealed class StoryAstExpression {
  public StoryToken token;
  public string op;
  public double number;
  public readonly List<StoryAstExpression> args=new List<StoryAstExpression>();
 }
 internal sealed class StoryAstArgument { public string name; public StoryAstExpression expression; }
 internal sealed class StoryAstNode {
  public string kind,name;
  public StoryToken token;
  public bool awaited,add;
  public StoryToken end;
  public StoryAstExpression expression;
  public readonly List<StoryAstArgument> arguments=new List<StoryAstArgument>();
  public readonly List<StoryAstNode> body=new List<StoryAstNode>(),otherwise=new List<StoryAstNode>();
 }
 internal sealed class StoryAstDeclaration {
  public string kind,name;
  public StoryToken token;
  public readonly List<StoryAstNode> body=new List<StoryAstNode>();
  public readonly List<string> tags=new List<string>();
  public readonly List<StoryAstExpression> requirements=new List<StoryAstExpression>();
  public double progress,weight=1;
  public int maxUses=-1,cooldown;
 }
 internal sealed class StoryParser {
  readonly List<StoryToken> tokens;
  readonly string file;
  int position;
  string declaration;
  StoryToken Current=>tokens[position];
  StoryToken Next=>tokens[Math.Min(position+1,tokens.Count-1)];
  StorySource Source(StoryToken t)=>new StorySource(file,t.line,t.column,declaration);
  StorySourceException Error(string message)=>new StorySourceException(Source(Current),message);
  public StoryParser(string text,string file) { this.file=file; tokens=StoryLexer.Read(text,file); }
  StoryToken Take() { var t=Current; position++; return t; }
  bool Match(string value) { if(Current.text!=value) return false; position++; return true; }
  StoryToken Expect(string value) { if(Current.text!=value) throw Error("Expected '"+value+"', found '"+Current.text+"'."); return Take(); }
  StoryToken Identifier() { if(Current.kind!="id") throw Error("Expected a name."); return Take(); }
  void Lines() { while(Current.kind=="nl") position++; }
  void EndLine() { if(Current.kind!="nl" && Current.kind!="eof" && Current.text!="}") throw Error("Expected the end of this command."); Lines(); }
  public List<StoryAstDeclaration> Parse() {
   var output=new List<StoryAstDeclaration>(); Lines();
   while(Current.kind!="eof") {
    var kind=Take(); if(kind.text!="setup" && kind.text!="card" && kind.text!="sequence") throw new StorySourceException(Source(kind),"Expected setup, card or sequence declaration.");
    var name=Identifier(); declaration=kind.text+" "+name.text;
    var item=new StoryAstDeclaration { kind=kind.text,name=name.text,token=name };
    Lines();var metadata=new HashSet<string>(StringComparer.Ordinal);
    while(item.kind=="card" && Current.text!="{") {
     string key=Take().text;
     if(key!="requires" && !metadata.Add(key))throw Error("Duplicate card metadata '"+key+"'.");
     switch(key) {
      case "tags":
       var tags=Expression();if(tags.op!="list")throw Error("Card tags require a list.");
       foreach(var tag in tags.args) {if(tag.op!="name" && tag.op!="string")throw Error("Card tags are names or quoted tags.");item.tags.Add(tag.token.text);}break;
      case "progress":item.progress=MetadataNumber();break;
      case "weight":item.weight=MetadataNumber();break;
      case "cooldown":item.cooldown=MetadataInteger();break;
      case "repeat":
       if(Match("unlimited"))item.maxUses=-1;else if(Match("once"))item.maxUses=1;else item.maxUses=MetadataInteger();break;
      case "requires":item.requirements.Add(Expression());break;
      default:throw Error("Unknown card metadata '"+key+"'.");
     }
     EndLine();
    }
    item.body.AddRange(Block()); output.Add(item); Lines();
   }
   return output;
  }
  double MetadataNumber() {
   bool negative=Match("-");if(Current.kind!="number")throw Error("Expected a numeric card value.");
   double value=double.Parse(Take().text,CultureInfo.InvariantCulture);return negative ? -value : value;
  }
  int MetadataInteger() {double value=MetadataNumber();if(value!=Math.Floor(value) || value<int.MinValue || value>int.MaxValue)throw Error("Expected a whole card count.");return (int)value;}
  List<StoryAstNode> Block() {
   Lines(); Expect("{"); Lines(); var output=new List<StoryAstNode>();
   while(Current.text!="}") { if(Current.kind=="eof") throw Error("Missing closing '}'."); output.Add(Statement()); Lines(); }
   Expect("}"); return output;
  }
  List<StoryAstNode> Body() { Lines(); return Current.text=="{" ? Block() : new List<StoryAstNode>{Statement()}; }
  StoryAstNode Statement() {var node=StatementCore();node.end=Current;return node;}
  StoryAstNode StatementCore() {
   var start=Current; string word=start.text;
   if(Match("if")) {
    var node=new StoryAstNode {kind="if",token=start,expression=Expression()}; node.body.AddRange(Body()); Lines();
    if(Match("else")) { if(Current.text=="if") node.otherwise.Add(Statement()); else node.otherwise.AddRange(Body()); } return node;
   }
   if(Match("while") || Match("repeat")) {
    var node=new StoryAstNode {kind=word,token=start,expression=Expression()}; node.body.AddRange(Body()); return node;
   }
   if(Match("phase")) { var node=new StoryAstNode {kind="phase",token=start,name=Identifier().text}; node.body.AddRange(Block()); return node; }
   if(Match("play")) { var node=new StoryAstNode {kind="play",token=start,name=Identifier().text}; EndLine(); return node; }
   if(Match("let")) {
    var node=new StoryAstNode {kind="let",token=start,name=Identifier().text}; Expect("=");
    if(Match("await")) {
     Expect("wait"); Expect("any"); node.kind="race"; Lines(); Expect("{"); Lines();
     while(Current.text!="}") {
      node.arguments.Add(new StoryAstArgument { expression=Expression() }); EndLine();
      if(Current.kind=="eof") throw Error("Missing closing '}' for wait any.");
     }
     Expect("}"); return node;
    }
    node.expression=Expression(); EndLine(); return node;
   }
   if(Match("wait")) {
    var node=new StoryAstNode {kind="call",token=start,name="Wait",awaited=true};
    node.arguments.Add(new StoryAstArgument { expression=Expression() }); EndLine(); return node;
   }
   bool awaited=Match("await"); var name=Identifier();
   if(Current.text=="=" || Current.text=="+=") {
    if(awaited) throw Error("An assignment is not awaited.");
    bool add=Take().text=="+="; var node=new StoryAstNode {kind="set",token=start,name=name.text,add=add,expression=Expression()}; EndLine(); return node;
   }
   var call=new StoryAstNode {kind="call",token=start,name=name.text,awaited=awaited};
   Expect("("); Lines();
   if(!Match(")")) {
    do {
     Lines(); string argument=null;
     if(Current.kind=="id" && Next.text==":") { argument=Take().text; Expect(":"); Lines(); }
     call.arguments.Add(new StoryAstArgument {name=argument,expression=Expression(true)}); Lines();
    } while(Match(",")); Expect(")");
   }
   EndLine(); return call;
  }
  static int Precedence(string value) {
   switch(value) { case "||":return 1; case "&&":return 2; case "==":case "!=":return 3;
    case "<":case "<=":case ">":case ">=":return 4; case "..":return 5;
    case "+":case "-":return 6; case "*":case "/":return 7; default:return 0; }
  }
  StoryAstExpression Expression(bool multiline=false,int minimum=1) {
   if(multiline) Lines(); var t=Take(); StoryAstExpression left;
   if(t.text=="!" || t.text=="-") { left=new StoryAstExpression {op=t.text=="!" ? "not" : "negate",token=t}; left.args.Add(Expression(multiline,8)); }
   else if(t.text=="(") { left=Expression(true); Lines(); Expect(")"); }
   else if(t.kind=="number") {
    double value=double.Parse(t.text,CultureInfo.InvariantCulture);
    if(Current.text=="ms") { Take(); value/=1000; } else if(Current.text=="s" || Current.text=="%") Take();
    left=new StoryAstExpression {op="number",token=t,number=value};
   } else if(t.kind=="string") left=new StoryAstExpression {op="string",token=t};
   else if(t.text=="true" || t.text=="false") left=new StoryAstExpression {op="bool",token=t};
   else if(t.text=="[") {
    left=new StoryAstExpression {op="list",token=t}; Lines();
    if(!Match("]")) {
     do { Lines(); left.args.Add(Expression(true)); Lines(); } while(Match(",")); Expect("]");
    }
   } else if(t.kind=="id") {
    if((t.text=="equipment" || t.text=="theme") && Match(":")) {
     string tag=t.text+":"+Identifier().text;while(Match("-"))tag+="-"+Identifier().text;
     t=new StoryToken("id",tag,t.line,t.column);
    }
    left=new StoryAstExpression {op="name",token=t};
    if(Match("(")) {
     left.op="function"; Lines();
     if(!Match(")")) { do { left.args.Add(Expression(true)); Lines(); } while(Match(",")); Expect(")"); }
    }
   } else throw new StorySourceException(Source(t),"Expected a value, found '"+t.text+"'.");
   while(true) {
    if(multiline) Lines(); int precedence=Precedence(Current.text); if(precedence<minimum) break;
    var op=Take(); var right=Expression(multiline,precedence+1); var parent=new StoryAstExpression {op=op.text,token=op}; parent.args.Add(left); parent.args.Add(right); left=parent;
   }
   return left;
  }
 }
}