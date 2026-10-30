using System;using System.Collections.Generic;using System.IO;using System.Text;
// Death-god tower dialogue: the [tower dialog] rows of dungeon/towers/towerofillusion.dgn and
// towerofdeath.dgn, verbatim (stage, on start / on complete). The CN70 client plays these from its
// own tower scene, which never starts for an offline entry (no op162, no tower HUD - live
// 2026-10-27), so the server delivers the same lines through op12 system chat at floor load and
// floor clear. Text is GBK on the wire, exactly like the native make_packet_chat_msg body.
internal static class Offline70TowerDialogFG363 {
 static readonly Encoding TextEncoding=Encoding.GetEncoding(936,EncoderFallback.ExceptionFallback,DecoderFallback.ExceptionFallback);
 static readonly Dictionary<int,string> Illusion=new Dictionary<int,string>{
  {2,"死神：我是死神， 在绝望中寻求新生的人啊， 欢迎你来到迷妄之塔！"},
  {3,"死神：时间就是生命， 不要浪费它。"},
  {4,"死神：怕了吗？ 死亡游戏才刚刚开始呢！"},
  {5,"死神：我的欢迎仪式结束了， 你可以奔赴死亡的邀请了。"},
  {6,"死神：我要掷色子了……"},
  {7,"死神：看来我这次掷的点数有点小啊。"},
  {8,"死神：灵活的脚步是战斗的基础， 我会在背后看着你的……"},
  {9,"死神：你的步伐有点凌乱， 以后要多锻炼， 继续往上走吧。"},
  {10,"死神：从现在开始， 要玩命了， 你有心理准备了吗？"},
  {11,"死神：看来我之前小看你了。"},
  {12,"死神：这是专门为你准备的……地狱之行！ 你可以开始了！"},
  {13,"死神：哼哼……别以为亡者们只有这点力量。"},
  {14,"死神：战前逃跑会被人唾弃的， 希望你能坚持到最后一秒。"},
  {15,"死神：骄傲自满必然会带来失败， 这是无数人用自身实践证明的。"},
  {16,"死神：忠告你一句， 惹谁都不要惹那个家伙！"},
  {17,"死神：绝望的乌云已经开始笼罩着你……"},
  {18,"死神：如果你在这里倒下， 就算以后成功了也还是失败， 希望你不是这样的人。"},
  {19,"死神：来者是客， 我好像怠慢你了， 嘿嘿。"},
  {20,"死神：凭你那慢吞吞的身法， 不用猜都知道你一定会吃亏的。"},
  {21,"死神：不管你用什幺方法， 一定要找到让我开心的东西。"},
  {22,"死神：还没开战就先示弱了三分， 你想不输也难。"},
  {23,"死神：嘿嘿， 你发现了吗？ 事情在不知不觉中变得有趣了。"},
  {24,"无论你从哪个角度攻击， 我都能100%防御。 放马过来吧！"},
  {25,"死神：原本以为你会输得很惨， 没想到你竟然破了绝对防御， 了不起！"},
  {26,"死神：我这里不欢迎没有战斗力的人， 你要好好把握这次机会。"},
  {27,"死神：嗯， 做得好。 上去吧。"},
  {28,"死神：在这里， 你会发现喘口气的时间都是宝贵的！"},
  {29,"死神：有个人已经等你很久了， 快上去吧。"},
  {30,"现身吧， 我忠实的部下们！ 该是向客人展示九龙威力的时候了！"},
  {31,"死神：你很强……不过你以后会遇到更艰难的挑战， 你有兴趣挑战死亡之塔吗？"},
 };
 static readonly Dictionary<int,string> Death=new Dictionary<int,string>{
  {2,"死神：我是死神， 渴望和强者对决的人啊， 欢迎你来到死亡国度……"},
  {3,"死神：嗯， 表现马马虎虎， 上去吧。"},
  {4,"死神：注意了， 死亡在悄悄接近你。"},
  {5,"死神：看来你暂时死不了， 上去吧。"},
  {6,"死神：既然不死心， 那就陪我玩玩死亡游戏吧。"},
  {7,"死神：算你有点能耐， 你可以上去了。"},
  {8,"死神：死亡游戏开始了！"},
  {9,"死神：手脚还算利索， 上去吧。"},
  {10,"死神：去吧， 死亡的召唤开始了！"},
  {11,"死神：嗯哼， 表现还凑合， 上去吧。"},
  {12,"死神：这才只是热身而已， 受不了就放弃吧。"},
  {13,"死神：实力决定成败， 继续往上吧。"},
  {14,"死神：嗯， 游戏开始！"},
  {15,"死神：表现得不错， 继续往上吧。"},
  {16,"死神：准备好了吗？ 游戏开始了！"},
  {17,"死神：嗯哼， 第10层是个坎， 不想死的话就趁早回去。"},
  {18,"死神：想知道死亡的感觉吗？ 那就来试试吧！"},
  {19,"死神：嗯， 表现不错。 如果你们能通过下一层，我就让你们挑战第11层， 嗯呵呵……"},
  {20,"咯吱， 嘎吱！ 可恶啊啊啊！ 好疼啊！ 呜啊啊啊啊！"},
  {21,"抖抖抖……簌簌簌簌……"},
  {22,"死神：从这一层开始， 你的命只能靠你自己了， 嗯哼哼。"},
  {23,"死神：嗯， 做得还不错， 上去吧。"},
  {24,"死神：新的游戏开始了， 请你……慢慢享受吧！"},
  {25,"死神：嗯， 你的技巧不错， 可以继续往上走。"},
  {26,"死神：嗯哼， 新一轮的死亡正向你招手。"},
  {27,"死神：你可以上去， 不过别太勉强。"},
  {28,"死神：怎幺样？ 还想继续挑战吗？"},
  {29,"死神：嗯哼， 表现还凑合， 上去吧。"},
  {30,"死神：现在退出还来得及， 你考虑清楚了吗？"},
  {31,"死神：不错不错， 但别太小瞧你的对手。"},
  {32,"死神：嗯哼， 死亡游戏开始了！"},
  {33,"死神：嗯， 表现还不错， 上去吧。"},
  {34,"死神：越来越接近死亡了， 你还要继续吗？"},
  {35,"死神：实力决定成败， 继续往上吧。"},
  {36,"死神：嗯哼， 越来越有趣了， 开始吧。"},
  {37,"死神：既然有点能耐， 那就继续往上吧。"},
  {38,"死神：如果你遭遇不测， 我不会同情你， 还要继续吗？"},
  {39,"死神：我看见了恐怖的死亡， 你现在回头还来得及。"},
  {40,"抖抖抖……好冷啊。 还是先热热身吧……你是谁？ 嗯， 过来陪我热身吧！"},
  {41,"死神：这次表现不错， 他暂时感觉不到冷了。"},
  {42,"死神：是死是活， 就看你的表现了！"},
  {43,"死神：算你有点能耐， 你可以上去了。"},
  {44,"死神：这次， 你还能幸运的躲过吗？"},
  {45,"死神：别高兴得太早， 这次是你运气好而已。"},
  {46,"死神：怕了吗？ 死亡游戏才刚刚开始呢！"},
  {47,"死神：你的步伐凌乱了， 不能坚持就放弃吧！"},
  {48,"死神：从这里开始， 你得有面对死亡的心理准备！"},
  {49,"死神：嗯， 没想到你越战越勇， 有点看头！"},
  {50,"死神：这是专门为你准备的死亡之旅， 嗯哼！"},
  {51,"死神：看来我小瞧你了， 上去吧。"},
  {52,"死神：这次， 我倒要看看你怎幺躲开他们的攻击！"},
  {53,"死神：嗯呵呵， 亡灵们等你很久了， 上去吧。"},
  {54,"死神：让人兴奋的杀戮就要开始了， 很期待！"},
  {55,"死神：你还不想放弃？ 那就继续往上吧。"},
  {56,"死神：嗯哼， 你也感觉到死亡在逼近吧？"},
  {57,"死神：骄兵必败， 希望你记住！"},
  {58,"死神：恐惧让你无法前进了吗？ 嗯哼~ 好戏还在后头呢！"},
  {59,"死神：永不放弃就是你的回答吗？ 那就继续往上吧~"},
  {60,"呀哈！ 又来一个送死的。 <apc::10504>是呀， 那我们就再来一次吧。 <apc::20500>我们死后竟然留在这里……难道……“死不瞑目”就是说我们这种情况？ <apc::10504>管他呢！ 这不也是所谓的永恒的友情吗， 嘿嘿。 <apc::20500>让你久等了， 都放马过来吧！"},
  {61,"竟然输了！ 啊！ 这次肯定又是你出错技能了…… <apc::10504>既然我们拦不住你， 你可以往上了。"},
  {62,"死神：时间就是生命， 游戏开始！"},
  {63,"死神：有点手忙脚乱了吧？ 继续努力！"},
  {64,"死神：亡灵们等你很久了！"},
  {65,"死神：强者的世界只有赢没有输， 上去吧！"},
  {66,"死神：在这儿， 实力决定成败， 希望你记住！"},
  {67,"死神：嗯， 表现还不错， 上去吧。"},
  {68,"死神：在这里， 死亡才是主宰！"},
  {69,"死神：嗯哈哈， 有趣的家伙， 上去吧。"},
  {70,"死神：只有时刻面临死亡， 强者才能变得更强！"},
  {71,"死神：你不怕死亡？ 那就继续往上吧！"},
  {72,"死神：死亡游戏开始， 祝你好运！"},
  {73,"死神：以为你会输得很惨， 没想到……上去吧！"},
  {74,"死神：嗯哼哼， 希望你能坚持到最后一秒！"},
  {75,"死神：没想到你能躲过亡灵的攻击， 上去吧。"},
  {76,"死神：如果你在这里倒下， 也算是半个英雄！"},
  {77,"死神：你的实力超出我的预料了， 上去吧！"},
  {78,"死神：在这里， 喘口气的时间都是宝贵的！"},
  {79,"死神：表现不错， 你可以休息下再上去！"},
  {80,"啊哈哈哈哈！ 挑战者还真是源源不断呢。 那我就继续燃烧吧， 嘿嘿嘿。"},
  {81,"嗯哈哈， 全都烧毁了， 只剩下白茫茫的灰烬……"},
  {82,"死神：他们等你很久了， 快开始吧！"},
  {83,"死神：到了这儿， 你只能继续往上了！"},
  {84,"死神：在这里， 死亡才是主宰！"},
  {85,"死神：你越来越勇猛， 上去吧。"},
  {86,"死神：这里越来越危险了， 你好自为之吧！"},
  {87,"死神：嗯， 我要对你刮目相看了， 上去吧。"},
  {88,"死神：在这里， 生与死已经没有区别了！"},
  {89,"死神：你已经向我证明了你的强大， 上去吧。"},
  {90,"死神：你的气势很强， 希望一会还能看见你活着！"},
  {91,"死神：你很强， 但是这里不缺更强的挑战者。 不想堕落就继续修炼吧， 等待更强的挑战才是唯一的出路。"},
 };
 internal static string Line(ushort dungeon,int stage,bool complete){
  Dictionary<int,string> table=dungeon==Offline70TowerRunFG348.IllusionTower?Illusion:dungeon==Offline70TowerRunFG348.DeathTower?Death:null;
  if(table==null||stage<1||stage>45)return null;
  string text;return table.TryGetValue(stage*2+(complete?1:0),out text)?text:null;
 }
 // op12 body {type u8, uid u16, serverGroup u8, len i32, GBK text}; type 0 renders as the
 // senderless system line. Live 2026-10-28: this CN70 client DROPS op12 lines whose uid is 0 -
 // the line only renders (in the detached DNF_WND_CHAT system tab) when uid carries the
 // receiving role's unique_id, so the body must be built per role.
 internal static byte[] Body(ushort uid,string text){
  if(String.IsNullOrEmpty(text))throw new InvalidDataException("Tower dialog text");
  byte[] encoded;try{encoded=TextEncoding.GetBytes(text);}catch(EncoderFallbackException){throw new InvalidDataException("Tower dialog encoding");}
  if(encoded.Length>600)throw new InvalidDataException("Tower dialog length");
  using(MemoryStream m=new MemoryStream())using(BinaryWriter w=new BinaryWriter(m)){w.Write((byte)0);w.Write(uid);w.Write((byte)0);w.Write((uint)encoded.Length);w.Write(encoded);return m.ToArray();}
 }
}
partial class Offline70BatchServiceFG {
 int towerDialogStartFloorFG363=-1,towerDialogCompleteFloorFG363=-1;
 // One "on start" line per floor after the load, one "on complete" line when the floor clears;
 // several killers can hit the clear hook, so the floor number is the write-once receipt.
 // Diagnostic switch: the op12 death-god line is the one server action present in EVERY tower
 // crash window (2026-10-29 06:42/07:00/07:10/07:22/07:31 all crash 3-6s after it, and the two
 // early sessions that "survived" left within 2s). The crash is a virtual call on a freed object
 // inside a per-frame object list, which is also what a chat line's UI object would be, so the line
 // now has to prove it is not the trigger. Flip back to true to restore the chat line.
 internal const bool ChatLineFG363=false;
 void TowerDialogFG363(bool complete){
  if(!TowerActiveFG348)return;
  int floor=towerFG348.Floor;
  if(complete){if(towerDialogCompleteFloorFG363==floor)return;towerDialogCompleteFloorFG363=floor;}
  else{if(towerDialogStartFloorFG363==floor)return;towerDialogStartFloorFG363=floor;}
  string text=Offline70TowerDialogFG363.Line(towerFG348.DungeonId,floor,complete);
  if(text==null)return;
  if(!ChatLineFG363){Note("TOWER_DIALOG_SUPPRESSED run="+dungeon.RunId+" floor="+floor+" phase="+(complete?"complete":"start")+" text="+text);return;}
  Send("tower-dialog-"+floor+(complete?"c":"s"),Offline70RoleProtocol.BatchPacketFG(0,12,Offline70TowerDialogFG363.Body(role.Id,text),keys,false));
  Note("TOWER_DIALOG run="+dungeon.RunId+" floor="+floor+" phase="+(complete?"complete":"start")+" text="+text);
 }
}
