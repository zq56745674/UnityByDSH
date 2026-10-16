---
id: "d3bf27fd-13bf-47e8-be39-e276c5659411"
title: "dsh-browser 浏览器桥插件：managed 安装、验证判据与 web/desktop profile 接入记录"
description: "dsh-browser（桥插件 + Chrome 扩展）在本机的 managed 安装全过程：pnpm/.npmrc/GitHub hosts 劫持绕行、install.ps1 本地 checkout 分支、桥端点验证，以及把桥接入 desktop profile（19387）的待定方案与回滚命令。"
status: "active"
created_at: "2026-10-12T12:57:15.169Z"
updated_at: "2026-10-12T12:57:15.169Z"
content_hash: "bf0683fb1e166372f4ea7886ec467b8424757a6e98fbebb11632f92f6c999575"
source_paths:
  []
session_ids:
  - "60a85e14-1beb-4f92-b3fc-568e068578bd"
memory_body_ids:
  []
---

# dsh-browser 浏览器桥插件：安装与 profile 接入记录

安装时间 2026-10-12，目标版本线 `0.2.0-rc.2`。结论：桥插件已以 **managed 模式**装好并端到端验证通过（桥真的起来并能应答），但**只注册在 `web` profile**；当前桌面版（`desktop` profile，端口 19387）里没有它。

## 1. 为什么不能走插件商店

- 该仓库（`github.com/Lum1104/dsh-browser`）是**桥插件 + Chrome 扩展一体的独立 pnpm workspace**，标准 `dsh plugin` 装不了，必须走官方 `scripts/install.ps1` / `install.sh`。
- npm 上无 scope 的 `dsh-browser`（`github.com/ben7am1n/dsh-browser`）是**另一个无关项目**，不要误装。
- 扩展不是包安装，需要手动在 `chrome://extensions` 用「加载已解压的扩展程序」加载宿主目录；installer 会打开该页并把路径放进剪贴板。

## 2. 安装前补的两项前置条件

- **系统原无 pnpm**：用 `npm i -g pnpm@11.7.0`（等于仓库 `packageManager` 声明版本）装到 `C:\Users\Administrator\AppData\Roaming\npm\pnpm.cmd`。该目录**不在默认 PATH**，跑需要 pnpm 的脚本时要临时把它加进 PATH。
- **GitHub 域名被 hosts 劫持到 127.0.0.1**（`github.com`、`raw.githubusercontent.com`、`api.github.com` 等一大串，由运行时在跑的 Steam++ 加速器造成，它监听 80/443）。`install.ps1` 第一步是 `Invoke-WebRequest https://github.com/.../main.zip`，走这条链路容易在证书上失败。**绕行做法**：用 gh-proxy 拉源码 zip，解压到托管目录 `~/.dsh/dsh-browser`（并放 `.managed-by-install-sh` 标记），再让 `install.ps1` 走「本地 checkout」分支。

## 3. npmrc 的临时改动（已还原）

workspace 的 `.npmrc` 把 `@deepseek-ai` 钉在 `registry.npmjs.org`，而该 workspace 依赖几乎全是 `@deepseek-ai/*`（版本线 `0.2.0-rc.2`）。

- 安装期间临时把该 scope 指向 `https://registry.npmmirror.com/`（已确认镜像上存在全部 `0.2.0-rc.2` 包），原文件备份为 `.npmrc.upstream`。
- **装完已还原**为上游原样（`@deepseek-ai:registry=https://registry.npmjs.org/`），并在 `.npmrc` 里保留说明注释。
- 其它有效配置：`auto-install-peers=false`、`strict-dep-builds=false`。

## 4. 安装结果与宿主注册

`install.ps1` 四步全部完成（`exit=0`）：

| 项目 | 位置 / 状态 |
|---|---|
| 桥插件源码（managed） | `~/.dsh/dsh-browser` |
| 构建产物 | `packages/browser/bridge-browser/lib/index.js`（约 88 KB）、`lib/client.js`、`lib/protocol.js`、`lib/invariant.js` |
| 宿主注册 | `~/.dsh/profiles/web/package.json`：dependency `"@yuxianglin/dsh-bridge-browser": "link:.dsh-browser-source"` + `dsh.profile.bundles` 同名条目 |
| 目录联接 | `.dsh-browser-source` → `~/.dsh/dsh-browser/packages/browser/bridge-browser`（Junction） |
| Chrome 扩展 dist | `~/.dsh/browser-extension`（manifest v3、扩展版本 0.1.4、`sidePanel`、`install-info.json` 为 `mode: managed`） |

插件包元数据要点：包名 `@yuxianglin/dsh-bridge-browser`（v0.0.7，private），peer 依赖全线锁 `0.2.0-rc.2`，通过 `dsh.bundle.patch = ./cordis.patch.yml` 注入 `web` profile。patch 内容注册 `id: bridge-browser`，`toolTimeoutMs: 90000`、`snapshotMaxChars: 32000`、`maxInteractiveItems: 60`，token 取 `process.env.DSH_EXT_TOKEN`，会话工作区取 `DSH_BROWSER_SESSION_WORKSPACE`（默认 `~/.dsh/browser-sessions`，置空则 Ungrouped）。安全注记（来自 patch 自身注释）：不要在不信任网络上跑 `dsh web --host 0.0.0.0`。

配置层用 `dsh --profile web --dump-config` 复核过：组合后的配置树里出现 `- id: bridge-browser`（退出码 0）。

## 5. 运行验证判据（已实测）

启动 `pnpm start`（等价 `dsh web`，端口 3080）后按 README 的成功判据验证：

```
GET http://127.0.0.1:3080/ext/bridge-config
→ {"wsUrl":"ws://127.0.0.1:3080/ext/bridge"}   ✅
```

同时 `~/.dsh/ext-bridge-token` 已生成（首次启动生成、chmod 0600；本地扩展自动发现，无需手填 token）。注意 `/`（Web UI）在无 token 时返回 401 属正常。

## 6. 与 desktop profile（19387）的差距 —— 待用户拍板

- 官方脚本**只注册 web profile**；`desktop` profile 的 bundles / node_modules 里没有该桥。
- 扩展的**自动探测端口仅 `3080 / 3081 / 3090 / 14389`**，**19387 不在其中**。Firefox 另需手动填 token（`moz-extension://` UUID 不构成认证）。

接入 desktop profile 预计两步：

1. 把桥注册进 desktop profile：建 Junction、`pnpm add -w link:...`、把 `@yuxianglin/dsh-bridge-browser` 加进 `dsh.profile.bundles`；改前先用 `--dump-config` 预检。
2. 重启 DSH Desktop，并在扩展面板设置里手动填 `http://127.0.0.1:19387`。

**回滚**：万一桌面版启动异常，删掉 desktop profile `package.json` 里那一行 bundle 即可恢复。

第 1 步会改动**正在运行**的 desktop profile 的 node_modules 与 bundle 清单，第 2 步会中断当前会话 —— 因此这两步**未经用户确认不得擅自执行**。

## 7. 相关既有文档（registry 与 pnpm 看门狗）

桌面插件市场安装失败/回滚问题见文档 id `e2fa1342-9e1f-4d73-9e5e-1ff64de9cf3e`（DSH Desktop 插件市场 pnpm 静默看门狗诊断）。其中关于「registry 走国内镜像以避开大包下载静默超时」的处置与本记录第 3 节的镜像做法是同一类手段，但场景不同（市场安装 vs 命令行 managed 安装）。
