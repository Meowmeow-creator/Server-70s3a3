using System;using System.IO;
// Original 70 notification 136 handler reads no fields and clears the town loading flag.
partial class Offline70RoleProtocol {
 internal static byte[] EnterWorldCompleteBody(){return System.Text.Encoding.ASCII.GetBytes("0000000");}
 internal static byte[] EnterWorldCompleteReply(Offline70Role role,byte[] keys){
  Offline70TownProtocol.Validate(role,keys);
  // Opaque seven-byte diagnostic body from the ACT12 reference; the 70 handler ignores it.
  // Native index 10, key offset 252, block size 8; encrypt/decrypt and gate verified separately.
  return Packet(0,136,Offline70TownProtocol.Hex("fccc1c2fae869bae"));
 }
}
