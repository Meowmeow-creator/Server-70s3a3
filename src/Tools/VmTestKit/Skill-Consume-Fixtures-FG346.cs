using System;
using System.IO;
using System.Collections.Generic;
// FG346 fixture: 无色消耗表、op41 解包、多堆扣减、op14 绝对数量行、收据幂等。
class Offline70SkillCubeFixturesFG346 {
 static int checks;static List<string> names=new List<string>();
 static void Check(bool ok,string name){if(!ok)throw new Exception("Skill cube fixture failed: "+name);checks++;names.Add(name);}
 static byte[] Keys(){byte[] k=new byte[334];for(int i=0;i<334;i++)k[i]=(byte)i;return k;}
 public static int Main(string[] args){
  try{
   if(args.Length!=1)return 2;
   string dir=Path.GetFullPath(args[0]);Directory.CreateDirectory(dir);
   string metadata=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"item-metadata.xml");
   if(!File.Exists(metadata))throw new FileNotFoundException("item-metadata.xml must sit next to the fixture",metadata);
   Offline70ItemMetadataFG.Register(metadata);
   byte[] keys=Keys();

   // 1) 表：按 PVF [consume item] 生成的数量（.skl 原值：3037 2 2 这类）。
   Check(Offline70SkillCubeFG346.Cost(0,9)==1,"swordman momentaryslash costs 1");
   Check(Offline70SkillCubeFG346.Cost(0,85)==5,"swordman hellbenter costs 5");
   Check(Offline70SkillCubeFG346.Cost(0,88)==7,"swordman waveeye costs 7");
   Check(Offline70SkillCubeFG346.Cost(0,169)==0,"backstep costs nothing");
   Check(Offline70SkillCubeFG346.Cost(0,255)==0,"unknown skill costs nothing");
   Check(Offline70SkillCubeFG346.Cost(9,9)==0,"unknown job costs nothing");
   Check(Offline70SkillCubeFG346.Cost(1,63)==5,"fighter typhoon costs 5");
   Check(Offline70SkillCubeFG346.Cost(2,54)==5,"gunner satellitelaser costs 5");
   Check(Offline70SkillCubeFG346.Cost(3,46)==5,"mage summon costs the 3+1+1 row sum");
   Check(Offline70SkillCubeFG346.Cost(4,119)==5,"priest 119 costs 5");
   Check(Offline70SkillCubeFG346.Cost(5,100)==7,"atgunner 100 costs 7");
   Check(Offline70SkillCubeFG346.Cost(6,57)==7,"thief 57 costs 7");
   Check(Offline70SkillCubeFG346.Cost(7,41)==5,"atfighter 41 costs 5");
   Check(Offline70SkillCubeFG346.Cost(8,48)==5,"atmage summonluis costs 5");

   // 2) op41 解包：明文 {u8 gap, u8 skill, u16 target, ...}，密文块 16 字节（41%14=13）。
   byte[] plain=new byte[16];plain[0]=0;plain[1]=85;BitConverter.GetBytes((ushort)1500).CopyTo(plain,2);
   byte[] body=Offline70GoldCipher.Crypt(plain,keys,13,true);
   ushort target;int skill=Offline70SkillCubeFG346.Parse(body,keys,out target);
   Check(skill==85&&target==1500,"op41 plaintext round trip");
   Check(Offline70SkillCubeFG346.Cost(0,skill)==5,"op41 skill resolves to its cube cost");

   // 3) 多堆扣减：低槽先扣，扣空即移除。
   Offline70EconomyState s=new Offline70EconomyState();
   s.Inventory.Add(new Offline70EconomyEntry{List=0,Slot=110,ItemId=3037,Quantity=4});
   s.Inventory.Add(new Offline70EconomyEntry{List=0,Slot=105,ItemId=3037,Quantity=3});
   s.Inventory.Add(new Offline70EconomyEntry{List=0,Slot=57,ItemId=1000,Quantity=5});
   Check(Offline70SkillCubeFG346.Owned(s)==7,"owned sums every cube stack");
   Check(Offline70SkillCubeFG346.Take(s,5)==5,"take returns the debited amount");
   Check(Offline70Economy.Find(s,0,105)==null,"emptied stack is removed");
   Check(Offline70Economy.Find(s,0,110).Quantity==2,"second stack keeps the remainder");
   Check(Offline70SkillCubeFG346.Owned(s)==2,"owned after debit");
   Check(Offline70SkillCubeFG346.Take(s,0)==0,"zero cost takes nothing");
   Check(Offline70SkillCubeFG346.Owned(s)==2,"zero cost leaves the bag alone");

   // 4) op14 绝对数量行：body = {list 0, count u16, 31-byte row}，整块按 keys[4+i%4] 异或。
   byte[] update=Offline70SkillCubeFG346.UpdateReply(s,110,keys);
   byte[] decoded=new byte[update.Length-15];Buffer.BlockCopy(update,15,decoded,0,decoded.Length);
   for(int i=0;i<decoded.Length;i++)decoded[i]^=keys[4+(i%4)];
   Check(decoded[0]==0&&BitConverter.ToUInt16(decoded,1)==1,"update carries one row");
   Check(BitConverter.ToUInt16(decoded,3)==110&&BitConverter.ToUInt32(decoded,5)==3037,"update row names the cube stack");
   Check(BitConverter.ToUInt32(decoded,9)==2,"update row carries the absolute quantity");
   Offline70EconomyState empty=new Offline70EconomyState();
   byte[] gone=Offline70SkillCubeFG346.UpdateReply(empty,110,keys);
   byte[] goneBody=new byte[gone.Length-15];Buffer.BlockCopy(gone,15,goneBody,0,goneBody.Length);
   for(int i=0;i<goneBody.Length;i++)goneBody[i]^=keys[4+(i%4)];
   Check(BitConverter.ToUInt16(goneBody,3)==110&&BitConverter.ToUInt32(goneBody,5)==UInt32.MaxValue,"emptied bag reports the gone row");

   // 5) 落账幂等：同一收据重放不多扣。
   string path=Path.Combine(dir,"skill-cube-fixture.xml");
   if(File.Exists(path))throw new IOException("Fixture directory already has a store");
   Offline70EconomyState saved=Offline70Economy.Initialize(path,100,true);
   bool duplicate;
   saved=Offline70Economy.Apply(path,"fixture-seed-cube","skill-cube-seed","seed",delegate(Offline70EconomyState st){Offline70Economy.AddAssetItem(st,3037,6);},out duplicate);
   Check(!duplicate&&Offline70SkillCubeFG346.Owned(saved)==6,"seed six cubes");
   int[] taken={0};
   saved=Offline70Economy.Apply(path,"fixture-cast-1","skill-cube","0:85:5",delegate(Offline70EconomyState st){taken[0]=Offline70SkillCubeFG346.Take(st,5);},out duplicate);
   Check(!duplicate&&taken[0]==5&&Offline70SkillCubeFG346.Owned(saved)==1,"first cast debits five");
   Check(Offline70SkillCubeFG346.Owned(saved)==1,"first cast persists the debit");
   saved=Offline70Economy.Apply(path,"fixture-cast-1","skill-cube","0:85:5",delegate(Offline70EconomyState st){taken[0]=Offline70SkillCubeFG346.Take(st,5);},out duplicate);
   Check(duplicate&&Offline70SkillCubeFG346.Owned(saved)==1,"replayed receipt does not debit twice");
   saved=Offline70Economy.Apply(path,"fixture-cast-2","skill-cube","0:88:7",delegate(Offline70EconomyState st){taken[0]=Offline70SkillCubeFG346.Take(st,7);},out duplicate);
   Check(!duplicate&&taken[0]==1&&Offline70SkillCubeFG346.Owned(saved)==0,"short bag takes what is left");
   File.WriteAllText(Path.Combine(dir,"skill-cube-fixture-result.txt"),"checks="+checks+Environment.NewLine+String.Join(Environment.NewLine,names.ToArray()));
   Console.WriteLine("SKILL_CUBE_FIXTURE_OK checks="+checks);
   return 0;
  }catch(Exception e){Console.WriteLine("SKILL_CUBE_FIXTURE_FAILED "+e.Message);return 1;}
 }
}
