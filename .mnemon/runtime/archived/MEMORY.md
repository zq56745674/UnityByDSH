# MEMORY.md archive

Entries that left MEMORY.md at its limit while no Memory Space could take them, oldest first.

## 2026-10-17T12:32:15.974Z

Unity 工程 HelloWorldProject（E:\pytest\unitydsh1）的项目知识已沉淀为 23 份 Mnemon Documents，入口是《Unity AutoBattle 项目现状总览与接手指南》（id 42b8694e-f097-43db-9f58-9d606e02cfd1）——含代码结构、12 步路线图进度、关键设计约定、踩坑教训、专题文档索引与待办。在新会话里接手这个工程的开发前，先 mnemon_document_search 搜这份总览，不要仅凭源码重新推导架构（注释里只有局部理由，全局取舍在文档里）。
§
火山方舟（ARK）图像模型现状（2026-10-16 实测 /api/v3/models）：可用的是 doubao-seedream-5-0-pro-260628、doubao-seedream-5-0-flash-260915、doubao-seedream-4-0-20260415；doubao-seedream-5-0-260128 与 doubao-seedream-4-0-250828 标 Retiring 且调用报 404 InvalidEndpointOrModel.NotFound；doubao-seedream-3-0-t2i-250415、doubao-seededit-3-0-i2i-250628 已 Shutdown。dsh-image-gen 插件的出厂默认 seedreamModel 恰是已下线的 doubao-seedream-5-0-260128，故开箱即报 404，需改 C:\Users\Administrator\.dsh\profiles\desktop\cordis.patch.yml 的 seedreamModel（该文件 image-gen 块在第 80-88 行）。
§
dsh-image-gen（desktop profile）的 cordis.patch.yml 配置改动无需重启 DSH 即生效——实测在 DSH 主进程启动后修改 seedreamModel，下一次 edit_image 调用就用了新模型。判别"配置到底有没有被插件加载"的技巧：ARK 先校验模型名、后校验余额，所以不存在/已下线的模型名报 404 InvalidEndpointOrModel.NotFound，而模型名有效但账户欠费报 403 AccountOverdueError；插件报 403 即证明它已经在用新模型名（若仍用旧模型名会先撞 404）。
§
Unity 工作区根 E:\pytest\unitydsh1 已建 git 仓库（分支 main，首提交 334b345，README 提交 f2e4e5d），远程 origin https://github.com/zq56745674/UnityByDSH.git。仓库根取工作区根而非 HelloWorldProject/，因 _balance/、_art/、.mnemon/ 都是项目资产。.gitignore 排除 HelloWorldProject/{Library(1.5GB/34546 文件),Temp,Logs,UserSettings}、dsh-image-gen/、_art/fish/ 与废弃立绘；.gitattributes 用 * text=auto 并对 .unity/.prefab/.asset/.meta 与代码强制 eol=lf。这台机器推 GitHub 须给仓库配 http.sslBackend=schannel（否则报 unable to get local issuer certificate，企业网络证书链问题）与 credential.helper=manager（GCM 在 D:\Program Files\Git\mingw64\bin\，凭据已存 Windows 凭据管理器，此后推送免登录）。改代码前先 commit，便于回退。
§
平衡回归测试工具：HelloWorldProject/Assets/Editor/BalanceRegression.cs，基线 E:\pytest\unitydsh1\_balance\baseline.json（工程外避免被 Unity 导入）。关键设计：战斗随机全走 UnityEngine.Random，用 Random.InitState 固定种子让整轮模拟逐位可复现，胜率容差收到 0.5 个百分点；每个巅峰档用独立种子（DefaultSeed+档位下标）使改动定位到单档。分两层——静态哨兵（技能表/公式采样/关卡表/怪物样本/评分自洽性）约 1.2 秒走字符串精确比对，胜率表约 25 秒。入口 RunStaticOnly() / Run(false,trials,perType) / Run(true,...)，菜单 Tools/平衡回归/。探针 LateGameProbe.RunWinrateRow(...) 返回 float[] 供其复用。
§
mnemon 文档更新：`mnemon_document_manage(update)` 的返回值会带上全部 25 个文档的快照（约 20K tokens），代价很高。若只想改文档正文中一两行，可直接用 edit 改 `.mnemon/documents/active/<file>.md`（内容立即生效，search/read 都能读到新内容），代价是 index.json 里的 contentHash 变旧；下次任何一次正规模 update 会自动重算 hash。需要完整一致性时才用工具更新。

## 2026-10-21T07:29:16.585Z

HelloWorldProject 的日志有两条写入路径：GameController.AddLog 维护 TotalLogEntries/LogVersion，BattleSystem.Tick 直接往传入的 Log 里 Add（不计数），差值由 GameController.TickFighting 补记。以后新增往 Log 写的地方必须同步这两个计数器，否则飘字/实时 DPS 静默停摆。Mnemon 文档 42b8694e 中「2026-10-17 全项目审查（未修项）」那段已部分过时：暂停/继续、技能面板冻结、浮层穿透、选关面板重建、飘字/DPS 均已修，详见新文档 a6ea98c9（飘字/实时 DPS 修复与 UI 冒烟）。
§
2026-10-21 按用户明确指令提交并推送 54f8020「转生与成就系统：轮回、42 条成就、第 11 节回归与存档修复」（57 文件 +6946/-113），包含转生/成就、第 11 节回归 harness、v8 首通回填修复、文档与验收截图；此前 6cfecf3、7d0cd0d 已推送。当前 HEAD=origin/main=54f8020，工作区干净。commit/push 仍需事先明确许可。
§
验收 UI 的两条操作经验（2026-10-18）：Play 模式用反射改设置/面板后，必须等 400–500ms 让 Refresh 跑过一帧再读文本，否则会读到上一帧（技能池推荐分、对比面板 _compareDirty 都踩过）；cc-safety-net 会拦截 `git checkout -- <file>`（视为丢弃未提交改动），要恢复单个文件改用 Start-Process git -ArgumentList @('-C',$repo,'show','HEAD:path') -RedirectStandardOutput $dest。
§
HelloWorldProject 终局 11-10 结算语义（2026-10-18 修）：首通时正常发奖/横幅并立即把 Player.stageKillCount 归零；之后继续刷同一关，ResolveStageClear 里的 SuppressFinalStageFarmKill 会吞掉满轮击杀（不再每次多等 StageClearDelay=3s、也不再刷通关日志）。HandleMonsterKilled 早已拆成约 29 行调度（ResolveStageProgress / GrantKillReward / ResolveLevelUp / ResolveDrops / EnterRespawnWait），README 的「182 行未拆」旧条目已删；此次把 55 行的 ResolveStageProgress 再拆成 ResolveStageClear / SuppressFinalStageFarmKill / ShowClearBanner / AnnounceStageBossIfNext。
§
HelloWorldProject 的 AutoSaveTick（GameController.cs:1541-1547）每 AutoSaveInterval（默认 5 秒）无条件 Save 当前存档槽，Play 模式里改 Player 后逗留超过 5 秒就会覆盖磁盘存档；测试前应先备份或测试后立即恢复，2026-10-18 曾因此把 6 个技能槽清空，后按截图恢复。
§
本机 github.com / api.github.com 等被 Watt Toolkit（Steam++）写入 hosts 映射到 127.0.0.1；未开加速时 git fetch/push 报 Failed to connect port 443。push 前启动 `D:\Program Files\Steam++\Steam++.exe`，并用 `git -c http.schannelCheckRevoke=false push origin main`（本地 443 代理证书吊销检查失败），推送后可关闭 Steam++。2026-10-19 以此法推送 7d0cd0d 成功。
§
HelloWorldProject 的音效层：GameAudio.cs 用 16 路 AudioSource 播 Resources/Sfx 下 14 个 Kenney CC0 OGG，实际音量 = GameSettings.MasterVolume × SfxVolume，AudioSource.ignoreListenerPause=true，命中音 45ms 节流；设置面板有主音量/音效音量两组 10 格音量条与 ± 微调。逐文件来源见 docs/audio-credits.md。

## 2026-10-28T17:53:11.549Z

HelloWorldProject 的 BGM 是四首本地 MP3（let_me_go / big_fish_cat / embrace / sadness_water），放在 HelloWorldProject/Assets/Resources/Music/，源自 D:\QQMusic 的星落落oi；该目录已 .gitignore，换机/克隆需自备文件。GameMusic.cs 管自动播放、自选、列表/单曲循环与音量（主音量×MusicVolume），GameUI.Music.cs 是顶部音符面板，曲目表在 MusicCatalog；第四首是《我的悲伤是水做的（大肥鱼版）》，224.8s。
§
HelloWorldProject 战斗可复现性：BattleSystem.Tick 只用 UnityEngine.Random（无 Time/DateTime），同状态同种子逐帧完全一致（实测 262 帧 firstDiff=-1）。2026-10-22 已修掉「同一进程内第一次 Tick 多消耗一个随机数」：根因是命中音效 GameAudio.PlayHit 用 UnityEngine.Random 变调，现表现层（音频、UI 抖动）统一改用独立 System.Random（GameRandom.cs，种子 0x5EA17），玩法随机保持 UnityEngine.Random；修复后带音频冷启动也是 54/54/54 draws，写探针不再需要热身。存档 2026-10-22 起为 v11，新增 PlayerData.endlessTop（本档 Top-5 历史榜，只记阵亡/撤退结束的局）。
§
HelloWorldProject 战斗舞台美术约定（2026-10-22 定型）：怪物贴图 Sprites/Monsters/{族}_{melee,ranged,crit}.png（384×384），族顺序必须与 GameConfig.ChapterFamilyNames 一致——GameUI.Stage.cs 的 StageFamilyKeys = goblin/kobold/skeleton/ogre/orc/spider/undead/gargoyle/demon/abyss/dragon，第 4~11 章才不再复用前三族；Boss 用单独立绘 Sprites/Bosses/{族}_boss.png（512×512），不是小怪放大；光环 Sprites/Auras/{aura_elite 176,aura_boss 208,affix_drain|shield|poison 84} 宽、AuraSquash=0.42 压成地面椭圆、必须比角色 Image 先创建才在脚下；背景 Sprites/Backgrounds/bg_{cavern,wilds,crypt,citadel}.jpg（1616×664）按章映射 + BackdropTint 色温区分同图章节，缓存键带章号；15 个技能各有专属特效图（fx_iron_body / fx_berserk_heart / fx_lethal_strike / fx_swiftness / fx_giant_slayer / fx_undying / fx_crit_chain / fx_near_death 等）。
§
dsh-image-gen 的 generate_image 默认 provider 是 ComfyUI（本机没跑会报 Could not connect to ComfyUI at http://127.0.0.1:8188）；要出图必须显式传 provider:"seedream"，模型 doubao-seedream-5-0-pro-260628，落盘在工作区 dsh-image-gen/image-*.png。多角色同图只适合列间有真空白的怪物表，Boss 必须一张一只（对图硬切会把斧头/披风切断）。
§
本机 hosts（C:\Windows\System32\drivers\etc\hosts）把 github.com、api.github.com、raw.githubusercontent.com 等解析到 127.0.0.1，git fetch/push 直连会报 Failed to connect to github.com port 443。绕过（不改 hosts）：git -c http.curloptResolve=github.com:443:<当前解析IP> push origin main；2026-10-25 用 20.27.177.113 实测推送成功。IP 可用 Resolve-DnsName github.com -Server 1.1.1.1 取。
§
HelloWorldProject 技能获取（2026-10-26 定稿）：P10 选流派得 1 技能点，P20~P90 每 10 级 +1，共 9 点；技能面板自选本流派 6 专属 + 通用 3，另一流派不可选；随机三选一已删除。技能点=Clamp(paragon/10,0,9)-已掌握数，不单独存档，存档仍 v15；v14 旧档选流派后按巅峰发点。
§
HelloWorldProject UI 规则：深皮革底框上的文字只用 GameConfig.BtnText/BtnTextDim/BtnTextGold，未选中底框着色用 FrameTintDim；TextNormal/TextMuted 只给羊皮纸（深底上=隐形）。按钮文字居中统一走 UiFace.Pixels（Gain 0.35/Lift 0.6）。另：设计坐标=渲染-12px，grep 判据见 _shots/build16.py。
