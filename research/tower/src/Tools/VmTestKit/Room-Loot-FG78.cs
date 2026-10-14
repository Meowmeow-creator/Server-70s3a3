using System.Collections.Generic;
// Ground slot numbers belong to a room. Keep the same state when the client
// returns to that room; clear all rooms only when the instance ends/restarts.
class Offline70RoomLootFG78 {
 internal bool ObjectsPlannedFG167;
 internal readonly Dictionary<ushort,Offline70GroundFG> Ground=new Dictionary<ushort,Offline70GroundFG>();
 internal readonly HashSet<ushort> Picked=new HashSet<ushort>();
}
