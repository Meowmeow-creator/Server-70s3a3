using System;
// User requested test rates. Stored balances are never multiplied on load.
// Disposable old regressions explicitly select the original profile.
class Offline70TestRatesFG114 {
 internal static readonly int Multiplier=Environment.GetEnvironmentVariable("DNF70_RATE_PROFILE")=="original"?1:3;
 internal static uint Experience(uint earned){return checked(earned*(uint)Multiplier);}
 // Movement is not an award: the client reads this u32 as the actor's raw walk speed
 // (_Additioal_info::m_moveSpeed, stat block +0x42), while the stat panel shows its own speed
 // percentage. Multiplying it by the XP test rate made dungeon movement 3x with a normal-looking
 // panel, so the [initial value] row is passed through unchanged.
 internal static uint Movement(uint original){return original;}
}
