using System;
partial class Offline70DungeonFlowFG {
 internal ushort GblQuestMapFG171;internal byte GblQuestXFG171,GblQuestYFG171;
 internal void ConfigureGblQuestFG171(Offline70ProgressionStateFG state){GblQuestMapFG171=0;if(Difficulty<1)return;if(DungeonId==21&&state.Find(23)!=null){GblQuestMapFG171=3025;GblQuestXFG171=5;GblQuestYFG171=0;}
if(DungeonId==22&&state.Find(50)!=null){GblQuestMapFG171=3255;GblQuestXFG171=3;GblQuestYFG171=0;}
if(DungeonId==23&&state.Find(51)!=null){GblQuestMapFG171=3425;GblQuestXFG171=4;GblQuestYFG171=3;}
if(DungeonId==24&&state.Find(52)!=null){GblQuestMapFG171=3641;GblQuestXFG171=2;GblQuestYFG171=0;}
}
 internal ushort GblMapFG171(byte x,byte y){return GblQuestMapFG171!=0&&x==GblQuestXFG171&&y==GblQuestYFG171?GblQuestMapFG171:(ushort)0;}
 ushort[][] GblMonstersFG171(){switch(CurrentMap){case 3025:return new ushort[][]{new ushort[]{250,31,0},new ushort[]{250,31,0},new ushort[]{260,30,0},new ushort[]{260,30,0},new ushort[]{263,30,0},new ushort[]{50009,32,0},new ushort[]{263,30,0},new ushort[]{260,30,0},new ushort[]{260,30,0},new ushort[]{250,31,0},new ushort[]{250,31,0}};
case 3255:return new ushort[][]{new ushort[]{500,32,0},new ushort[]{263,31,0},new ushort[]{263,31,0},new ushort[]{501,32,0},new ushort[]{501,32,0},new ushort[]{263,31,0},new ushort[]{263,31,0},new ushort[]{50011,33,0},new ushort[]{263,31,0},new ushort[]{263,31,0},new ushort[]{500,32,0}};
case 3425:return new ushort[][]{new ushort[]{602,34,0},new ushort[]{602,33,0},new ushort[]{602,33,0},new ushort[]{260,33,0},new ushort[]{261,34,0},new ushort[]{263,33,0},new ushort[]{602,33,0},new ushort[]{602,33,0},new ushort[]{50012,35,0},new ushort[]{261,34,0},new ushort[]{263,33,0},new ushort[]{602,33,0},new ushort[]{602,34,0},new ushort[]{602,33,0},new ushort[]{260,33,0}};
case 3641:return new ushort[][]{new ushort[]{603,33,0},new ushort[]{603,33,0},new ushort[]{263,33,0},new ushort[]{603,34,0},new ushort[]{50010,35,0},new ushort[]{603,34,0},new ushort[]{262,34,0},new ushort[]{262,34,0},new ushort[]{603,33,0},new ushort[]{603,33,0},new ushort[]{260,33,0},new ushort[]{260,33,0},new ushort[]{263,33,0}};
default:return null;}}
}
