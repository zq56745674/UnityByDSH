namespace AutoBattle
{
    /// <summary>
    /// 表现层专用随机流：命中音变调、飘字抖动、立绘眨眼、特效位移抖动都从这里取。
    /// 这些都不影响胜负，但以前和玩法共用 UnityEngine.Random——播一个命中音
    /// （GameAudio 里随机 pitch）就会多吃一个随机数，同种子的战斗直接走到另一条分支。
    /// 玩法随机（战斗判定 / 掉落 / 词缀 / 技能三选一）继续用 UnityEngine.Random。
    /// </summary>
    public static class GameRandom
    {
        private static readonly System.Random Rng = new System.Random(0x5EA17);

        /// <summary>[0,1) 的浮点，语义同 UnityEngine.Random.value。</summary>
        public static float Value => (float)Rng.NextDouble();

        public static float Range(float min, float max) => min + (float)Rng.NextDouble() * (max - min);
    }
}
