using System;using System.IO;using System.Xml;using System.Xml.Serialization;using System.Collections.Generic;
public class Offline70ItemMetadataDocumentFG {public int Format=1;public string PvfSha256;public List<Offline70EconomyItemDefinition> Items=new List<Offline70EconomyItemDefinition>();}
class Offline70ItemMetadataFG {
 // 64 MiB / 100000 records: the GM registration of weapons, armor, jewelry, support/magic stone and
 // titles alone adds ~20800 records, which no longer fits the old 8 MiB / 20000 caps.
 internal const long MaximumBytes=67108864;internal const int MaximumItems=100000;
 internal static void Register(string path){if(new FileInfo(path).Length>MaximumBytes)throw new InvalidDataException("Item metadata size");Offline70ItemMetadataDocumentFG d;using(FileStream f=File.OpenRead(path))using(XmlTextReader r=new XmlTextReader(f)){r.ProhibitDtd=true;r.XmlResolver=null;d=(Offline70ItemMetadataDocumentFG)new XmlSerializer(typeof(Offline70ItemMetadataDocumentFG)).Deserialize(r);}if(d.Format!=1||d.PvfSha256!=Offline70QuestCatalogFG.CurrentPvf||d.Items==null||d.Items.Count>MaximumItems)throw new InvalidDataException("Item metadata profile");HashSet<uint> seen=new HashSet<uint>();foreach(Offline70EconomyItemDefinition item in d.Items){if(!seen.Add(item.ItemId))throw new InvalidDataException("Duplicate item metadata");bool exists=true;try{Offline70Economy.Definition(item.ItemId);}catch(InvalidDataException){exists=false;}if(!exists)Offline70Economy.RegisterMetadata(item);}}
}
