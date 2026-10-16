---
id: "e2fa1342-9e1f-4d73-9e5e-1ff64de9cf3e"
title: "DSH Desktop 插件市场 pnpm「静默看门狗」导致安装被回滚：诊断与处置"
description: "DSH Desktop 插件市场用 pnpm 安装插件时，因宿主 10 分钟无输出看门狗杀进程并回滚失败，但 node_modules 残留孤儿包；含证据链、根因与两种绕过方案。"
status: "active"
created_at: "2026-10-12T12:30:35.585Z"
updated_at: "2026-10-12T12:30:35.585Z"
content_hash: "ce0b494f92c07a46c105338dd1e9bcfb04fafe9dd7e19157be048900ffab28e3"
source_paths:
  []
session_ids:
  - "da96600d-5709-45b8-8c21-1ad1f650387d"
memory_body_ids:
  []
---

# DSH Desktop 插件市场安装失败诊断（静默看门狗 + 回滚）

结论先行：插件"装不上"通常不是包本身的问题。安装往往已经走到一半甚至主体成功，随后 pnpm 长时间无输出，被宿主插件管理器的静默看门狗杀掉，市场按 `exit=1` 处理并回滚声明，于是商店永远显示未安装。

## 现象

- 市场反复提示安装失败，商店里目标插件显示未安装。
- profile 的 `package.json` 里既没有该 dependency，`dsh.profile.bundles` 里也没有它。
- 但 `node_modules/<scope>/<pkg>` 目录完整存在，且 `.modules.yaml` 里仍登记着该包 —— 孤儿包（orphan）。下次重装时 pnpm 会复用它，反而更快。

## 证据链（2026-10-12，profile: `C:\Users\Administrator\.dsh\profiles\desktop`）

| 时间 | 事件 | 来源 |
|---|---|---|
| 20:13:35 | `modsearch: pinned to the registry's latest, 5.10.6`，安装开始 | `.dsh-market\log.ndjson` |
| 20:13:38 | 子进程 11656 启动，命令行为 `DeepSeek Harness.exe --expose-internals …\pnpm.mjs add @liustack/modsearch@5.10.6`，父进程为 GUI 宿主 17488 | Win32_Process 查询 |
| 20:13:43 | pnpm 主体已完成：`dependencies: + @liustack/modsearch 5.10.6` / `Packages: +2 -18` / `Done in 5.7s` | `.plugin-manager\logs\operation-bfb5Ol\pnpm.log` |
| 紧接着 | 两条 `[WARN] GET https://registry.npmjs.org/@univerjs-pro/exchange-node-binding-darwin-arm64/… error (23). Will retry in 10 seconds.` | 同上 |
| 20:13:43 → 20:24:41 | pnpm 一行输出都没有（整整 10 分钟） | — |
| 20:24:41 | `dsh: pnpm printed nothing for 600000ms and was terminated`；市场记 `exit=1` | `pnpm.log` + `log.ndjson` |
| 回滚后 | `package.json` 从 592 → 555 字节，`@liustack/modsearch` 从 dependencies 消失；`dsh.profile.bundles` 里从来没有它 | 文件对比 |

## 根因（按贡献度）

1. **宿主的静默看门狗**：`@deepseek-ai/dsh-plugin-manager` 对 pnpm 有 600000ms（10 分钟）无输出即终止的规则，无宽限；市场在 `node_modules\dshmarket\lib\pnpm-compat.js` 里专门识别并翻译这条消息。
2. **每次 `add` 都要重算整棵树**：该 profile 装有 `dsh-univer-office`，带入 12 个 `@univerjs-pro/*` 平台二进制包（解包 8–38 MB，单个 tarball 13 MB+）。日志里的 `Packages: +2 -18` 就是在移出/重估这批包，随后又去 GET 它们（`error (23)` 重试）。重装这类插件最容易触发超时。
3. **官方 registry 慢**：实测同一时刻 `registry.npmjs.org` 下载 `exchange-node-binding-win32-x64-msvc` 13.7 MB 用 30.8s（466 KB/s）、`darwin-arm64` 13.0 MB 用 38.4s（354 KB/s），而 `modsearch` 自己只有 158 KB / 2.5s。国内镜像会把 `dist.tarball` 重写成自己的域名，速度是另一个量级。
4. **非 TTY 下 pnpm 下载大包不打进度**：于是"慢"直接变成"静默"，正好踩中看门狗。
5. **镜像未生效**：市场 `state.json` 里 `region: china`，但本次 pnpm 日志中的 tarball URL 全是 `registry.npmjs.org`；profile 目录没有 `.npmrc`，环境里也没有 `npm_config_registry`。说明安装路径上没有走到国内镜像。
6. **失败清理不彻底**：回滚撤销 `package.json` 的两行声明，但不删 `node_modules` 里的包目录 —— 即上面说的孤儿包，也是"看起来装了、商店说没装"的直接原因。

次要因素：市场在 agent 会话运行期间会拒绝安装（同一份 `log.ndjson` 里有 `install-blocked — refused while agents are running`），点安装的时机也可能被挡。

## 处置方案（按性价比）

1. **先让 registry 走国内镜像再点安装**：在 profile 目录放 `.npmrc`，写 `registry=https://registry.npmmirror.com/`（pnpm 会读项目级 `.npmrc`；当前无 `npm_config_registry` 覆盖）。同时核对商店设置里的"下载区域"是否真的生效。
2. **手动装一次绕过看门狗**，装完确认 bundle 注册：
   ```powershell
   cd $env:USERPROFILE\.dsh\profiles\desktop
   & "D:\Program Files\DSH\DeepSeek Harness.exe" --expose-internals "D:\Program Files\DSH\resources\runtime\pnpm\bin\pnpm.mjs" add @liustack/modsearch@5.10.6 --registry https://registry.npmmirror.com
   ```
   随后确认 `package.json` 的 `dsh.profile.bundles` 里有该包 —— `@liustack/modsearch` 的 dsh 清单（`dsh.bundle.patch` + `client.platform: web`）要求以 bundle 形式挂载，光有 dependency 不会被加载。
3. 换网络/挂代理重试是市场自身给出的提示，但根因是"大包 + 10 分钟静默"，镜像更稳。

两种方案都会改动用户 profile，动手前需先确认。

## 相关环境事实

- `DSH_HOME=C:\Users\Administrator\.dsh`，`DSH_PROFILE=desktop`，`DSH_PROFILE_DIR=C:\Users\Administrator\.dsh\profiles\desktop`，`DSH_WEB_URL=http://127.0.0.1:19387`。
- 市场区域路由定义在 `node_modules\dshmarket\lib\regions.js`：`NPM_CHINA = https://mirrors.cloud.tencent.com/npm`、`GITHUB_PROXY_CHINA = https://gh-proxy.com`。
- 该 profile 由 pnpm 管理（`pnpm-lock.yaml`，`lockfileVersion: '9.0'`，importer 为 `.`）。
- 已知不相关但易混淆的现象：`@deepseek-ai/dsh-plugin-manager` 在 `D:\Program Files\DSH\resources\app.asar\dsh\node_modules\` 下搜索不到（asar 内路径不可枚举），不代表宿主没有该模块。
