using System;using System.Collections.Generic;using System.IO;using System.Xml;using System.Xml.Serialization;
// FG381: account-wide premium (contract) ledger.
//
// Native anchor: the premium list lives on the CUser account session (WongWork::CUserPremium),
// not on CCharacter - 达人/霸王契约 bought on one role light up every role of the account. This
// engine kept the list on each role's economy archive, so contracts were role-bound (2026-10-28
// report 角色不互通). The ledger below is the account-level source of truth; each role's
// PremiumsFG129 list stays a read cache that is merged up at every Sync (purchase/GM add
// propagate) and refreshed from the ledger, so a role that only reads still gets the contract.
public class Offline70AccountPremiumDocumentFG381 {public int Format=1;public List<Offline70PremiumFG129> Premiums=new List<Offline70PremiumFG129>();}
class Offline70AccountPremiumsFG381 {
 static readonly XmlSerializer Serializer=new XmlSerializer(typeof(Offline70AccountPremiumDocumentFG381));
 internal static string PathFor(string folder){return Path.Combine(folder,"premiums-account1-FG381.xml");}
 internal static List<Offline70PremiumFG129> Load(string folder){
  string path=PathFor(folder);if(!File.Exists(path))return new List<Offline70PremiumFG129>();
  using(FileStream f=File.OpenRead(path))using(XmlTextReader r=new XmlTextReader(f)){
   r.ProhibitDtd=true;r.XmlResolver=null;
   Offline70AccountPremiumDocumentFG381 d=(Offline70AccountPremiumDocumentFG381)Serializer.Deserialize(r);
   if(d==null||d.Format!=1||d.Premiums==null||d.Premiums.Count>32)throw new InvalidDataException("Account premium ledger profile");
   HashSet<byte> kinds=new HashSet<byte>();foreach(Offline70PremiumFG129 p in d.Premiums)if(p==null||p.Kind>105||p.Expires==0||!kinds.Add(p.Kind))throw new InvalidDataException("Account premium expiry state");
   return d.Premiums;}
 }
 internal static void Save(string folder,List<Offline70PremiumFG129> premiums){
  string path=PathFor(folder);
  Offline70AccountPremiumDocumentFG381 d=new Offline70AccountPremiumDocumentFG381{Premiums=premiums};
  string tmp=path+".pending";using(FileStream f=File.Create(tmp))Serializer.Serialize(f,d);
  if(File.Exists(path))File.Replace(tmp,path,null);else File.Move(tmp,path);
 }
 // Merge-up: only extends, so it is safe to feed it any role cache at any time.
 internal static void Merge(string folder,IEnumerable<Offline70PremiumFG129> incoming){
  List<Offline70PremiumFG129> merged=Load(folder);bool changed=false;
  foreach(Offline70PremiumFG129 p in incoming){
   if(p==null)continue;
   Offline70PremiumFG129 found=null;foreach(Offline70PremiumFG129 q in merged)if(q.Kind==p.Kind)found=q;
   if(found==null){merged.Add(new Offline70PremiumFG129{Kind=p.Kind,Expires=p.Expires});changed=true;}
   else if(p.Expires>found.Expires){found.Expires=p.Expires;changed=true;}
  }
  if(changed)Save(folder,merged);
 }
 internal static void Remove(string folder,byte kind){
  List<Offline70PremiumFG129> merged=Load(folder);int before=merged.Count;
  merged.RemoveAll(delegate(Offline70PremiumFG129 p){return p.Kind==kind;});
  if(merged.Count!=before)Save(folder,merged);
 }
}
