using System;using System.IO;using System.Collections.Generic;
// FG410: 罗杰（npc 29，shop 20 itemproduct1.shp）的生产/制作。
// 客户端 command 27（rednf PacketDispatcher: m_dispatchers[27] = DisPatcher_Compound）请求体
// 明文 16 字节（尾部 0 填充）：int 配方itemId / byte type / ushort 数量；type==1 表示罗杰窗口的
// “按配方 id 直接生产”。材料与产物按数量倍乘，材料里的 item 0 是金币。
// 回包同样是 27，body 按 rednf _ProcCompoundCore 的 cubeMode 分支：
//   byte 删除行数{ byte 空间, short 槽, int 数量 }* ; byte 插入行数{ byte 0, short 槽, int 物品,
//   int 数量, byte 强化, short 耐久, byte 能力类型, short 能力值, byte 标记,
//   byte 随机选项数(=0), byte 强化分离, byte 交易限制 }* 
// 删除行的空间沿用原生 INVEN_TYPE_INVEN=3（主背包）；金币行是原生约定的 (0,0,金额)。
partial class Offline70BatchServiceFG {
 // 与 buy/sell 同一套重发语义：同 sequence 同指纹直接回旧包，指纹不同视为序列复用。
 readonly Dictionary<ushort,string> productionRequestsFG410=new Dictionary<ushort,string>();
 readonly Dictionary<ushort,byte[]> productionRepliesFG410=new Dictionary<ushort,byte[]>();

 void ProductionFG410(byte[] encrypted,ushort sequence){
  string fingerprint=Convert.ToBase64String(encrypted),prior;
  if(productionRequestsFG410.TryGetValue(sequence,out prior)){
   if(prior!=fingerprint)throw new InvalidDataException("Production sequence reused");
   Send("production-repeat27",productionRepliesFG410[sequence]);return;
  }
  byte[] reply;bool committed=false;Dictionary<ushort,uint> collection=null;
  try{
   if(dungeon.Active||town!=Offline70ProductionFG410.TownId)throw new InvalidDataException("Production requires Roger in the current town area");
   byte[] plain=Offline70RoleProtocol.BatchCryptFG(encrypted,keys,27%14,false);
   if(plain.Length!=16)throw new InvalidDataException("Production request length");
   uint rawId=BitConverter.ToUInt32(plain,0);byte type=plain[4];ushort count=BitConverter.ToUInt16(plain,5);
   for(int i=6;i<plain.Length;i++)if(plain[i]!=0)throw new InvalidDataException("Production request padding");
   if(type!=1)throw new InvalidDataException("Production by-recipe path only");
   if(rawId==0||rawId>UInt16.MaxValue||count==0||count>500)throw new InvalidDataException("Production request bounds");
   Offline70ProductionRecipeFG410 recipe=Offline70ProductionFG410.Get((ushort)rawId);
   if(recipe==null)throw new InvalidDataException("Unknown production recipe "+rawId);
   // [success rate] 0 = the original always succeeds (isSuccessCompound: chance==0 -> true).
   bool success=recipe.Chance==0||Offline70ClearItemsFG125.Draw(session+"-production-"+sequence,"production-success",100)<(uint)recipe.Chance;
   int[] outputs=success?recipe.Outputs:recipe.FailOutputs;
   List<int[]> materials=Scale(recipe.Materials,count);
   List<int[]> products=Scale(outputs,count);
   uint gold=0;
   foreach(int[] row in materials){if(row[0]==0)gold=checked(gold+(uint)row[1]);}
   if(gold>assets.Gold)throw new InvalidDataException("Insufficient production gold");
   Sync();collection=CollectionSnapshot();Offline70EconomyState before=assets;bool duplicate;
   assets=Offline70Economy.Apply(economyPath,session+"-production-"+sequence,"production",
     rawId+":"+count+":"+(success?1:0)+":"+gold,
     delegate(Offline70EconomyState s){ApplyRowsFG410(s,materials,products,gold);},out duplicate);
   reply=Offline70RoleProtocol.BatchPacketFG(1,27,ProductionBodyFG410(before,assets,gold,materials,products),keys,false);
   committed=true;
   Send("production27",reply);
   byte[] changes=Offline70EconomyProtocol.InventoryChangeReply(before,assets,keys);
   if(changes!=null)Send("production-inventory",changes);
   Note("PRODUCTION_COMMITTED recipe="+rawId+" name="+recipe.Name+" count="+count+" success="+success+" gold="+gold+" outputs="+products.Count+" duplicate="+duplicate);
  }catch(InvalidDataException e){
   byte code=ProductionErrorFG410(e.Message);
   reply=Offline70RoleProtocol.BatchPacketFG(1,27,new byte[]{0,code},keys,false);
   Send("production-failed27",reply);
   if(collection!=null)collection=null;
   Note("PRODUCTION_REJECTED code="+code+" reason="+e.Message);
  }
  productionRequestsFG410.Add(sequence,fingerprint);
  productionRepliesFG410.Add(sequence,reply);
  if(committed){Sync();CollectionChanged(collection);}
 }

 internal static List<int[]> Scale(int[] pairs,ushort count){
  List<int[]> rows=new List<int[]>();
  for(int i=0;i<pairs.Length;i+=2)rows.Add(new int[]{pairs[i],checked(pairs[i+1]*count)});
  return rows;
 }
 // Native delete_event_items only walks the main inventory (INVEN_TYPE_INVEN) and the equipment
 // slots; production materials live in the material page, so the bag is the whole domain here.
 internal static void ApplyRowsFG410(Offline70EconomyState s,List<int[]> materials,List<int[]> products,uint gold){
  if(s.Gold<gold)throw new InvalidDataException("Insufficient production gold");
  s.Gold-=gold;
  foreach(int[] row in materials){if(row[0]==0)continue;ConsumeBagFG410(s,(uint)row[0],(uint)row[1]);}
  foreach(int[] row in products)Offline70Economy.AddAssetItem(s,(uint)row[0],(uint)row[1]);
 }
 internal static void ConsumeBagFG410(Offline70EconomyState s,uint item,uint count){
  ulong total=0;foreach(Offline70EconomyEntry e in s.Inventory)if(e.List==0&&e.ItemId==item)total+=e.Quantity;
  if(total<count)throw new InvalidDataException("Insufficient production material "+item);
  for(int i=s.Inventory.Count-1;i>=0&&count>0;i--){
   Offline70EconomyEntry e=s.Inventory[i];if(e.List!=0||e.ItemId!=item)continue;
   uint take=Math.Min(count,e.Quantity);e.Quantity-=take;count-=take;if(e.Quantity==0)s.Inventory.RemoveAt(i);
  }
  if(count!=0)throw new InvalidDataException("Insufficient production material "+item);
 }
 internal static byte ProductionErrorFG410(string message){
  if(message.StartsWith("Insufficient production material")||message.StartsWith("Insufficient production gold"))return 0x15;
  if(message.StartsWith("Economy bag capacity"))return 0x13;
  if(message.StartsWith("Production requires Roger"))return 0x13;
  return 0x11;
 }
 // Reply rows are built from the committed before/after states, in recipe order so the client
 // sees the gold row where the recipe lists it (Roger's rows are material + gold).
 internal static byte[] ProductionBodyFG410(Offline70EconomyState before,Offline70EconomyState after,uint gold,List<int[]> materials,List<int[]> products){
  List<byte[]> deleted=new List<byte[]>(),inserted=new List<byte[]>();
  foreach(int[] row in materials){
   if(row[0]==0){if(row[1]>0)deleted.Add(DeleteRowFG410(0,0,(uint)row[1]));continue;}
   Dictionary<int,uint> removed=new Dictionary<int,uint>();
   foreach(Offline70EconomyEntry old in before.Inventory){
    if(old.List!=0||old.ItemId!=(uint)row[0])continue;
    Offline70EconomyEntry current=Offline70Economy.Find(after,0,old.Slot);
    uint left=current!=null&&current.ItemId==old.ItemId?current.Quantity:0;
    if(old.Quantity>left)removed[old.Slot]=old.Quantity-left;
   }
   List<int> slots=new List<int>(removed.Keys);slots.Sort();
   foreach(int slot in slots)deleted.Add(DeleteRowFG410(3,(ushort)slot,removed[slot]));
  }
  foreach(int[] row in products){
   Dictionary<int,uint> gained=new Dictionary<int,uint>();
   foreach(Offline70EconomyEntry current in after.Inventory){
    if(current.List!=0||current.ItemId!=(uint)row[0])continue;
    Offline70EconomyEntry old=Offline70Economy.Find(before,0,current.Slot);
    uint prior=old!=null&&old.ItemId==current.ItemId?old.Quantity:0;
    if(current.Quantity>prior)gained[current.Slot]=current.Quantity-prior;
   }
   List<int> slots=new List<int>(gained.Keys);slots.Sort();
   foreach(int slot in slots)inserted.Add(InsertRowFG410((ushort)slot,(uint)row[0],gained[slot]));
  }
  using(MemoryStream m=new MemoryStream())using(BinaryWriter w=new BinaryWriter(m)){
   w.Write((byte)deleted.Count);foreach(byte[] r in deleted)w.Write(r);
   w.Write((byte)inserted.Count);foreach(byte[] r in inserted)w.Write(r);
   return m.ToArray();
  }
 }
 internal static byte[] DeleteRowFG410(byte space,ushort slot,uint count){
  using(MemoryStream m=new MemoryStream())using(BinaryWriter w=new BinaryWriter(m)){w.Write(space);w.Write(slot);w.Write(count);return m.ToArray();}
 }
 internal static byte[] InsertRowFG410(ushort slot,uint item,uint count){
  using(MemoryStream m=new MemoryStream())using(BinaryWriter w=new BinaryWriter(m)){
   w.Write((byte)0);w.Write(slot);w.Write(item);w.Write(count);
   w.Write((byte)0);w.Write((ushort)0);w.Write((byte)0);w.Write((ushort)0);w.Write((byte)0);
   // put_packet: random-option count 0, upgrade-separate, trade restriction
   w.Write((byte)0);w.Write((byte)0);w.Write((byte)0);
   return m.ToArray();
  }
 }
}
