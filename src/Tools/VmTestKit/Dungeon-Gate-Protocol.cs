using System;using System.IO;
class Offline70Gate {
 internal byte Town=1,Area;internal ushort X,Y;internal byte Direction;internal byte[] Plain;internal bool WorldMapHop;
 // Evidence classification: door = the request matches a portal rectangle in this town's table;
 // worldmap = zero-source town hop; teleport = a hop with no matching door, which rednf admits
 // (its param==2 channel and the non-matching-door hack counter). See the Parse comment.
 internal string Route="door";internal string Tail="";
 internal static Offline70Gate Parse(byte[] encrypted,Offline70Role role,byte[] keys,int currentArea){
  return Parse(encrypted,role,keys,1,currentArea);
 }
 // Route kind recorded for the evidence log: the client only carries a door source when it walked
 // through a portal rectangle. The world map / airship request sends town+area with a zero source,
 // and rednf validates that one with GameWorld::check_valid_area + CUser::CheckMoveTown only
 // (move_area.inc:110-143 logs a non-matching door as a hack counter, it never rejects the move),
 // so requiring Linked() there rejected legitimate town hops. The village level/quest prerequisites
 // CheckMoveTown reads (Village::m_field2c / m_questIdx) are not imported yet, so a hop is admitted
 // on destination validity alone and recorded as route=worldmap.
 internal static Offline70Gate Parse(byte[] encrypted,Offline70Role role,byte[] keys,int currentTown,int currentArea){
  Offline70TownProtocol.Validate(role,keys);if(encrypted.Length!=16)throw new InvalidDataException("Gate request length");
  byte[] b=Offline70AreaCipher.Crypt(encrypted,keys,10,false);
  // The five trailing bytes are zero for a walked door but carry the caller's own markers for other
  // channels (rednf's move_area signature takes z, flag and param after x/y; param==2 is the teleport
  // channel). Rejecting them killed every NPC area teleport, so they are recorded instead.
  string tail="";for(int i=11;i<16;i++)if(b[i]!=0)tail+=i.ToString()+"="+b[i].ToString()+" ";
  byte town=b[0],area=b[1],dir=b[6];ushort x=BitConverter.ToUInt16(b,2),y=BitConverter.ToUInt16(b,4),sourceTown=BitConverter.ToUInt16(b,7),sourceArea=BitConverter.ToUInt16(b,9);
  // TownSupported is the single town authority (Town-Content-FG75.cs:10): a hop into a town whose
  // areas are generated but which the service does not open would otherwise pass Exists() here and
  // then be rejected by Economy.SaveTown, so the refusal belongs in one place with one reason.
  if(!Offline70TownContentFG75.TownSupported(currentTown)||!Offline70TownContentFG75.TownSupported(town)||!Offline70TownContentFG75.Exists(currentTown,currentArea)||!Offline70TownContentFG75.Exists(town,area))throw new InvalidDataException("Gate source or destination area");
  if(x>8192||y>1024||x==0&&y==0)throw new InvalidDataException("Gate coordinates");
  // rednf GameWorld::move_area.inc:110-143 validates a move with check_valid_area on both ends plus
  // CUser::CheckMoveTown, and a door rectangle that does not match is only logged as a hack counter -
  // it never rejects the move. Requiring Linked() here made the NPC teleports unusable (马琳 is npc 56
  // in 6/0, 杰西卡 npc 20 in 2/0, 素喃's 诺羽 hops 6/9/12), because those requests carry no walked-door
  // source at all. A hop is therefore admitted on destination validity and classified for the log.
  bool zeroSource=sourceTown==0&&sourceArea==0;
  bool worldMap=zeroSource&&town!=currentTown;
  bool door=dir==5&&sourceTown==currentTown&&sourceArea==currentArea&&Offline70TownContentFG75.Linked(currentTown,currentArea,town,area);
  string route=door?"door":worldMap?"worldmap":"teleport";
  bool roomExit=currentArea==1&&area==0&&x==785&&y==158;
  bool roomReturn=currentArea==0&&area==1&&x==474&&y==336;
  // Current PVF: area0 right edge -> dungeon gate area2. Exit position was captured from the native client.
  bool dungeonEntrance=currentArea==0&&area==2&&x==8&&y==247;
  // Reverse gate targets the right edge of the original Elvengard map. Retain the client's validated position.
  bool dungeonReturn=currentArea==2&&area==0&&x>=1500&&x<=1568&&y>=176&&y<=333;
  bool campEntry=currentArea==0&&area==3;bool campReturn=currentArea==3&&area==0;
  // Elvengard (town 1) corridors were pinned from native captures; keep demanding a known one only for
  // requests that claim to be a walked door, so an NPC teleport inside town 1 is not rejected either.
  if(door&&currentTown==1&&town==1&&!roomExit&&!roomReturn&&!dungeonEntrance&&!dungeonReturn&&!campEntry&&!campReturn)throw new InvalidDataException("Unimplemented Elvengard gate route");
  Offline70Gate result=new Offline70Gate{Town=town,Area=area,X=x,Y=y,Direction=dir,Plain=b,WorldMapHop=route=="worldmap",Route=route,Tail=tail};
  // New town links may spawn directly inside the reverse portal. Move the
  // actor just into the destination interior to avoid bouncing back at once.
  if(currentTown!=1||town!=1)result.OffsetReturn(currentTown,currentArea);
  return result;
 }
 void OffsetReturn(int fromTown,int fromArea){int[][] portals=Offline70TownContentFG75.Portals(Town,Area);foreach(int[] p in portals){if(p[4]!=fromTown||p[5]!=fromArea||X<p[0]||X>=p[0]+p[2]||Y<p[1]||Y>=p[1]+p[3])continue;int cx=p[0]+p[2]/2,cy=p[1]+p[3]/2,maxX=Int32.MinValue,maxY=Int32.MinValue;bool other=false;foreach(int[] q in portals){if(q==p)continue;other=true;maxX=Math.Max(maxX,q[0]+q[2]/2);maxY=Math.Max(maxY,q[1]+q[3]/2);}int x=cx,y=cy;if(p[2]>p[3])y=other&&cy>=maxY?p[1]-16:p[1]+p[3]+16;else x=other&&cx>=maxX?p[0]-16:p[0]+p[2]+16;if(x<0||x>8192||y<0||y>1024)throw new InvalidDataException("Portal interior bounds");X=(ushort)x;Y=(ushort)y;return;}}
 internal byte[] Body(ushort op){
  if(op==38)return new byte[]{1};
  using(MemoryStream m=new MemoryStream())using(BinaryWriter w=new BinaryWriter(m)){
   if(op==23){w.Write((ushort)1);w.Write(Town);w.Write(Area);}else if(op==24){w.Write(Town);w.Write(Area);w.Write((ushort)1);w.Write((ushort)1);}else throw new InvalidDataException("Gate reply opcode");
   w.Write(X);w.Write(Y);w.Write(Direction);w.Write((byte)3);return m.ToArray();
  }
 }
 internal byte[] Reply(ushort op,byte[] keys){byte[] body=Body(op),padded=new byte[(body.Length+7)&~7];body.CopyTo(padded,0);return Offline70RoleProtocol.Packet((byte)(op==38?1:0),op,Offline70AreaCipher.Crypt(padded,keys,op==23?9:10,true));}
 internal static byte[] Reject(byte[] keys){return Offline70RoleProtocol.Packet(1,38,Offline70AreaCipher.Crypt(new byte[]{0,21,1,0,0,0,0,0},keys,10,true));}
}
