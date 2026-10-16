---
id: "e4783751-09e9-4165-924d-f1028c2261c0"
title: "DSH image-gen 插件 seedream 通道：默认模型已下线、可用模型清单与透明底出图约束"
description: "本机 desktop profile 下 image-gen 插件走火山方舟 seedream 出图时踩到的三个坑与修法：默认模型 404、需配置的模型 ID、透明背景参数对输入 PNG 的要求，附一次性修配置的路径与工作区里的两个 Pillow 预处理/后处理脚本。"
status: "active"
created_at: "2026-10-15T18:18:34.584Z"
updated_at: "2026-10-15T18:18:34.584Z"
content_hash: "70c1426a9cb14a1e51fb432d1075711f9e464ce04fa579eda96cd908160e6577"
source_paths:
  - "_art/prep_ref.py"
  - "_art/finish_hero.py"
session_ids:
  - "4b1b03b3-e3d3-432d-bb1b-91f5e487cf90"
memory_body_ids:
  []
---

# DSH image-gen 插件 seedream 通道：模型下线与透明底约束

适用范围：DSH desktop profile（当前 GUI `127.0.0.1:19387` 走的就是它）、`edit_image` / 图像生成插件的「seedream」渠道（火山方舟 ARK）。以下结论均来自一次真实的立绘生成任务（鲸鱼娘 Q 版立绘，任务已完成），错误原文与实测尺寸照录。

## 1. 只有 seedream 配了密钥

分别调用 edit_image 时，只有 seedream 可用；其余渠道直接报缺 key（原文）：

- `Error: edit_image requires the DashScope API key; configure it in Settings > Plugins > Image generation.`
- `Error: edit_image requires the OpenAI API key; configure it in Settings > Plugins > Image generation.`
- `Error: edit_image requires the xAI Grok API key; configure it in Settings > Plugins > Image generation.`
- `Error: edit_image requires the 智谱 GLM API key; configure it in Settings > Plugins > Image generation.`

密钥不在插件配置里，而在凭据库 `C:\Users\Administrator\.dsh\.credentials.yaml`；该文件顶部是 `records:`（`client-connection/browser-session`、`deepseek-account-platform/*`），方舟的 key 以 `refs: ARK_API_KEY: <redacted>` 形式引用。定位靠 grep 变量名即可，**不要打印值**。

## 2. 默认 seedream 模型已下线（404 的根因）

- 插件默认的 `doubao-seedream-5-0-260128` 状态为 **Retiring（已下线）**，直接调用返回 404 `InvalidEndpointOrModel.NotFound`。
- 同一账号当前可用：`doubao-seedream-5-0-pro-260628`（用它可以出图，2K 档）、`doubao-seedream-5-0-flash-260915`。
- `doubao-seedream-4-0-250828` 同样处于 Retiring。
- 结论：「404 / 模型不存在」优先怀疑模型 ID 已下线，去方舟账号侧拉一次模型清单，而不是反复重试或怀疑密钥。

**一劳永逸的修法**：在 `C:\Users\Administrator\.dsh\profiles\desktop\cordis.patch.yml` 的 image-gen 配置块里把 `seedreamModel` 改成 `doubao-seedream-5-0-pro-260628`。（该改动未执行，属待办建议。）

## 3. 透明底出图的两个约束

插件配置了 `seedreamBackground: transparent`，带来两个连带条件：

1. **输出必然是透明底**（RGBA），要白底得自己合成。实测生成图为 1888×2192、alpha 极值 (0, 255)。
2. **输入图必须是「至少含一个透明像素的 PNG」**，否则 400，原文：
   `InvalidParameter` / `The parameter \`image\` specified in the request are not valid: transparent background requires a PNG input with at least one transparent pixel.`（`param: image`）

## 4. 实操流程（工作区脚本）

- 预处理 [_art/prep_ref.py](_art/prep_ref.py)：从多视图设定表中裁出**单个**角色正面（避免三视图互相干扰），并给图打上一个透明像素以满足第 3 节的输入要求。实测：源 (1448, 1086) → `_art/ref_hero_only.png` (560, 650)。
- 出图：seedream `doubao-seedream-5-0-pro-260628`（2K），插件另存到工作区 `dsh-image-gen/image-<hash>.png`（本次为 `dsh-image-gen/image-caca3b2b.png`）。
- 后处理 [_art/finish_hero.py](_art/finish_hero.py)：把 RGBA 合成到纯白底输出。校验方式值得复用——读 mode/size、`alpha extrema`、以及合成后四角像素是否为 `(255, 255, 255)`。本次产出 `_art/whale_girl_stand.png`（白底）与 `_art/whale_girl_stand_alpha.png`（透明底，导入 Unity 做 UI/Sprite 更合适）。

环境：Pillow 在本机运行时可用，路径 `C:\Users\Administrator\.dsh\dsh-runtimes\dsh-primary-runtime\dependencies\python\python.exe`（pythonDistributions: `Pillow 12.3.0`、`numpy 2.3.5`）。

## 5. 其他可复用教训

- 列举 `C:\Users\Administrator\.dsh` 时，对 `app.asar` 内的文件 grep 不可用（沙箱/打包限制），改用 `--include "*.json"` 之类限定范围，并优先查 `.credentials.yaml`、`profiles\desktop\cordis.patch.yml`、`storages\session_projcache`。
- 出图插件会把图同时挂到会话附件并落盘到工作区 `dsh-image-gen/`，回复用户时无需再去读取该图。
