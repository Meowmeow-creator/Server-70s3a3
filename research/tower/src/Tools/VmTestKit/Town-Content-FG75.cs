using System;using System.IO;
// Original CN70 town/map links, NPCs and corresponding Seria rooms.
class Offline70TownContentFG75 {
 internal static bool TownSupported(int town){return town>=1&&town<=3;}
 internal static int SeriaArea(int town){if(!TownSupported(town))throw new InvalidDataException("Unsupported town region");return town==1?1:town==2?5:2;}
 internal static bool Exists(int town,int area){switch(town*16+area){
 case 16:return true;
 case 17:return true;
 case 18:return true;
 case 32:return true;
 case 33:return true;
 case 34:return true;
 case 35:return true;
 case 36:return true;
 case 37:return true;
 case 38:return true;
 case 48:return true;
 case 49:return true;
 case 50:return true;
 case 51:return true;
 case 52:return true;
 case 53:return true;
 case 54:return true;
 case 55:return true;
 default:return false;}}
 internal static bool Npc(int town,int area,int npc){switch(town*16+area){
 case 16:return npc==1||npc==8;
 case 17:return npc==2||npc==22||npc==32;
 case 32:return npc==6||npc==13||npc==17||npc==20||npc==75||npc==84;
 case 33:return npc==15||npc==18||npc==27||npc==45||npc==92||npc==93||npc==94||npc==95||npc==97;
 case 34:return npc==3||npc==4||npc==7||npc==36||npc==80;
 case 35:return npc==9||npc==64;
 case 36:return npc==5;
 case 37:return npc==2||npc==22||npc==32;
 case 38:return npc==123;
 case 48:return npc==10||npc==11||npc==15||npc==21||npc==29||npc==30;
 case 49:return npc==12||npc==28||npc==38;
 case 50:return npc==2||npc==22||npc==32;
 case 52:return npc==19;
 case 54:return npc==60;
 case 55:return npc==72;
 default:return false;}}
 internal static int[][] Portals(int town,int area){switch(town*16+area){
 case 16:return new int[][]{new int[]{-5,192,29,141,2,1},new int[]{730,147,110,22,1,1},new int[]{1540,189,28,144,1,2}};
 case 17:return new int[][]{new int[]{330,324,289,24,1,0}};
 case 18:return new int[][]{new int[]{-2,176,21,143,1,0}};
 case 32:return new int[][]{new int[]{1,230,19,195,2,6},new int[]{1172,157,107,21,2,2},new int[]{1396,456,100,40,2,1},new int[]{3210,169,96,14,2,4},new int[]{3555,223,21,220,3,0},new int[]{1552,170,79,24,2,5}};
 case 33:return new int[][]{new int[]{1855,163,106,18,2,0},new int[]{3777,219,20,117,1,0}};
 case 34:return new int[][]{new int[]{1133,155,87,19,2,3},new int[]{1289,350,95,19,2,0}};
 case 35:return new int[][]{new int[]{462,342,91,20,2,2}};
 case 36:return new int[][]{new int[]{74,170,21,77,2,0}};
 case 37:return new int[][]{new int[]{330,324,289,24,2,0}};
 case 38:return new int[][]{new int[]{873,122,22,187,2,0}};
 case 48:return new int[][]{new int[]{-2,206,18,250,2,0},new int[]{287,164,99,16,3,1},new int[]{723,168,99,15,3,2},new int[]{2893,219,18,237,3,3},new int[]{2500,476,94,18,3,5}};
 case 49:return new int[][]{new int[]{333,351,100,15,3,0},new int[]{1765,221,21,110,3,4},new int[]{1173,161,91,21,3,6},new int[]{13,223,30,107,3,7}};
 case 50:return new int[][]{new int[]{330,324,289,24,3,0}};
 case 51:return new int[][]{new int[]{4,114,19,188,3,0}};
 case 52:return new int[][]{new int[]{2,221,21,129,3,1}};
 case 53:return new int[][]{new int[]{4,114,19,188,3,0}};
 case 54:return new int[][]{new int[]{2,221,21,129,3,1}};
 case 55:return new int[][]{new int[]{871,230,23,127,3,1}};
 default:throw new InvalidDataException("Unsupported town area");}}
 internal static bool Linked(int town,int area,int targetTown,int targetArea){foreach(int[] p in Portals(town,area))if(p[4]==targetTown&&p[5]==targetArea)return true;return false;}
 internal static bool DungeonGate(int town,int area){return town==1&&area==2||town==2&&area==6||town==3&&(area==3||area==4||area==5||area==6||area==7);}
}
