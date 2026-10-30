using System;using System.IO;
// Compatibility shim. FG350 registered a single legacy-lottery capsule (7036 压缩的胶囊) with a
// hand-copied table; FG373 replaced it with the PVF-driven container table carrying every
// [legacy] / [stackable legacy] / [upgradable legacy] item the server can hold
// (Container-Lottery-FG373.cs, data/container-lottery-FG373.dat). The API is kept because the
// offline drivers and the op47 route are written against it; the draw still hashes the receipt,
// so a duplicated sequence keeps reproducing its committed grant.
class Offline70CapsuleLotteryFG350 {
 internal const uint Source=7036;
 internal static bool Registered(uint item){return Offline70ContainerLotteryFG373.Registered(item);}
 internal static uint Roll(string receipt){
  if(String.IsNullOrEmpty(receipt))throw new InvalidDataException("Capsule roll receipt");
  return Offline70ContainerLotteryFG373.Roll(receipt,Source);
 }
}
