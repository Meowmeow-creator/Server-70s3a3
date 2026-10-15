# rednf Infrastructure Feature Gap Analysis

> Date: 2026-10-15 | Baseline: Server-70s3a3 FG175 (C#, 145 files, single-process local)
> Reference: rednf (C++, 14 services, MySQL, Docker, multi-process)

## Architecture Context

rednf is a 14-service C++ reimplementation (COServer, DBMW, Guild, Manager, Monitor, Statics, Relay, ChannelOld, Community, StunServer, secsvr trio, AuctionServer) targeting byte-equivalence with original DNF binaries. Our server is a single-process C# local server using JSON persistence. Many rednf features are architecturally out of scope (marked n-a) but listed for completeness.

## Gap Table (sorted by value, desc)

| # | Feature | rednf evidence (file:line) | our status | value | fixable offline? | notes |
|---|---------|---------------------------|------------|-------|-------------------|-------|
| 1 | **DB save/load layer + schema** | `DBMW/DBManager.h:1-278` (100+ query methods); `DBMW/DBManager.cpp` (7262 lines); `DBMW/DNFMySql.cpp:337-380` (MySQL connect w/ retry) | partial: `Batch-Service-FG.cs` (JSON economy state, no DB abstraction) | high | YES — schema/field design portable | rednf has 17 DB handles, 100+ typed queries, connection pooling. Our JSON approach works for local but lacks query layer. Schema field naming (characNo, m_id, guildId) could inform our data model. |
| 2 | **Packet dispatch framework (ID-based)** | `COServer/DNFPacketDecoder.cpp:26-37` (handler table m_handlers[0x27fd]); `DBMW/DNFPacketTranslater.cpp` (3445 lines); `Game/Dispatchers.cpp` (6086 lines); `Game/InterDispatcher.cpp` (6615 lines) | absent | high | YES — dispatch table pattern directly applicable | rednf uses a function-pointer dispatch table indexed by packet ID (1000..0x27fc). We handle packets ad-hoc in Batch-Service. A dispatch table would simplify adding new opcodes. |
| 3 | **Signal handling / crash recovery** | `COServer/DNFSignalTranslator.cpp:65-123` (13 signals: SIGTERM, SIGSEGV, SIGFPE, SIGBUS, SIGILL, SIGUSR1/2, etc.); `COServer/DNFSignal.h` (5 handler classes) | absent | high | YES — signal handling is deterministic | rednf catches SIGSEGV/SIGFPE/SIGBUS and runs graceful shutdown (flush DB, notify peers). We crash with no cleanup. |
| 4 | **Timer / event scheduling system** | `Game/TimerDispatcher.cpp` (1411 lines); `Game/TimerDispatcher.h` (1803 lines, TimerCheckConn, TimerDBUpdate, TimerStamina, TimerMapLoading, TimerPvpRankRes) | absent | high | YES — timer queue logic is verifiable | rednf has a full timer dispatcher with priority queue, timer registration, and callback dispatch. We have only a Stopwatch for card timer. Fatigue reset, stamina, DB auto-save all need timers. |
| 5 | **Crypto: RC6 + Anubis + MD5/SHA** | `Game/CNCryptoAlgo.cpp` (4651 lines, RC6 variant + Anubis); `Game/ChecksumCrypto.cpp` (2452 lines, MD5/SHA256/384/512); `DBMW/TEA.cpp` (210 lines) | partial: `Area-Cipher.cs`, `Gold-Cipher-Constants.cs`, `Flow-Cipher-Constants-FG.cs`, `Select-Cipher-Constants.cs`, `Role-Crypto-Constants.cs` (fixed offline keys only) | high | YES — algorithm implementations are deterministic | rednf has full RC6 (ORIG variant), Anubis, MD5, SHA family, TEA. We have area/gold/flow/select ciphers with hardcoded 334-byte keys. If client crypto changes, we'd need these. |
| 6 | **Guild system (DB + logic)** | `Guild/DNFGuild.cpp` (2980 lines); `Guild/DNFGuildManager.cpp` (893 lines); `Guild/DNFGuildWar.cpp` (421 lines); `Guild/GuildCargo.cpp` (606 lines); `Guild/PowerManager.cpp` (845 lines) | absent | mid | YES — guild logic is protocol-verifiable | Full guild CRUD, guild war, guild cargo/storage, power management. We have zero guild code. |
| 7 | **Community / friends / buddy** | `Community/BuddyManager.cpp`; `Community/CommunityServer.cpp`; `Community/SessionManager.cpp`; `Community/UserManager.cpp`; `Monitor/DNFBuddy.cpp`; `Monitor/DNFBuddyHandle.cpp`; `Monitor/BuddyRegisterManager.cpp` | absent | mid | YES — buddy protocol is deterministic | Buddy/friends system with registration, session management, user management. We have nothing. |
| 8 | **Auction server + bot + PVF parser** | `ServerLab/Auction/AuctionServer/auction_source/HandlerFor_DB_.cpp`; `auction_bot/auction_bot/pvf.py` (60 lines, full PVF parser); `auction_bot/auction_bot/protocol.py`; `auction_bot/auction_bot/bot.py`; `auction_bot/auction_bot/engine.py` | absent | mid | YES — PVF parser + protocol are deterministic | Complete auction server (C++) + Python bot with PVF parsing, pricing engine, web console, random option generator. We have nothing. PVF parser alone is valuable for static data. |
| 9 | **Relay / channel server protocol** | `Relay/DNFRelayServer.cpp`; `Relay/RelayService.cpp`; `Relay/Reactor.cpp`; `Relay/Socket.cpp`; `Relay/SocketSystem.cpp`; `ChannelOld/DNFChannelBridge/ChannelService.cpp`; `ChannelOld/DNFChannelBridge/Socket.cpp` | absent | mid | YES — protocol is deterministic | Relay server with reactor pattern, channel bridge with socket system, script support. We have a single TCP listener. For local play this is n-a but protocol knowledge is valuable. |
| 10 | **Logging system** | `ServerCommon/DNFFileLog.cpp`; `ServerCommon/DNFFileLog.h`; `ServerCommon/CFileLogWriterBase.cpp`; `ChannelOld/DNFChannelBridge/DebugLog.h` | partial: `Batch-Service-FG.cs:15` (evidence writer only) | mid | YES — logging is deterministic | rednf has multi-level file logging with scopes, log files per subsystem (Decoder.log, Channel.log, User.log, Except.log, MysqlErr.log, etc.). We write evidence lines to a single file. |
| 11 | **Monitor / statistics / load checking** | `Statics/CubeStatistics.cpp`; `Statics/DNFDBServer.cpp`; `Statics/DNFInnerMsgHandler.cpp`; `DBMW/ServerLoadChecker.cpp` (291 lines); `DBMW/CMisc.cpp` | absent | mid | YES — statistics logic is deterministic | Cube upgrade statistics, server load checking, frame lag statistics, login/logout statistics. We have nothing. |
| 12 | **Server configuration (XML-based)** | `DBMW/ServerXml.cpp` (342 lines); `COServer/DNFServerConfig.cpp` (69 lines); `COServer/DNFAppConfig.cpp` (103 lines); `ChannelOld/DNFChannelBridge/System.cpp` | absent | mid | YES — config parsing is deterministic | XML-based server configuration with hot-reload. We hardcode everything. A config file would help tuning. |
| 13 | **Scripting engine (flex lexer)** | `Game/SlangFlexLexerEngine.cpp` (16246 lines); `Game/FlexLexerEngine.cpp` (2185 lines); `Game/STQuestScript.cpp` (1852 lines); `ChannelOld/DNFChannelBridge/Script.cpp`; `Relay/Script.cpp` | absent | mid | NO — script execution needs live gameplay to verify | Full flex/bison lexer + parser for game scripts (quest scripts, dungeon scripts, town scripts). We hardcode everything in C#. |
| 14 | **Packet name tables / tracing** | `DBMW/PacketNameTables.h` (1176 lines); `Game/PacketNameTables.cpp` (2118 lines); `COServer/DNFPacketTracer.cpp`; `DBMW/DNFPacketTracer.cpp` | absent | low | YES — name tables are static data | Human-readable packet name lookup for debugging. We log opcodes as numbers. |
| 15 | **Version checking** | `COServer/DNFVersion.cpp` (49 lines); `COServer/DNFVersion.h` (39 lines) | absent | low | YES — version check is deterministic | Client version validation before login. We accept any connection. |
| 16 | **Kill / ban user config** | `COServer/DNFKillUserConfig.cpp` (93 lines); `Monitor/BlackUser.cpp`; `DBMW/DBManager.h:22` (RegisterToBlackList) | absent | low | YES — ban logic is deterministic | Per-user kill/ban with DB persistence. We have nothing. |
| 17 | **User session manager** | `COServer/DNFUserManager.cpp` (65 lines); `COServer/DNFUser.h` (46 lines); `COServer/DNFUser1Sig.cpp`; `COServer/DNFUserInOutCounter.cpp`; `Community/SessionManager.cpp`; `Community/UserManager.cpp` | partial: `Batch-Service-FG.cs:6` (single session GUID) | low | YES — session logic is deterministic | Full user session management with login/logout/heartbeat, user in/out counters, double-login detection. We have a single-player session. |
| 18 | **Core dump handling** | `Community/CoreDump.cpp`; `Community/CoreDump.h` | absent | low | NO — core dump analysis needs crash reproduction | Core dump capture on crash for post-mortem debugging. |
| 19 | **UDP handler** | `COServer/DNFUdpHandler.cpp` (330 lines); `COServer/DNFUdpRecvBuffer.cpp` (113 lines); `COServer/DNFUdpRecvBuffer.h` (41 lines) | absent | low | YES — UDP protocol is deterministic | Separate UDP channel for server-to-server communication. We are TCP-only. |
| 20 | **AAS (Account Auth Service)** | `Game/AASHandler.cpp` | absent | low | NO — auth flow needs live server | Account authentication service handler. We have a simple login probe. |
| 21 | **STUN server** | `DNFServer/StunServer/` | absent | low | NO — NAT traversal needs live clients | STUN server for NAT traversal. N/A for local play. |
| 22 | **Reactor pattern event loop** | `Relay/Reactor.cpp`; `Relay/Reactor.h`; `ChannelOld/DNFChannelBridge/Reactor.cpp`; `ChannelOld/DNFChannelBridge/Reactor.inl` | absent | low | YES — reactor pattern is deterministic | Reactor-based event-driven I/O. We use synchronous TCP. |
| 23 | **Linux service management** | `Relay/LinuxService.cpp`; `ChannelOld/DNFChannelBridge/LinuxService.cpp`; `ChannelOld/DNFChannelBridge/LinuxService.h` | n-a (architecture) | low | n/a | Linux systemd service integration. We are Windows-only. |
| 24 | **secsvr (security agent trio)** | `secsvr/source/gunnersvr/`; `secsvr/source/zergsvr/`; `secsvr/source/secagent/`; `secsvr/source/gunnersvr/import/include/tencore/tencrypt/TenCrypt.h` | n-a (architecture) | low | n/a | Dedicated security/anti-cheat servers (gunnersvr, zergsvr, secagent) with TenCrypt encryption. Our single-process architecture doesn't need separate security services. |
| 25 | **Manager server** | `Manager/DNFManagerServerMain.cpp`; `Manager/DBManager.cpp`; `Manager/DNFMonitorServer.cpp`; `Manager/DNFSegmentationFaultSig.cpp` | n-a (architecture) | low | n/a | Central manager server coordinating all other services. We are single-process. |
| 26 | **DNFShared / RDAR script import** | `DNFShared/GameScript/RDARScriptAvatarColorInfo.cpp`; `DNFShared/GameScript/RDARScriptItemInfo.cpp` | absent | low | NO — RDAR format is version-specific | RDAR script file import at runtime. We hardcode data in C#. |
| 27 | **Exception handling framework** | `ChannelOld/DNFChannelBridge/Exception.cpp`; `ChannelOld/DNFChannelBridge/Exception.h`; `Relay/Exception.cpp`; `Relay/Exception.h` | absent | low | YES — exception patterns are deterministic | CDNFException with what() messages, try/catch in every handler. We use InvalidDataException in places. |
| 28 | **Process management** | `Community/ProcessManager.cpp`; `Community/ProcessManager.h` | absent | low | n/a | Process lifecycle management for multi-service coordination. We are single-process. |
| 29 | **Archive logging** | `Community/ArchiveLog.cpp`; `Community/ArchiveLog.h` | absent | low | YES — archiving is deterministic | Separate archive log for historical records. We have a single evidence log. |
| 30 | **Hot-reload game events** | `DBMW/DBManager.h:66` (QueryOnTimeEventIdx); `DBMW/DBManager.h:129` (QueryOnTimeEventIdxUpdate); `DBMW/DBManager.h:206` (insertServerGameEvent); `DBMW/DBManager.h:207` (updateServerGameEvent) | absent | low | YES — event scheduling is deterministic | Server-side game events (on-time events) with hot-reload from DB. We have nothing. |

## Top 5 Fix-First (marked with ***)

1. *** **DB save/load layer** — Portable schema design from rednf's 100+ typed queries. Even if we keep JSON, the field naming and relationship patterns would improve our data model. Fixable offline: YES.
2. *** **Packet dispatch framework** — A dispatch table indexed by packet ID would simplify adding new opcodes and reduce ad-hoc handling. Fixable offline: YES.
3. *** **Signal handling / crash recovery** — Graceful shutdown on SIGTERM/SIGSEGV with DB flush would prevent save corruption. Fixable offline: YES.
4. *** **Timer / event scheduling** — Needed for fatigue reset, stamina, DB auto-save, and many gameplay timers. Fixable offline: YES.
5. *** **Crypto: RC6 + Anubis + MD5/SHA** — If client crypto needs updating or if we need packet checksums, these implementations are ready. Fixable offline: YES.

## Not-Applicable (Architecture) List

- Linux service management (we are Windows-only)
- secsvr security agent trio (single-process doesn't need separate security services)
- Manager server (single-process doesn't need central coordinator)
- Process management (single-process)
- STUN server (local play, no NAT traversal needed)
- Multi-service Docker deployment (single-process)

## Unverified Leads

- `Game/CDataManager.h` (3993 lines) — likely contains data table structures; not read in detail
- `Game/GlobalData.cpp` (3335 lines) — likely contains global game state; not read in detail
- `DBMW/PacketNameTables.h` (1176 lines) — packet name lookup tables; not read in detail
- `ServerLab/Auction/AuctionServer/auction_source/HandlerFor_GP_JPN.cpp` — Japanese auction handler; not read in detail
- `auction_bot/auction_bot/randopt.py` — random option generator for fake purple equipment; not read in detail but referenced extensively in README
- `secsvr/source/gunnersvr/import/include/tencore/` — TenCrypt encryption library headers; not read in detail
