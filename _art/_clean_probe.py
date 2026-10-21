import shutil, io
p = r"E:\pytest\unitydsh1\HelloWorldProject\Assets\Editor\LateGameProbe.cs"
shutil.copyfile(p, r"E:\pytest\unitydsh1\_archive\LateGameProbe.pre-cleanup.cs.bak")
data = open(p, "r", encoding="utf-8", newline="").read()
lines = data.splitlines(keepends=True)
print("total", len(lines))
def check(n, expect):
    got = lines[n-1].strip()
    assert got == expect, (n, got, expect)
check(410, "/// <summary>")
check(411, "/// 装备评分合理性审计：逐词条算「每 1 点评分换来多少期望 DPS」。")
check(464, "")
check(465, "/// <summary>生成一套固定的 Lv.100 传说五件套，供各类技能审计复用（避免每次随机导致技能间差异）。</summary>")
check(481, "")
check(482, "/// <summary>")
check(483, "/// 理论期望 DPS。实现只有一份——\u0060EquipmentScore.Dps\u0060，")
check(674, "}")
check(675, "")
check(702, "")
check(703, "/// <summary>")
check(790, 'static string TimeText(float speed) => speed < 0f ? "战败" : (-speed).ToString("0.0") + "s";')
check(791, "")
check(792, "/// <summary>把一场战斗跑到分出胜负，含自动喝药（口径与游戏内一致）。</summary>")
for a, b in [(702, 790), (481, 674), (410, 464)]:
    del lines[a-1:b]
open(p, "w", encoding="utf-8", newline="").write("".join(lines))
print("after", len(lines))
