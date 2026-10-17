using Verse;

namespace NewThreeKingdomsArt
{
    /// <summary>
    /// 游戏开始 / 读档后自动应用权重。
    /// 规则包 include 已改为 Patches/NewThreeKingdomsArt_ArtInclude.xml 在 Def 加载阶段注入，
    /// 不再依赖运行时 Harmony 补丁（1.7 CoreCLR 下对已 JIT 的方法打补丁不生效）。
    /// </summary>
    public class MemeGameComponent : GameComponent
    {
        public MemeGameComponent(Game game) { }

        /// <summary>新游戏初始化完成后应用权重（此时 Def 与派系均已就绪）。</summary>
        public override void StartedNewGame()
        {
            base.StartedNewGame();
            MemeWeightApplier.Apply();
        }

        /// <summary>读档完成后重新应用权重（存档可能来自旧的设置值）。</summary>
        public override void LoadedGame()
        {
            base.LoadedGame();
            MemeWeightApplier.Apply();
        }
    }
}
