using UnityEngine;
using Verse;

namespace NewThreeKingdomsArt
{
    /// <summary>
    /// Mod 入口：注册设置界面和 Harmony 补丁
    /// </summary>
    public class NewThreeKingdomsArtMod : Mod
    {
        public static NewThreeKingdomsArtSettings Settings;
        private string weightBuf = "1";
        // 构造函数
        public NewThreeKingdomsArtMod(ModContentPack content) : base(content)
        {
            Settings = GetSettings<NewThreeKingdomsArtSettings>();
        }

        /// <summary>
        /// 设置界面：权重输入 + 概率计算显示
        /// </summary>
        public override void DoSettingsWindowContents(Rect inRect)
        {
            var listing = new Listing_Standard();
            listing.Begin(inRect);

            listing.Gap(12f);

            // 权重输入
            listing.Label("新三国三艺术入口权重 (p值):");
            listing.Gap(4f);

            // 初始化缓冲区
            if (weightBuf == "1" && Settings.memeWeight != 1f)
                weightBuf = Settings.memeWeight.ToString("F2");

            Rect inputRect = listing.GetRect(30f);
            var newBuf = Widgets.TextField(inputRect, weightBuf);
            if (newBuf != weightBuf)
            {
                weightBuf = newBuf;
                if (float.TryParse(weightBuf, out float val) && val >= 0f)
                {
                    Settings.memeWeight = val;
                    if (Current.Game != null)
                        MemeWeightApplier.Apply();
                }
            }

            listing.Gap(8f);

            // 概率计算
            float p = Settings.memeWeight;
            if (p <= 0f)
            {
                listing.Label("触发概率: 0%（已禁用）");
            }
            else
            {
                // Harmony Prefix 在 Taleless 分支中注入 1 条 p=X 的入口规则
                // 与 Root 的 p=1 规则竞争，实际概率 = X / (1 + X)
                // 例：p=1→50% | p=4→80% | p=99→99%
                float rate = p / (1f + p);
                listing.Label("触发概率: " + rate.ToString("P1") + "  (p=" + p.ToString("F2") + " / 1+" + p.ToString("F2") + ")");
            }

            listing.Gap(4f);

            // 快捷预设按钮
            listing.Label("快捷预设:");
            Rect btnRow = listing.GetRect(32f);
            float x = btnRow.x;
            float w = btnRow.width / 5f;
            if (Widgets.ButtonText(new Rect(x, btnRow.y, w - 4f, btnRow.height), "禁用"))
            {
                Settings.memeWeight = 0f; weightBuf = "0.00";
                if (Current.Game != null) MemeWeightApplier.Apply();
            }
            if (Widgets.ButtonText(new Rect(x + w, btnRow.y, w - 4f, btnRow.height), "0.25"))
            {
                Settings.memeWeight = 0.25f; weightBuf = "0.25";
                if (Current.Game != null) MemeWeightApplier.Apply();
            }
            if (Widgets.ButtonText(new Rect(x + w * 2, btnRow.y, w - 4f, btnRow.height), "1"))
            {
                Settings.memeWeight = 1f; weightBuf = "1.00";
                if (Current.Game != null) MemeWeightApplier.Apply();
            }
            if (Widgets.ButtonText(new Rect(x + w * 3, btnRow.y, w - 4f, btnRow.height), "4"))
            {
                Settings.memeWeight = 4f; weightBuf = "4.00";
                if (Current.Game != null) MemeWeightApplier.Apply();
            }
            if (Widgets.ButtonText(new Rect(x + w * 4, btnRow.y, w - 4f, btnRow.height), "99"))
            {
                Settings.memeWeight = 99f; weightBuf = "99.00";
                if (Current.Game != null) MemeWeightApplier.Apply();
            }

            listing.Gap(8f);
            listing.Label("公式: 概率 = p / (原版权重合计 + p)");
            listing.Label("(若有其他mod注入同名规则，分母会相应增大)");
            listing.End();
            base.DoSettingsWindowContents(inRect);
        }

        public override string SettingsCategory()
        {
            return "新三国艺术";
        }
    }

    /// <summary>
    /// 持久化设置
    /// </summary>
    public class NewThreeKingdomsArtSettings : ModSettings
    {
        /// <summary>新三国三艺术入口权重 p 值，默认 1（≈50%）</summary>
        public float memeWeight = 1f;

        public override void ExposeData()
        {
            Scribe_Values.Look(ref memeWeight, "memeWeight", 1f);
        }
    }
}
