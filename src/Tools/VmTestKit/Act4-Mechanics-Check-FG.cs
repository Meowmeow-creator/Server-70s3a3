using System;using System.IO;using System.Reflection;using System.Collections.Generic;
// Static check driver for Act4 room mechanics (condition monsters, event monsters, seal doors).
// Verifies that the generated tables match the expected PVF-derived data.
// Usage: Act4-Mechanics-Check-FG.exe <path-to-DNF70-Channel-FG.exe>
class Act4MechanicsCheckFG {
 static int failures=0;
 static void Check(bool cond,string msg){
  if(!cond){Console.WriteLine("FAIL: "+msg);failures++;}else{Console.WriteLine("OK:   "+msg);}
 }
 static int Main(string[] args){
  if(args.Length!=1){Console.Error.WriteLine("Usage: Act4-Mechanics-Check-FG.exe <path-to-exe>");return 2;}
  Assembly a=Assembly.LoadFile(Path.GetFullPath(args[0]));
  // Condition monsters
  Type condType=a.GetType("ConditionMonstersFG");
  if(condType==null){Console.WriteLine("FAIL: ConditionMonstersFG type not found");return 1;}
  // Check dungeon 35 has condition monsters
  object condTable=condType.GetField("Table",BindingFlags.NonPublic|BindingFlags.Public|BindingFlags.Static).GetValue(null);
  var condDict=(IDictionary<ushort,List<ushort[]>>)condTable;
  Check(condDict.ContainsKey(35),"ConditionMonsters: dungeon 35 has entry");
  Check(condDict.ContainsKey(37),"ConditionMonsters: dungeon 37 has entry");
  if(condDict.ContainsKey(35)){
   var list=condDict[35];
   Check(list.Count>0,"ConditionMonsters: dungeon 35 has "+list.Count+" entries");
   // Check first entry: map 5402, monster 50073, level 44 (basis 22 + 2), kind 0
   bool found=false;
   foreach(var e in list){
    if(e[0]==5402&&e[1]==50073){found=true;break;}
   }
   Check(found,"ConditionMonsters: dungeon 35 map 5402 monster 50073 found");
  }
  if(condDict.ContainsKey(37)){
   var list=condDict[37];
   Check(list.Count>0,"ConditionMonsters: dungeon 37 has "+list.Count+" entries");
   bool found=false;
   foreach(var e in list){
    if(e[1]==50097){found=true;break;}
   }
   Check(found,"ConditionMonsters: dungeon 37 monster 50097 found");
  }
  // Event monsters
  Type eventType=a.GetType("EventMonstersFG");
  if(eventType==null){Console.WriteLine("FAIL: EventMonstersFG type not found");return 1;}
  object eventTable=eventType.GetField("EventTable",BindingFlags.NonPublic|BindingFlags.Public|BindingFlags.Static).GetValue(null);
  var eventDict=(IDictionary<ushort,ushort[][]>)eventTable;
  // Dungeons 31,32,34,35,36,37 have event monsters (33 does not)
  foreach(ushort d in new ushort[]{31,32,34,35,36,37}){
   Check(eventDict.ContainsKey(d),"EventMonsters: dungeon "+d+" has entry");
  }
  Check(!eventDict.ContainsKey(33),"EventMonsters: dungeon 33 has NO event monster entry");
  if(eventDict.ContainsKey(31)){
   var entries=eventDict[31];
   Check(entries.Length>0&&entries[0][1]==9000,"EventMonsters: dungeon 31 monsterIdx=9000");
  }
  // Event monsters3
  object event3Table=eventType.GetField("EventTable3",BindingFlags.NonPublic|BindingFlags.Public|BindingFlags.Static).GetValue(null);
  var event3Dict=(IDictionary<ushort,ushort[][]>)event3Table;
  // All 7 dungeons have event monsters3
  foreach(ushort d in new ushort[]{31,32,33,34,35,36,37}){
   Check(event3Dict.ContainsKey(d),"EventMonsters3: dungeon "+d+" has entry");
  }
  // Seal doors
  Type sealType=a.GetType("SealDoorsFG");
  if(sealType==null){Console.WriteLine("FAIL: SealDoorsFG type not found");return 1;}
  object sealTable=sealType.GetField("Table",BindingFlags.NonPublic|BindingFlags.Public|BindingFlags.Static).GetValue(null);
  var sealDict=(IDictionary<ushort,ushort[]>)sealTable;
  Check(sealDict.ContainsKey(33),"SealDoors: dungeon 33 has entry");
  Check(sealDict.ContainsKey(36),"SealDoors: dungeon 36 has entry");
  Check(!sealDict.ContainsKey(31),"SealDoors: dungeon 31 has NO seal door");
  if(sealDict.ContainsKey(33)){
   var entry=sealDict[33];
   Check(entry[0]==60026,"SealDoors: dungeon 33 map=60026");
   Check(entry[1]==1&&entry[2]==0,"SealDoors: dungeon 33 pos=(1,0)");
   Check(entry[3]==10,"SealDoors: dungeon 33 rate=10");
  }
  if(sealDict.ContainsKey(36)){
   var entry=sealDict[36];
   Check(entry[0]==60020,"SealDoors: dungeon 36 map=60020");
   Check(entry[1]==3&&entry[2]==2,"SealDoors: dungeon 36 pos=(3,2)");
   Check(entry[3]==1,"SealDoors: dungeon 36 rate=1");
  }
  // Boss room entrance conditions (PVF [boss room entrance condition]) and the runtime gate.
  var huntDict=(IDictionary<ushort,ushort[][]>)condType.GetField("HuntConditions",BindingFlags.NonPublic|BindingFlags.Public|BindingFlags.Static).GetValue(null);
  Check(huntDict!=null&&huntDict.ContainsKey(35)&&huntDict[35].Length==4,"HuntConditions: dungeon 35 hunts 4 monsters");
  Check(huntDict!=null&&huntDict.ContainsKey(37)&&huntDict[37].Length==1&&huntDict[37][0][0]==50097,"HuntConditions: dungeon 37 hunts the seal stone 50097");
  Check(!huntDict.ContainsKey(31),"HuntConditions: dungeon 31 has no boss room condition");
  MethodInfo declares=condType.GetMethod("DeclaresMonster",BindingFlags.NonPublic|BindingFlags.Public|BindingFlags.Static);
  Check(declares!=null&&(bool)declares.Invoke(null,new object[]{(ushort)37,(ushort)7015,(ushort)50097}),"HuntConditions: room 7015 declares the seal stone");
  Check(declares!=null&&!(bool)declares.Invoke(null,new object[]{(ushort)37,(ushort)7027,(ushort)50097}),"HuntConditions: boss room 7027 declares no condition monster");
  Type flowType=a.GetType("Offline70DungeonFlowFG");
  if(flowType==null){Console.WriteLine("FAIL: Offline70DungeonFlowFG type not found");return 1;}
  const BindingFlags any=BindingFlags.NonPublic|BindingFlags.Public|BindingFlags.Instance;
  PropertyInfo unlockedProp=flowType.GetProperty("BossRoomUnlockedFG181",any);
  PropertyInfo bossXProp=flowType.GetProperty("BossX",any),bossYProp=flowType.GetProperty("BossY",any);
  PropertyInfo countProp=flowType.GetProperty("CurrentCount",any);
  MethodInfo gateM=flowType.GetMethod("BossConditionGateFG181",any);
  MethodInfo beginM=flowType.GetMethod("Begin",any,null,new Type[]{typeof(ushort),typeof(byte)},null);
  MethodInfo loadedM=flowType.GetMethod("MarkLoaded",any);
  MethodInfo condM=flowType.GetMethod("CurrentConditionMonsters",any);
  MethodInfo killM=flowType.GetMethod("Kill",any);
  FieldInfo xF=flowType.GetField("X",BindingFlags.NonPublic|BindingFlags.Instance);
  FieldInfo yF=flowType.GetField("Y",BindingFlags.NonPublic|BindingFlags.Instance);
  FieldInfo stF=flowType.GetField("conditionStatusFG181",BindingFlags.NonPublic|BindingFlags.Instance);
  bool members=unlockedProp!=null&&bossXProp!=null&&bossYProp!=null&&countProp!=null&&gateM!=null&&beginM!=null&&loadedM!=null&&condM!=null&&killM!=null&&xF!=null&&yF!=null&&stF!=null;
  Check(members,"Gate: runtime members resolvable (unlocked="+(unlockedProp!=null)+" gate="+(gateM!=null)+" begin="+(beginM!=null)+" load="+(loadedM!=null)+" cond="+(condM!=null)+" kill="+(killM!=null)+" count="+(countProp!=null)+" x="+(xF!=null)+" y="+(yF!=null)+" statuses="+(stF!=null)+")");
  if(!members){Console.WriteLine("---");Console.WriteLine(failures==0?"ALL CHECKS PASSED":failures+" CHECKS FAILED");return 1;}
  object flow=Activator.CreateInstance(flowType,true);
  beginM.Invoke(flow,new object[]{(ushort)37,(byte)0});
  byte bx=(byte)bossXProp.GetValue(flow,null),by=(byte)bossYProp.GetValue(flow,null);
  bool unlocked=(bool)unlockedProp.GetValue(flow,null);
  bool gate=(bool)gateM.Invoke(flow,new object[]{bx,by});
  Check(!unlocked&&gate,"Gate: a fresh 37 run keeps the boss cell locked");
  int cell=-1;ushort stone=0;
  System.Collections.IEnumerable statuses=(System.Collections.IEnumerable)stF.GetValue(flow);
  foreach(object st in statuses){
   Type stType=st.GetType();
   stone=(ushort)stType.GetField("MonsterId",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(st);
   foreach(int c in (System.Collections.IEnumerable)stType.GetField("Cells",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(st))cell=c;
   break;
  }
  Type forestType=a.GetType("Offline70ForestFG75");
  int width=(byte)forestType.GetMethod("Width",BindingFlags.NonPublic|BindingFlags.Public|BindingFlags.Static).Invoke(null,new object[]{(ushort)37});
  Check(cell>=0,"Gate: the seal stone was rolled into a candidate room (cell="+cell+")");
  if(cell>=0){
   xF.SetValue(flow,(byte)(cell%width));
   yF.SetValue(flow,(byte)(cell/width));
   loadedM.Invoke(flow,null);
   var rows=(List<ushort[]>)condM.Invoke(flow,null);
   int index=-1;for(int i=0;i<rows.Count;i++)if(rows[i][1]==stone)index=i;
   Check(index>=0,"Gate: the rolled room spawns the hunted monster (rows="+rows.Count+")");
   if(index>=0){
    int currentCount=(int)countProp.GetValue(flow,null);
    killM.Invoke(flow,new object[]{(ushort)(100+currentCount+index)});
    bool unlocked2=(bool)unlockedProp.GetValue(flow,null);
    bool gate2=(bool)gateM.Invoke(flow,new object[]{bx,by});
    Check(unlocked2&&!gate2,"Gate: killing the hunted monster unlocks the boss cell");
   }
  }
  // Seal door routing: with hell-party mode absent the door stays closed and the cell keeps
  // its own [map specification] room (33's (1,0) is 4805, not the hell map 60026).
  object flow33=Activator.CreateInstance(flowType,true);
  beginM.Invoke(flow33,new object[]{(ushort)33,(byte)0});
  bool doorOpen=(bool)flowType.GetProperty("SealDoorOpenFG181",any).GetValue(flow33,null);
  xF.SetValue(flow33,(byte)1);yF.SetValue(flow33,(byte)0);
  ushort cellMap=(ushort)flowType.GetProperty("CurrentMap",any).GetValue(flow33,null);
  Type forestType33=a.GetType("Offline70ForestFG75");
  ushort wantMap=(ushort)forestType33.GetMethod("Map",BindingFlags.NonPublic|BindingFlags.Public|BindingFlags.Static).Invoke(null,new object[]{(ushort)33,(byte)1,(byte)0});
  Check(!doorOpen,"SealDoor: hell-party mode absent -> the door stays closed");
  Check(cellMap==wantMap&&cellMap!=60026,"SealDoor: 33 (1,0) keeps its own room map="+cellMap+" (hell map 60026 not served)");
  Console.WriteLine("---");
  Console.WriteLine(failures==0?"ALL CHECKS PASSED":failures+" CHECKS FAILED");
  return failures==0?0:1;
 }
}
