using System;
// User requested test rates. Stored balances are never multiplied on load.
// Disposable old regressions explicitly select the original profile.
class Offline70TestRatesFG114 {
 internal static readonly int Multiplier=Environment.GetEnvironmentVariable("DNF70_RATE_PROFILE")=="original"?1:3;
 internal static uint Experience(uint earned){return checked(earned*(uint)Multiplier);}
 internal static uint Movement(uint original){return checked(original*(uint)Multiplier);}
}
