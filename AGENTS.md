# 工作区指令

## 语言偏好（用户明确要求，跨会话持续有效）

- **推理过程（thinking / 思考）一律使用中文。** 这是硬性偏好，不是建议。
- 面向用户的回复使用中文。
- 代码、命令、路径、标识符、报错原文、以及引用外部文档的原文保持原样，不要翻译。

用户原话：「思考过程换成中文，并且记住，重启也一样」。

## Unity

本工作区用于 Unity × DSH 集成。

- 工程 `HelloWorldProject/`（Universal 2D 模板，仅 `Assets/Scenes/SampleScene.unity`）；Editor `6000.6.5f1`；`com.unity.pipeline` 已随工程装好。
- 官方 `unity-cli` skill 在 `.dsh/skills/unity-cli/`（SKILL.md + 11 个 references）。驱动 Unity 前先读它。
- 核心纪律：改场景/预制体/资源前先跑 `unity status`；绝不手改 `.unity`/`.prefab`/`.asset` 的 YAML。
- 判断 Editor 死活看 `unity status` 的 `state`（`ready` 才算连上），**不要**信 `unity pipeline list` 的 `isRunning`——它会误报 true。
- `unity commands --grep X` 搜的是 **CLI 自己的**命令；Editor 暴露的命令要用 `unity command --query X` 或 `--tag <tag>`。全部工具见 `unity list --format json`（本工程 160 个）。
- 调用格式：`unity command <name> --key value`，参数名取自该命令的 schema。
- `recompile` 报 `up_to_date`（"No scripts needed recompilation"）**不代表**外部新写入的 .cs 没被编译——别据此下结论。判据是 `find_assets --name <类名>` 看有没有生成 MonoScript，或直接调 `attach_script`（类型未编译时返回可恢复错误）。想更稳，用 Editor 自己的 `write_text_file` 写文件（写完自动 import）。
- **传 JSON 参数给 Editor 命令：用 PowerShell 变量，不要在单引号里写 `\"`。** 本工作区的命令走 `pwsh -Command`（当前是 pwsh 7，`FullLanguage`），**不是** PS 5.1，两者引号行为不同：pwsh 7 会把单引号内的 `\"` 原样传给 CLI，CLI 收到 `{\"member\":...}` 并报 `--condition 需要 WaitConditionInput`（实测失败）。可靠写法是先把 JSON 放进变量，再作为单个参数传：
  ```powershell
  $cond = '{"member":"UnityEngine.Time.frameCount","op":"changed"}'
  unity command wait_for -- --condition $cond --timeout_s 10
  ```
  同理 pwsh 7 下单引号内的**裸**双引号字面量是安全的，`eval` 的 C# 可以直接写 `return "x=" + y;`，不必再回避字符串字面量（那是 PS 5.1 才有的破坏）。
- **`--` 之后的参数属于 Editor 命令，会被它吞掉，CLI 自己的 flag 要放在 `--` 之前。** 例如 `unity command eval -- --code 'return 1;' --format json` 报「eval 没有参数 --format」——`--format` 得写成 `unity command --format json eval -- --code '...'`（或直接省略）。
- 验证运行中的游戏走 skill 的 `references/playmode-verification-loop.md` 有序流程：`set_autotick` →（读/设 `runInBackground`）→ `editor_play` → `status --until-ready` → `wait_for` 帧计数 → `capture_game_view` → `console` → `editor_stop` → 恢复 `runInBackground`。注意本工程 `editor_play` **没有** `run_in_background` 参数，得自己设并自己恢复。

## 环境注意事项

- 默认 `workspace-write` 沙箱下，任何 pwsh 子进程都以 `0xC0000142`（STATUS_DLL_INIT_FAILED）退出，无法执行命令；切到 `danger-full-access` 后正常。需要跑命令时留意这一点。
