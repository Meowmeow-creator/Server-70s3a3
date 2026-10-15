using System;using System.Collections.Generic;using System.IO;using System.Text;using System.Text.RegularExpressions;
// Acceptance check: every door row the CN70 client can trigger must pass the
// server's op38 validation chain (Exists + Linked + town1 whitelist + bounds).
// Door data comes from the PVF export (town/*.twn + map/*.map [town movable area]).
class TownDoorsCheck {
 static readonly Regex AreaBlock=new Regex(@"\[area\]\s*(?:\r?\n)\s*(\d+)\s*(?:\r?\n)\s*`([^`]+)`");
 static readonly Regex MapLine=new Regex(@"^[ \t]*(-?\d+)[ \t]+(-?\d+)[ \t]+(-?\d+)[ \t]+(-?\d+)[ \t]+(-?\d+)[ \t]+(-?\d+)[ \t]*$",RegexOptions.Multiline);
 static readonly Regex NameLine=new Regex(@"(\d+)\s+`([^`]+)`");
 static int Main(string[] args){
  if(args.Length<1){Console.Error.WriteLine("usage: TownDoorsCheck <pvf-export-dir> [quest-catalog.xml]");return 2;}
  string root=Path.GetFullPath(args[0]);
  string townDir=Path.Combine(root,"town"),mapDir=Path.Combine(root,"map");
  if(!Directory.Exists(townDir)||!Directory.Exists(mapDir)){Console.Error.WriteLine("export dir must contain town/ and map/");return 2;}
  // town list (id -> name)
  Dictionary<int,string> townFile=new Dictionary<int,string>();
  foreach(Match m in NameLine.Matches(File.ReadAllText(Path.Combine(townDir,"town.lst.txt"),Encoding.UTF8)))
   townFile[int.Parse(m.Groups[1].Value)]=m.Groups[2].Value.ToLower().Replace(".twn","");
  // areas per town and doors per (town,area)
  Dictionary<int,List<int>> areas=new Dictionary<int,List<int>>();
  Dictionary<string,List<int[]>> doors=new Dictionary<string,List<int[]>>();
  foreach(KeyValuePair<int,string> kv in townFile){
   string twn=Path.Combine(townDir,kv.Value+".twn.txt");
   if(!File.Exists(twn))continue;
   string text=File.ReadAllText(twn,Encoding.UTF8);
   List<int> areaIds=new List<int>();
   foreach(Match m in AreaBlock.Matches(text)){
    int aid=int.Parse(m.Groups[1].Value);string mapPath=m.Groups[2].Value.ToLower();
    areaIds.Add(aid);
    string mf=Path.Combine(mapDir,"map_"+mapPath.Replace('/','_')+".txt");
    List<int[]> rows=new List<int[]>();
    if(File.Exists(mf)){
     string mt=File.ReadAllText(mf,Encoding.UTF8);
     int i=mt.IndexOf("[town movable area]");
     if(i>=0){int j=mt.IndexOf("[/town movable area]",i);string block=mt.Substring(i,j<0?mt.Length-i:j-i);
      foreach(string line in block.Split('\n')){
       string s2=line.Trim();if(s2.Length==0)continue;
       string[] parts=s2.Split(new char[]{'\t',' '},StringSplitOptions.RemoveEmptyEntries);
       if(parts.Length<6)continue;
       int[] v=new int[6];bool okRow=true;
       for(int k=0;k<6;k++)if(!int.TryParse(parts[k],out v[k])){okRow=false;break;}
       if(okRow&&v[4]>=1&&v[5]>=0)rows.Add(v);}}
    }
    doors[kv.Key+"/"+aid]=rows;
   }
   areas[kv.Key]=areaIds;
  }
  int total=0,accepted=0,rejectedUnsupported=0,unexpected=0;
  List<string> failures=new List<string>();
  foreach(KeyValuePair<string,List<int[]>> kv in doors){
   string[] key=kv.Key.Split('/');int town=int.Parse(key[0]),area=int.Parse(key[1]);
   foreach(int[] row in kv.Value){
    int targetTown=row[4],targetArea=row[5];total++;
    if(town<1||town>4||targetTown<1||targetTown>4){rejectedUnsupported++;continue;}   // town outside the server's supported set: expected reject
    // request position = center of the destination's reverse portal (client behaviour)
    int x=row[0]+row[2]/2,y=row[1]+row[3]/2;
    List<int[]> destRows;int[] reverse=null;
    if(doors.TryGetValue(targetTown+"/"+targetArea,out destRows))
     foreach(int[] q in destRows)if(q[4]==town&&q[5]==area){reverse=q;break;}
    if(reverse!=null){x=reverse[0]+reverse[2]/2;y=reverse[1]+reverse[3]/2;}
    bool ok=true;string why="";
    if(!Offline70TownContentFG75.Exists(town,area)){ok=false;why="source not Exists";}
    else if(!Offline70TownContentFG75.Exists(targetTown,targetArea)){ok=false;why="target not Exists";}
    else if(!Offline70TownContentFG75.Linked(town,area,targetTown,targetArea)){ok=false;why="not Linked";}
    else if(x>8192||y>1024||(x==0&&y==0)){ok=false;why="request position bounds";}
    else if(town==1&&targetTown==1){
     bool roomExit=area==1&&targetArea==0&&x==785&&y==158;
     bool roomReturn=area==0&&targetArea==1&&x==474&&y==336;
     bool dungeonEntrance=area==0&&targetArea==2&&x==8&&y==247;
     bool dungeonReturn=area==2&&targetArea==0&&x>=1500&&x<=1568&&y>=176&&y<=333;
     bool campEntry=area==0&&targetArea==3;bool campReturn=area==3&&targetArea==0;
     if(!roomExit&&!roomReturn&&!dungeonEntrance&&!dungeonReturn&&!campEntry&&!campReturn){ok=false;why="town1 whitelist";}
    }
    if(ok)accepted++;else{unexpected++;failures.Add(String.Format("town{0}/area{1} -> town{2}/area{3} row[{4},{5},{6},{7}] request=({8},{9}) : {10}",
     town,area,targetTown,targetArea,row[0],row[1],row[2],row[3],x,y,why));}
   }
  }
  foreach(KeyValuePair<int,List<int>> kv in areas)
   foreach(int a in kv.Value)
    if(kv.Key>=1&&kv.Key<=4&&!Offline70TownContentFG75.Exists(kv.Key,a))
     failures.Add("missing Exists for town"+kv.Key+"/area"+a);
  Console.WriteLine("town files: "+townFile.Count+"  door rows: "+total);
  Console.WriteLine("accepted: "+accepted+"  expected-reject (unsupported town): "+rejectedUnsupported+"  unexpected rejects: "+unexpected);
  int[] perTown=new int[5];int[] perTownOk=new int[5];
  foreach(KeyValuePair<string,List<int[]>> kv in doors){string[] key=kv.Key.Split('/');int t=int.Parse(key[0]);
   foreach(int[] row in kv.Value){if(t<1||t>4)continue;perTown[t]++;if(row[4]>=1&&row[4]<=4)perTownOk[t]++;}}
  for(int t=1;t<=4;t++)Console.WriteLine("  town "+t+": "+perTown[t]+" door rows ("+perTownOk[t]+" within supported towns)");
  foreach(string f in failures)Console.WriteLine("  FAIL "+f);
  // report reverse-portal coverage for accepted cross links (OffsetReturn operand)
  int noReverse=0;
  foreach(KeyValuePair<string,List<int[]>> kv in doors){
   string[] key=kv.Key.Split('/');int town=int.Parse(key[0]);
   foreach(int[] row in kv.Value){
    if(row[4]<1||row[4]>4)continue;
    List<int[]> destRows;bool found=false;
    if(doors.TryGetValue(row[4]+"/"+row[5],out destRows))
     foreach(int[] q in destRows)if(q[4]==town&&q[5]==int.Parse(key[1])){found=true;break;}
    if(!found){noReverse++;Console.WriteLine("  note: no reverse portal for town"+town+"/area"+key[1]+" -> town"+row[4]+"/area"+row[5]);}
   }
  }
  Console.WriteLine("links without reverse portal (OffsetReturn will not reposition): "+noReverse);
  // ---- NPC coverage for [meet npc] quests + dungeon portals (PVF vs server table) ----
  int meetOk=0, meetMiss=0;
  if(args.Length>1){
   string cat=File.ReadAllText(args[1],Encoding.UTF8);
   foreach(Match q in Regex.Matches(cat,@"<Offline70QuestDefinitionFG>(.*?)</Offline70QuestDefinitionFG>",RegexOptions.Singleline)){
    string blk=q.Groups[1].Value;
    Match ty=Regex.Match(blk,@"<Type>(.*?)</Type>",RegexOptions.Singleline);
    if(!ty.Success||!ty.Value.Contains("meet npc"))continue;
    Match cond=Regex.Match(blk,@"<int>(\d+)</int>");
    Match nm=Regex.Match(blk,@"<Name>(.*?)</Name>",RegexOptions.Singleline);
    if(!cond.Success)continue;
    int npc=int.Parse(cond.Groups[1].Value);bool found=false;
    for(int t=1;t<=4&&!found;t++)for(int a=0;a<16&&!found;a++)
     if(Offline70TownContentFG75.Exists(t,a)&&Offline70TownContentFG75.Npc(t,a,npc))found=true;
    if(found)meetOk++;else{meetMiss++;if(meetMiss<=12)Console.WriteLine("  meet-npc quest NPC "+npc+" not in any supported area: "+(nm.Success?nm.Groups[1].Value.Trim():""));}
   }
   Console.WriteLine("meet-npc quests: reachable="+meetOk+"  unreachable(in unsupported towns)="+meetMiss);
  }
  bool n1=Offline70TownContentFG75.Npc(4,1,2), n2=Offline70TownContentFG75.Npc(4,4,62);
  Console.WriteLine("Alfhlyra Seria room Npc(4,1,2)="+n1+"   D_Alfhlyra3 Npc(4,4,62)="+n2);
  int portals=0;
  for(int t=1;t<=4;t++)for(int a=0;a<16;a++)if(Offline70TownContentFG75.Exists(t,a)&&Offline70TownContentFG75.DungeonPortal(t,a)!=null)portals++;
  Console.WriteLine("dungeon portals registered for supported towns: "+portals);
  if(!(n1&&n2))failures.Add("NPC whitelist regression");
  bool pass=unexpected==0&&failures.Count==0;
  Console.WriteLine("PASS="+pass);
  return pass?0:1;
 }
}
