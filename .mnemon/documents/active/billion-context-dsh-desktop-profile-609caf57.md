---
id: "609caf57-6568-4386-8c0c-d99116057527"
title: "billion-context 上下文压缩插件：原理、DSH 原生接入与 desktop profile 生效实测"
description: "billion-context 0.1.195 在 desktop profile 生效的证据链、模型驱动分层压缩原理（T1–T3、nudgeGrowthTokens、前缀缓存友好）、dsh.bundle.patch.yml 对 DSH 的三处改动，以及本会话 acp_status 实测读数与存储/运维要点。"
status: "active"
created_at: "2026-10-12T16:57:51.394Z"
updated_at: "2026-10-12T16:57:51.394Z"
content_hash: "60c0042d34a7bb6c94dc3702789df7ead8138d1843bc26411976cf8a17569197"
source_paths:
  []
session_ids:
  - "05c25654-b701-4d63-baa8-3b15660cb543"
memory_body_ids:
  []
---

# billion-context 上下文压缩插件：原理、DSH 原生接入与 desktop profile 生效实测

结论：`billion-context 0.1.195 (f76ffe9a)` 已在本机 desktop profile 的 DSH 会话中**生效**，以**原生插件（native）**方式挂入宿主进程，不是代理/MITM 模式。本文基于实际读取的包内 `package.json`、`dsh.bundle.patch.yml`、`README.zh-CN.md` 与配置文档，以及会话内 `acp_status` 的真实读数。

## 1. 生效证据与安装位置

- 安装位置：`C:\Users\Administrator\.dsh\profiles\desktop\node_modules\billion-context`（**desktop profile 专有**，非全局安装；当前 GUI `127.0.0.1:19387` 走的正是 desktop profile）。
- 装载声明：`desktop\package.json` 与 `pnpm-lock.yaml` 中均有该依赖；`billion-context\dsh.bundle.patch.yml` 存在；发现记录见 `desktop\.dsh-market\discovery-compatibility-v1.json`。
- 运行时自证：`acp_status` 输出 `ACTIVE SURFACE: pack=default | host=billion-context 0.1.195 (f76ffe9a)`，并报出本会话 6 段可压缩区间（`m00001–m00014`、`m00026–m00324`、`m00337–m00393`、`m00397–m00646`、`m00652–m00662`、`m00665–m00755`），说明钩子已挂上并读到会话消息流。
- 会话内每条消息被包上 `mNNNNN` 引用标记，即该插件注入的元数据。

## 2. 是什么、解决什么

官方定位：小窗口（100K 上下文足够）、省 5 倍 token、单会话跑到十亿级 token。它架在**任意**编程助手与模型 API 之间，用 [acp-kernel](https://github.com/ranxianglei/acp-kernel) 压缩重写 Anthropic/OpenAI 流；压缩时机与范围**由模型决定**，不是硬截断。

针对宿主自带摘要器的三个毛病：一次性大压缩（质量抖动）、改写整段历史（打掉上游前缀缓存，省下的 token 不抵缓存 miss）、不可逆（细节永久丢失）。该插件改为**增量、可逆、对前缀缓存友好**。

## 3. 工作原理

**3.1 模型驱动 + 四个注入工具**
插件向对话注入 `compress` / `decompress` / `search_context` / `acp_status` 四个工具与一段「压缩哲学」提示词，模型在上下文增长时自行调用，代理在服务端执行折叠；被折叠区间在历史中保持摘要形态直到被恢复。

- `compress` 把一段消息范围折叠成详细摘要；
- `decompress` 需要精确细节时还原（默认只升一层 T1，`full: true` 才回到原始消息）；
- `search_context` 在压缩摘要 + 可见消息中做关键词检索；
- `acp_status` 给出上下文用量概览与仍可压缩的区间。

**3.2 分层蒸馏 T1 → T2 → T3**
配置项 `compress.tiers`（默认 `true`）：「T1→T3 分级蒸馏把折叠成本摊到多代」。T1 = 原始消息 → 详细摘要，T2/T3 依次再蒸馏。每层有独立触发阈值 `compress.tierNudgeTokens` `{ t1?, t2?, t3? }`（token 数 ≥1），未设置时从 `nudgeGrowthTokens` 派生（T1 = 步长，T2/T3 = 步长 × 1.5），可逐层钉死。

**3.3 触发按「增长量」而非「窗口位置」**
默认增长步长固定 50K token（内核 `nudge.growthFloor`，可用 `compress.nudgeGrowthTokens` 调整），不随窗口大小放大；仅「输入本身就超窗」时才走预检硬兜底。因此启动内容约 100K 时，第一次软压缩往往要到 ~200K 才发生。资格层（内核默认 45%）只管第 1 轮冷启动门票与 T2/T3 块数升级地板。

**3.4 前缀缓存友好（经济学关键）**
压缩**原位小范围写入**，历史前缀保持字节稳定，上游 prompt cache 仍能命中。经验值：健康会话前缀缓存命中 **95–97%**，**压缩本身成本 ≤2%**；持续低于该值用 `/acp-cache` 或 `GET /__bili/cache-report` 归因，常见原因依次为上游缓存 TTL 到期、切换模型、插件 bug、其他。

## 4. DSH 接入方式（`dsh.bundle.patch.yml` 的三处改动）

1. 插入 `bili-native` 条目，把插件本体挂进 DSH 进程；
2. **关闭宿主自带 `compaction-basic` 的 `auto`**，避免两套压缩互相打架；
3. 给 `llm-deepseek` / `llm-deepseek-account` **补上 `MALFORMED_RESPONSE` 重试码**——DSH 默认白名单漏了它，一次采样把工具参数 JSON 弄坏就会让整个回合失败且不重试。

安装通道（官方）：`bili dsh`（启动器，经 `--patch` 注入完整原生插件）或 `bili plugin install dsh` ≡ `dsh plugin --profile <name> add billion-context`。

## 5. 本会话实测读数（`acp_status`）

```
131.5K tool (43%) | 172.4K text (57%) | 0 summaries (0%)
BILLED INPUT: 324419 tok · est-view total 324535 tok · ratio 1.0×
Nudge: idle — growth 1194 < floor 22500, ready: T1 286480
STORAGE — storage: legacy (json only)
```

含义：尚未压过任何东西（`0 summaries`），计费输入 = 全量 324K；本轮仅增长 1194 token，远低于 22500 最小增长门槛，故判断「现在不值得压」；约到 286K 才 ready 做第一次 T1 压缩。已识别 6 段可压缩区间、合计约 286K（占当前上下文约 88%），其中最大两段为 `m00026–m00324`（112.3K）与 `m00397–m00646`（106.4K）。

## 6. 存储与运维要点

- 存储当前为旧版 JSON 后端。配置项 `storage.unified` = `legacy` | `shadow` | `full`（环境变量 `BILI_STORAGE_UNIFIED`；配置文件层 `{"storage":{"unified":"..."}}`，严格拼写）。`shadow` = 统一 SQLite 双写干跑 + digest 对账，可先验证一致性再切 `full`。
- 日志：`~/.local/state/billion-context/bili.log`（同时镜像 stderr，>10MB 轮转，每请求一行 `[acp-usage] round N input=X cached=Y (cache hit Z%)`）；会话状态：`~/.local/share/billion-context/`。
- 状态查询：`/acp`（当前会话 ACP 面板）、`/acp-cache [full]`（缓存报告）、`GET /__bili/status`、`GET /__bili/cache-report`、`GET /__bili/health`。
- **插件模式下「重启 bili」= 重启宿主**：内嵌进程无法自行退出，长期开着的宿主会继续跑启动时那份代码，默默错过自动更新；可开 `--auto-restart-on-update`（或配置 `autoRestartOnUpdate: true`，默认关）在安全点自重启。
- 自 v0.1.183 起 routine/recommended 版本更新提示默认静默，`/acp` 与 `acp_status` 只显示 `critical` 级更新；「已安装未重启」的常设线索是 Web UI 横幅、`/acp` 单行警告、`GET /__bili/status` 的 `stale`/`diskVersion`。

## 7. 相关文档

- `d3bf27fd-13bf-47e8-be39-e276c5659411` —— dsh-browser 桥插件在本机 profile 的 managed 安装与验证记录（同类「DSH 插件接入」记录，但对象不同）。
