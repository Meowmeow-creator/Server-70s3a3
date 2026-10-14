using System;using System.Collections.Generic;using System.IO;
// Values come from current CN cerashop and the corresponding skin scripts.
class Offline70SkinCashFG152 {
 internal class Row {internal uint Commodity,Item,Days,Price;internal byte Period,Options;}
 internal static readonly Dictionary<uint,Row> Rows=new Dictionary<uint,Row>();
 static Offline70SkinCashFG152(){RegisterRows();}
 static void RegisterRows(){Rows.Add(160000,new Row{Commodity=160000,Item=42201,Period=1,Days=7,Price=300,Options=2});
Rows.Add(160001,new Row{Commodity=160001,Item=42201,Period=2,Days=30,Price=660,Options=2});
Rows.Add(160002,new Row{Commodity=160002,Item=42201,Period=3,Days=0,Price=1980,Options=2});
Rows.Add(160003,new Row{Commodity=160003,Item=42202,Period=1,Days=7,Price=300,Options=2});
Rows.Add(160004,new Row{Commodity=160004,Item=42202,Period=2,Days=30,Price=660,Options=2});
Rows.Add(160005,new Row{Commodity=160005,Item=42202,Period=3,Days=0,Price=1980,Options=2});
Rows.Add(160006,new Row{Commodity=160006,Item=42203,Period=1,Days=7,Price=300,Options=2});
Rows.Add(160007,new Row{Commodity=160007,Item=42203,Period=2,Days=30,Price=660,Options=2});
Rows.Add(160008,new Row{Commodity=160008,Item=42203,Period=3,Days=0,Price=1980,Options=2});
Rows.Add(160009,new Row{Commodity=160009,Item=42204,Period=1,Days=7,Price=300,Options=2});
Rows.Add(160010,new Row{Commodity=160010,Item=42204,Period=2,Days=30,Price=660,Options=2});
Rows.Add(160011,new Row{Commodity=160011,Item=42204,Period=3,Days=0,Price=1980,Options=2});
Rows.Add(160012,new Row{Commodity=160012,Item=42205,Period=1,Days=7,Price=300,Options=2});
Rows.Add(160013,new Row{Commodity=160013,Item=42205,Period=2,Days=30,Price=660,Options=2});
Rows.Add(160014,new Row{Commodity=160014,Item=42205,Period=3,Days=0,Price=1980,Options=2});
Rows.Add(160015,new Row{Commodity=160015,Item=42206,Period=1,Days=7,Price=300,Options=2});
Rows.Add(160016,new Row{Commodity=160016,Item=42206,Period=2,Days=30,Price=660,Options=2});
Rows.Add(160017,new Row{Commodity=160017,Item=42206,Period=3,Days=0,Price=1980,Options=2});
Rows.Add(160018,new Row{Commodity=160018,Item=42207,Period=1,Days=7,Price=300,Options=2});
Rows.Add(160019,new Row{Commodity=160019,Item=42207,Period=2,Days=30,Price=660,Options=2});
Rows.Add(160020,new Row{Commodity=160020,Item=42207,Period=3,Days=0,Price=1980,Options=2});
Rows.Add(160021,new Row{Commodity=160021,Item=42208,Period=1,Days=7,Price=300,Options=2});
Rows.Add(160022,new Row{Commodity=160022,Item=42208,Period=2,Days=30,Price=660,Options=2});
Rows.Add(160023,new Row{Commodity=160023,Item=42208,Period=3,Days=0,Price=1980,Options=2});
Rows.Add(160024,new Row{Commodity=160024,Item=42209,Period=1,Days=7,Price=300,Options=2});
Rows.Add(160025,new Row{Commodity=160025,Item=42209,Period=2,Days=30,Price=660,Options=2});
Rows.Add(160026,new Row{Commodity=160026,Item=42209,Period=3,Days=0,Price=1980,Options=2});}
 internal static bool Known(uint item){foreach(Row row in Rows.Values)if(row.Item==item)return true;return false;}
 internal static bool AbilityValid(Offline70EconomyEntry e){foreach(Row row in Rows.Values)if(row.Item==e.ItemId)return e.Ability<row.Options;return false;}
 internal static Offline70CashPurchaseFG45.Offer Resolve(Offline70CashPurchaseFG45.Request request){
  Row row;if(request==null||!Rows.TryGetValue(request.Commodity,out row))return null;
  if(request.Payment!=0||request.Period!=row.Period||request.Option>=row.Options)throw new InvalidDataException("Skin cash payment, period or ability");
  return new Offline70CashPurchaseFG45.Offer{Commodity=row.Commodity,Category=0,Item=row.Item,Cost=row.Price,Days=row.Days,Special=8,Ability=request.Option,Rewards=new uint[][]{new uint[]{row.Item,1}}};
 }
 internal static bool Valid(Offline70CashPurchaseFG45.Offer offer){
  Row row;return offer!=null&&offer.Special==8&&Rows.TryGetValue(offer.Commodity,out row)&&offer.Category==0&&offer.Item==row.Item&&offer.Cost==row.Price&&offer.Days==row.Days&&offer.Ability<row.Options&&offer.Voucher==0&&!offer.Once&&offer.Rewards!=null&&offer.Rewards.Length==1&&offer.Rewards[0].Length==2&&offer.Rewards[0][0]==row.Item&&offer.Rewards[0][1]==1;
 }
 internal static void Grant(Offline70EconomyState state,Offline70CashPurchaseFG45.Offer offer,uint now){
  if(!Valid(offer))throw new InvalidDataException("Unregistered skin offer");
  Offline70EconomyItemDefinition definition=Offline70Economy.Definition(offer.Item);
  if(definition.Kind!=3||definition.EquipSlot!=8||definition.Job!=state.Job)throw new InvalidDataException("Skin character job");
  HashSet<ushort> occupied=new HashSet<ushort>();foreach(Offline70EconomyEntry e in state.Inventory)if(e.List==1)occupied.Add(e.Slot);
  Offline70Economy.AddAssetItem(state,offer.Item,1);Offline70EconomyEntry added=null;
  foreach(Offline70EconomyEntry e in state.Inventory)if(e.List==1&&e.ItemId==offer.Item&&!occupied.Contains(e.Slot))added=e;
  if(added==null)throw new InvalidDataException("Skin instance missing");added.Ability=offer.Ability;added.ExpiresUnix=offer.Days==0?0:checked(now+offer.Days*86400u);
 }
}
