using System;using System.IO;using System.Collections.Generic;
class IntegrationBoundariesFG183 {
 static int checks,failures;static void Check(bool b,string name){checks++;if(!b){failures++;Console.Error.WriteLine("FAIL: "+name);}}
 static bool Rejected(Action action){try{action();return false;}catch(InvalidDataException){return true;}}
 static int Main(string[] args){try{
  foreach(int town in new int[]{4})foreach(int area in new int[]{2,3}){
   foreach(ushort id in new ushort[]{31,32,34,35,36,37})Check(Offline70DungeonSelection.SupportedInArea(town,area,id),"Act4 ordinary entry");
   Check(!Offline70DungeonSelection.SupportedInArea(town,area,33),"33 cannot bypass4083");Check(!Offline70DungeonSelection.SupportedInArea(town,area,1502),"1502 cannot bypass4343");
  }
  Offline70QuestCatalogFG catalog=Offline70QuestCatalogFG.Load(Path.Combine(args[0],"quest-catalog.xml"));Check(catalog.Get(39).Supported&&catalog.Get(40).Supported,"NPC chain retained");Check(Rejected(delegate{catalog.Get(41);}),"Unsupported clear condition unavailable");Check(catalog.Get(453).Selection.Length==0,"FG175 diary selection retained");
  byte[] keys=new byte[334];for(int i=0;i<keys.Length;i++)keys[i]=(byte)i;byte[] frame=Offline70FatigueFG50.EntryFailure(keys);byte[] encoded=new byte[frame.Length-15];Buffer.BlockCopy(frame,15,encoded,0,encoded.Length);byte[] body=Offline70RoleProtocol.BatchCryptFG(encoded,keys,2,false);Check(body[0]==0&&BitConverter.ToUInt32(body,1)==22&&body[5]==0,"Fatigue current error22 and party seat");
  Offline70DungeonFlowFG f=new Offline70DungeonFlowFG();f.Begin(37,0);f.X=f.BossX;f.Y=f.BossY;f.RoomBody();ushort uid=f.ApcSpawnedUidFG176;Check(f.BossApcAliveFG176&&uid==f.ApcUidFG176,"APC own roster UID");Check(Rejected(delegate{f.KillBossApcFG176(uid);}),"APC cannot die before load");f.MarkLoaded();Check(f.KillBossApcFG176(uid),"APC first death");Check(!f.KillBossApcFG176(uid),"APC repeated death no-op");Check(f.ReadyToClear,"APC opens clear handshake");f.Abort();f.Begin(1,0);f.RoomBody();f.MarkLoaded();Check(!f.ApcRoutingFG176(uid),"APC route cannot capture next normal run");f.ValidateKill(100);f.Kill(100);Check(f.IsDead(100),"Next ordinary monster can die");
  Offline70ProgressionStateFG progress=new Offline70ProgressionStateFG();progress.Active.Add(new Offline70QuestActiveFG{Id=4307,Instance=Guid.NewGuid().ToString("N")});Offline70DungeonFlowFG g=new Offline70DungeonFlowFG();g.Begin(11,3);g.ConfigureGsdFG130(progress);byte[] room=g.RoomBody();ushort actual=BitConverter.ToUInt16(room,12+14*(room[11]-1)+6);Check(g.GsdAliveFG130&&actual==900,"GSD retains verified900 UID");g.MarkLoaded();Check(g.DieGsdFG130(900),"GSD death uses own UID");Check(!g.DieGsdFG130(900),"GSD repeated death no-op");Check(!g.GsdAliveFG130,"GSD survival flag clears");
  Console.WriteLine("{\"allPassed\":"+(failures==0?"true":"false")+",\"checks\":"+checks+",\"failures\":"+failures+",\"releaseAllowed\":false}");return failures==0?0:1;
 }catch(Exception e){Console.Error.WriteLine(e);return 1;}}
}
