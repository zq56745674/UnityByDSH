# _art/ 目录索引

这个目录统一存放**素材**与**一次性验证产物**，放在 Unity 工程外，避免被 AssetDatabase 导入。

约定：一次性产物一律放 `_art/<主题>/`，并在 `.gitignore` 里忽略；只有需要长期随仓库分发的源图、提示词与处理脚本放在根目录。

| 子目录 | 内容 | 版本控制 |
|---|---|---|
| `tmp-verify/` | 探针脚本、UI 层级 dump、PlayerPrefs 备份、NUnit 报告、提交信息草稿、一次性验证截图 | 忽略 |
| `ui-audit/` | 2026-10-22~23 逐面板 UI 审计截图（见 `docs/roadmap.md` 第 4 节） | 忽略 |
| `topn/` | 无尽 Top-5 历史榜与第 5 层 BOSS 前商店的实机截图（见 `docs/endless-mode.md` §6.5） | 忽略 |
| `sprites/` | 精灵/特效概念图、切图中间产物（见 `docs/sprites-and-fx.md`） | 忽略 |
| `stage/` | 战斗舞台实机验证截图 | 忽略 |
| `fish/` | 早期从 GitHub 素材库下载的候选立绘调研图 | 忽略 |

根目录（部分跟踪）：主角立绘源图（`whale_girl_stand_alpha.png`、`ref_dafeiyu_sheet.png`）、生成提示词 `gen_prompt.txt`、图像处理脚本（`clean_hero.py`、`prep_v2.py` 等）。被淘汰的 `whale_girl_v3*` / `hero_v2*` 与 `*.bak` 已在 `.gitignore` 中排除。

> 2026-10-25（roadmap E4）：原先在仓库根的 `_tmp_verify/` 已整体迁入 `tmp-verify/`，脚本与文档引用同步更新。
