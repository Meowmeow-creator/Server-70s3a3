using System;using System.IO;using System.Globalization;using System.Xml;
// Explicit local UI action. This is not a game opcode or paid activation.
class Offline70BlackLocalFG163 {
 internal static int Run(string[] args){
  try{
   if(args.Length!=4&&args.Length!=6||args[1]!="--batch-store")throw new InvalidDataException("Local black arguments");
   string store=Path.GetFullPath(args[2]),economy=Path.Combine(store,"economy-account1-yoyo-FG.xml");
   if(Path.GetFileName(store)!="DNF70-batch-test-FG"||!File.Exists(economy)||!File.Exists(Path.Combine(store,"progression-yoyo-FG.xml")))throw new InvalidDataException("Existing separate local store required");
   // Check the existing character file without registering tables, migrating,
   // refreshing daily state, or writing any character progress.
   using(XmlTextReader reader=new XmlTextReader(economy)){reader.ProhibitDtd=true;reader.XmlResolver=null;reader.MoveToContent();if(reader.Name!="Offline70EconomyState")throw new InvalidDataException("Local economy identity");}
   DateTime now=DateTime.UtcNow;bool duplicate=false;Offline70BlackAccountFG158 profile;
   if(args[3]=="--black-test-status"&&args.Length==4)profile=Offline70BlackFG158.Load(economy);
   else if(args[3]=="--black-test-activate"&&args.Length==6){
    Guid id;try{id=new Guid(args[4]);}catch{throw new InvalidDataException("Local activation receipt");}
    uint seconds;if(!UInt32.TryParse(args[5],NumberStyles.None,CultureInfo.InvariantCulture,out seconds)||seconds!=60&&seconds!=86400&&seconds!=604800&&seconds!=2592000)throw new InvalidDataException("Local activation term");
    profile=Offline70BlackFG158.TestExtend(economy,"local-ui-FG163-"+id.ToString("N"),seconds,now,out duplicate);
    // Qualification was atomically committed by the service's account writer.
    // Do not alter economy, skills, quests or currency from this entry.
    File.AppendAllText(Path.GetFullPath(args[0]),now.ToString("o")+" LOCAL_BLACK_TEST_COMMITTED seconds="+seconds+" expires="+profile.ExpiresUnix+" duplicate="+duplicate+" noCharge=True characterUntouched=True\r\n");
   }else throw new InvalidDataException("Local black mode");
   Console.WriteLine("OK\t"+(profile.ExpiresUnix>Offline70BlackFG158.Unix(now)?"1":"0")+"\t"+profile.ExpiresUnix+"\t"+profile.Revision+"\t"+(duplicate?"1":"0"));return 0;
  }catch(Exception e){Console.Error.WriteLine(e.GetType().Name+": "+e.Message);return 3;}
 }
}
