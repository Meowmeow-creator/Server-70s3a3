using System;using System.IO;using System.Collections.Generic;using System.Xml;using System.Xml.Serialization;
// FG410: 罗杰（npc 29）的生产/制作窗口数据。来源 itemshop/itemproduct1.shp（shop 20）里的 49 条
// 配方 .stk，按 rednf 的 createRecipeInfo 语义解析 [int data]：
//   n1 材料对（item 0 = 金币），n2 产物对，n3 需求技能对，可选尾部标记。
// 服务端入口是客户端 command 27（DisPatcher_Compound），请求 int itemId + byte type + ushort count；
// type==1 = 按物品 id 直接取配方（罗杰窗口），材料/产物按数量倍乘。
[XmlType("Offline70ProductionRecipeFG")]public class Offline70ProductionRecipeFG410 {
 public ushort Id;public string Name,Path;
 public int[] Materials=new int[0],Outputs=new int[0],Skills=new int[0],FailOutputs=new int[0];
 public int Chance,MaintainAbility;
}
[XmlRoot("Offline70ProductionDocumentFG")]public class Offline70ProductionDocumentFG410 {
 public int Format=1;public string PvfSha256;public int Shop,Npc;
 public List<Offline70ProductionRecipeFG410> Recipes=new List<Offline70ProductionRecipeFG410>();
}
// The table is validated once at boot against the registrar: every material/output must be a
// registered stackable, because the transaction below pays them out through Offline70Economy.
class Offline70ProductionFG410 {
 internal const int MaximumBytes=1048576;
 internal const int MaximumRecipes=256;
 internal const int ShopId=20;
 internal const int NpcId=29;
 // 罗杰只站在西海岸（town3）；原生 27 号命令本身不校验 NPC，客户端只从他的窗口发包。
 internal const int TownId=3;
 static readonly Dictionary<ushort,Offline70ProductionRecipeFG410> Table=new Dictionary<ushort,Offline70ProductionRecipeFG410>();
 internal static int Count{get{return Table.Count;}}
 internal static void Load(string path){
  if(Table.Count!=0)return;
  if(new FileInfo(path).Length>MaximumBytes)throw new InvalidDataException("Production recipe catalog size");
  Offline70ProductionDocumentFG410 document;
  using(FileStream f=File.OpenRead(path))using(XmlTextReader r=new XmlTextReader(f)){r.ProhibitDtd=true;r.XmlResolver=null;document=(Offline70ProductionDocumentFG410)new XmlSerializer(typeof(Offline70ProductionDocumentFG410)).Deserialize(r);}
  if(document.Format!=1||document.PvfSha256!=Offline70QuestCatalogFG.CurrentPvf||document.Shop!=ShopId||document.Npc!=NpcId||document.Recipes==null||document.Recipes.Count<1||document.Recipes.Count>MaximumRecipes)throw new InvalidDataException("Production recipe catalog profile");
  foreach(Offline70ProductionRecipeFG410 q in document.Recipes){
   if(q==null||q.Id==0||Table.ContainsKey(q.Id)||String.IsNullOrEmpty(q.Name)||String.IsNullOrEmpty(q.Path))throw new InvalidDataException("Production recipe identity");
   Pairs(q.Materials,"material",true,q.Id);
   Pairs(q.Outputs,"output",false,q.Id);
   Pairs(q.Skills,"skill",false,q.Id);
   Pairs(q.FailOutputs,"fail output",false,q.Id);
   if(q.Materials.Length<2||q.Materials.Length>64||q.Outputs.Length<2||q.Outputs.Length>32)throw new InvalidDataException("Production recipe width "+q.Id);
   if(q.Chance<0||q.Chance>100||q.MaintainAbility!=0&&q.MaintainAbility!=1)throw new InvalidDataException("Production recipe flags "+q.Id);
   if(q.Skills.Length!=0)throw new InvalidDataException("Production skill rows are not modelled "+q.Id);
   for(int i=0;i<q.Materials.Length;i+=2)if(q.Materials[i]!=0)Kind(q.Materials[i],"material",q.Id);
   for(int i=0;i<q.Outputs.Length;i+=2)Kind(q.Outputs[i],"output",q.Id);
   for(int i=0;i<q.FailOutputs.Length;i+=2)Kind(q.FailOutputs[i],"fail output",q.Id);
   Table.Add(q.Id,q);
  }
 }
 static void Pairs(int[] values,string what,bool allowGold,ushort id){
  if(values==null||values.Length%2!=0)throw new InvalidDataException("Production "+what+" pairs "+id);
  for(int i=0;i<values.Length;i+=2){
   int item=values[i],count=values[i+1];
   if(item==0){if(!allowGold)throw new InvalidDataException("Production "+what+" gold row "+id);if(count<=0||count>1000000)throw new InvalidDataException("Production gold row "+id);continue;}
   if(item<=2||item>Int32.MaxValue||count<=0||count>1000000)throw new InvalidDataException("Production "+what+" pair "+id);
  }
 }
 static void Kind(int item,string what,ushort id){
  Offline70EconomyItemDefinition d;
  try{d=Offline70Economy.Definition((uint)item);}
  catch(InvalidDataException e){throw new InvalidDataException("Production "+what+" item "+item+" recipe "+id+": "+e.Message);}
  // Roger's core production turns materials into materials; equipment-output recipes (the 2012
  // snow-accessory event) are deliberately not in this table, see gen_production_recipes_FG410.py.
  if(d.Kind!=1&&d.Kind!=2&&d.Kind!=9&&d.Kind!=10)throw new InvalidDataException("Production "+what+" category "+id+" item="+item);
 }
 internal static Offline70ProductionRecipeFG410 Get(ushort id){Offline70ProductionRecipeFG410 q;Table.TryGetValue(id,out q);return q;}
}
