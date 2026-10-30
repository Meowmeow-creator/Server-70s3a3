// FG389 — 疑惑之村 (dungeon 53) 起始房的电梯。
//
// 原版实现（rednf = 台服 1031 df_game_r 逐指令重建，注释里的地址是 ORIG）：
//   · CParty::check_allmember_loading 0x085b1ad4：一间房装载完成时
//       if (BF.m_dungeonIndex == 0x35 && BF.m_elevatorFlag != 0)
//         m_elevatorTimer=0, m_elevatorState=2, m_elevatorFlag=0,
//         TimerQueue.InsertTimer(OBJ_TYPE_1, partyIndex, msg 0x28, 15 秒, key, 0)
//     m_elevatorFlag 由 reset_field 置 1，所以只有一局的第一间房会开计时。
//   · CBattle_Field::check_elevator_timer 0x0830d902（TimerElevatorDungeon, msg 0x28）：
//       ++m_elevatorTimer;
//       if (m_elevatorTimer < 4 && m_elevatorState == 2) 再排一次 15 秒；
//       if (m_elevatorState == 2) send_elevator_time_packet(0);
//   · CBattle_Field::send_elevator_time_packet 0x0830d848：
//       notify 0xfd（PacketNameTables: g_szNotiPacketName[253] =
//       ENUM_NOTIPACKET_ELEVATOR_CLEAR_TIME_CHECK），包体 2 字节
//       { byte m_elevatorTimer, byte state }。
//     注意：253 是台服 1031 的编号。本机 CN70 客户端 (DNF70_v50.exe) 把同一个处理器注册在
//     op 265：注册点 file 0x9EB49 = push 0x471290 / push 0x109 / call 0x915E0；handler
//     0x471290 先读两个 byte，再在副本被动对象表里找 id 0x459 (1113 ElevatorControl)，
//     把第一个 byte 写进 [obj+0xCD4]、第二个写进 [obj+0xCD0]。所以这里发包用 265。
//   · CBattle_Field::kill_monster 0x0830d082 段：击杀 m_mobIndex == 0xdd8c
//     (=56716 独眼巨人) 时
//       m_elevatorState = (signed char)m_elevatorTimer <= 3 ? 1 : 2;
//       send_elevator_time_packet(m_elevatorState);
//
// 客户端靠这串包推进/结束起始房电梯的下降。本服 FG365 只补了
// SuspiciousVillage/(2,5)start.map 的 [special passive object] 1112 -> [monster] 56716
// （对应 native CMap::set_map 的 insert_monster），没有电梯计时和 0xfd 包，所以电梯一直
// 循环下降、走不出起始房（2026-10-29 玩家反馈）。这里按 ORIG 的字段、时序、包体复现。
partial class Offline70DungeonFlowFG {
 internal const ushort SuspicionElevatorMonsterFG389=56716;   // 0xdd8c
 internal const int ElevatorTickSecondsFG389=15;
 readonly System.Diagnostics.Stopwatch elevatorClockFG389=new System.Diagnostics.Stopwatch();
 bool elevatorArmedFG389;int elevatorTimerFG389,elevatorStateFG389;bool elevatorRunningFG389;
 // reset_field 0x082f???? 段：每次开局 state=2/timer=0/flag=1。
 void ResetElevatorFG389(ushort dungeon){elevatorArmedFG389=dungeon==53;elevatorTimerFG389=0;elevatorStateFG389=2;elevatorRunningFG389=false;elevatorClockFG389.Reset();}
 // check_allmember_loading：一局只有第一间房装载完成会走到这里（armed 只置一次）。
 internal void ArmElevatorFG389(){if(!elevatorArmedFG389)return;elevatorArmedFG389=false;elevatorRunningFG389=true;elevatorTimerFG389=0;elevatorStateFG389=2;elevatorClockFG389.Restart();}
 internal bool ElevatorTickFG389(out byte timer,out byte state){return ElevatorTickFG389(elevatorClockFG389.ElapsedMilliseconds,out timer,out state);}
 // 每 15 秒一个 tick，最多 4 个；state 一旦离开 2，原生就不再续排也不再发包。
 // elapsedMs 显式传入，离线验收可以喂任意时间点，不需要真等 60 秒。
 internal bool ElevatorTickFG389(long elapsedMs,out byte timer,out byte state){
  timer=(byte)elevatorTimerFG389;state=0;
  if(!elevatorRunningFG389)return false;
  if(elapsedMs<(long)(elevatorTimerFG389+1)*ElevatorTickSecondsFG389*1000)return false;
  elevatorTimerFG389++;
  if(elevatorTimerFG389>=4)elevatorRunningFG389=false;      // 只有 timer<4 才续排
  if(elevatorStateFG389!=2){elevatorRunningFG389=false;return false;}  // 只有 state==2 才发 state=0
  timer=(byte)elevatorTimerFG389;return true;
 }
 // kill_monster 0xdd8c：timer<=3 -> state 1，否则 state 2；包体带当前 timer。
 internal bool ElevatorKillFG389(out byte timer,out byte state){
  timer=(byte)elevatorTimerFG389;state=0;
  if(!Active)return false;
  elevatorStateFG389=elevatorTimerFG389<=3?1:2;state=(byte)elevatorStateFG389;return true;
 }
}
