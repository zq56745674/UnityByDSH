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
