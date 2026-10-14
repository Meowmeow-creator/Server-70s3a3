using System;using System.IO;
// Fixed valid seven-cell layout from LorienInside.dgn; AA cells are absent.
// Each selected map retains its current-PVF monster template order and codes.
class Offline70DeepLorienFG72 {
 internal static bool Exists(byte x,byte y){return Map(x,y)!=0;}
 internal static ushort Map(byte x,byte y){if(x>2||y>2)return 0;switch(y*3+x){
 case 0:return 116;
 case 3:return 104;
 case 6:return 101;
 case 1:return 109;
 case 4:return 107;
 case 7:return 111;
 case 5:return 117;
 default:return 0;}}
 internal static ushort[][] Monsters(byte x,byte y){switch(Map(x,y)){
 case 101:return new ushort[][]{new ushort[]{1,2,0},new ushort[]{1,2,0},new ushort[]{2,2,0},new ushort[]{1,2,0},new ushort[]{2,2,0}};
 case 104:return new ushort[][]{new ushort[]{2,2,0},new ushort[]{1,2,0},new ushort[]{2,2,0},new ushort[]{1,2,0},new ushort[]{1,2,0}};
 case 107:return new ushort[][]{new ushort[]{1,2,0},new ushort[]{1,2,0},new ushort[]{2,2,0},new ushort[]{1,2,0},new ushort[]{1,2,0},new ushort[]{2,2,0}};
 case 109:return new ushort[][]{new ushort[]{2,2,0},new ushort[]{2,2,0},new ushort[]{2,2,0},new ushort[]{2,2,0}};
 case 111:return new ushort[][]{new ushort[]{1,2,0},new ushort[]{1,2,0},new ushort[]{1,2,0},new ushort[]{2,2,0},new ushort[]{2,2,0}};
 case 116:return new ushort[][]{new ushort[]{2,2,0},new ushort[]{1,2,0},new ushort[]{1,2,0},new ushort[]{2,2,0},new ushort[]{2,2,0}};
 case 117:return new ushort[][]{new ushort[]{2,2,0},new ushort[]{1,2,0},new ushort[]{11,2,3},new ushort[]{2,2,0},new ushort[]{1,2,0}};
 default:throw new InvalidDataException("Unsupported LorienInside cell");}}
}
