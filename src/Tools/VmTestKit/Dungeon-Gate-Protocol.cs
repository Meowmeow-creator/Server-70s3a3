using System;using System.IO;
class Offline70Gate {
 internal byte Town=1,Area;internal ushort X,Y;internal byte Direction;internal byte[] Plain;
 internal static Offline70Gate Parse(byte[] encrypted,Offline70Role role,byte[] keys,int currentArea){
  return Parse(encrypted,role,keys,1,currentArea);
 }
 internal static Offline70Gate Parse(byte[] encrypted,Offline70Role role,byte[] keys,int currentTown,int currentArea){
  Offline70TownProtocol.Validate(role,keys);if(encrypted.Length!=16)throw new InvalidDataException("Gate request length");
  byte[] b=Offline70AreaCipher.Crypt(encrypted,keys,10,false);for(int i=11;i<16;i++)if(b[i]!=0)throw new InvalidDataException("Gate padding");
  byte town=b[0],area=b[1],dir=b[6];ushort x=BitConverter.ToUInt16(b,2),y=BitConverter.ToUInt16(b,4),sourceTown=BitConverter.ToUInt16(b,7),sourceArea=BitConverter.ToUInt16(b,9);
  if(sourceTown!=currentTown||sourceArea!=currentArea||dir!=5||!Offline70TownContentFG75.Exists(currentTown,currentArea)||!Offline70TownContentFG75.Exists(town,area)||!Offline70TownContentFG75.Linked(currentTown,currentArea,town,area)||x>8192||y>1024||x==0&&y==0)throw new InvalidDataException("Gate source, destination or portal link");
  bool roomExit=currentArea==1&&area==0&&x==785&&y==158;
  bool roomReturn=currentArea==0&&area==1&&x==474&&y==336;
  // Current PVF: area0 right edge -> dungeon gate area2. Exit position was captured from the native client.
  bool dungeonEntrance=currentArea==0&&area==2&&x==8&&y==247;
  // Reverse gate targets the right edge of the original Elvengard map. Retain the client's validated position.
  bool dungeonReturn=currentArea==2&&area==0&&x>=1500&&x<=1568&&y>=176&&y<=333;
  if(currentTown==1&&town==1&&!roomExit&&!roomReturn&&!dungeonEntrance&&!dungeonReturn)throw new InvalidDataException("Unimplemented Elvengard gate route");
  Offline70Gate result=new Offline70Gate{Town=town,Area=area,X=x,Y=y,Direction=dir,Plain=b};
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
