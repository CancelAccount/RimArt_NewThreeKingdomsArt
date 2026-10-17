using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using HarmonyLib;
using Verse;
using Verse.Grammar;

namespace NewThreeKingdomsArt
{
    /// <summary>
    /// 运行时修改新三入口规则权重，并注入动态派系名称。
    /// 规则包 include 由 Patches/NewThreeKingdomsArt_ArtInclude.xml 在 Def 加载阶段注入，
    /// 本类不再负责补丁注册。
    /// </summary>
    public static class MemeWeightApplier
    {
        private const string NTKDefName = "NewThreeKingdomsArt_MemeArtRules";
        private const string NTKCubeDefName = "NewThreeKingdomsArt_CubeArtRules";
        private const string SharedRulesDefName = "NewThreeKingdomsArt_SharedRules";
        private const string Tag = "[新三艺术]";

        private static readonly Regex PWeightRegex = new Regex(@"\(p=[\d.]+\)");

        /// <summary>
        /// 应用当前设置：注入派系名、替换入口权重，并刷新被 include 的上游规则包缓存。
        /// </summary>
        public static void Apply()
        {
            float p = NewThreeKingdomsArtMod.Settings.memeWeight;
            Log.Message($"{Tag} Apply() p={p}（概率={p / (1f + p):P1}）");

            // 注入动态派系名称
            InjectFactionNames();

            // 替换入口权重（雕像描述是多条原版规则竞争，需按条数等比放大）
            int statueCompetitors = CountStatueCompetitorRules();
            float pStatue = p * Math.Max(1, statueCompetitors);
            ReplaceEntryWeight(NTKDefName, p, pStatue);
            ReplaceEntryWeight(NTKCubeDefName, p, pStatue);

            // 上游描述包的 rulesResolved 会把 include 链内联缓存，必须一并刷新
            RefreshIncludeCaches(p);

            Log.Message($"{Tag} Apply() 完成");
        }

        /// <summary>
        /// 从游戏存档中扫描所有可见派系名称，动态注入到 SharedRules 的 ntkmeme_faction 词池。
        /// </summary>
        private static void InjectFactionNames()
        {
            var sharedDef = DefDatabase<RulePackDef>.GetNamedSilentFail(SharedRulesDefName);
            if (sharedDef == null) return;

            var rulePack = Traverse.Create(sharedDef).Field<RulePack>("rulePack").Value;
            if (rulePack == null) return;

            var list = Traverse.Create(rulePack).Field<List<string>>("rulesStrings").Value;
            if (list == null) return;

            list.RemoveAll(s => s.StartsWith("ntkmeme_faction->"));

            int injected = 0;
            foreach (var faction in Find.FactionManager.AllFactionsListForReading)
            {
                if (faction.IsPlayer || faction.Hidden) continue;
                string name = faction.Name;
                if (string.IsNullOrEmpty(name)) continue;
                list.Add($"ntkmeme_faction->{name}");
                injected++;
            }

            Log.Message($"{Tag} 注入 {injected} 个派系名称到 ntkmeme_faction");
            ClearDefCaches(sharedDef, SharedRulesDefName);
        }

        /// <summary>
        /// 替换单个 Def 中 r_art_description / r_statue_description 的权重并清除缓存。
        /// 两个根关键字使用不同权重：雕像描述有多条原版规则竞争，入口需等比放大。
        /// </summary>
        private static void ReplaceEntryWeight(string defName, float pArt, float pStatue)
        {
            var def = DefDatabase<RulePackDef>.GetNamedSilentFail(defName);
            if (def == null) return;

            var rulePack = Traverse.Create(def).Field<RulePack>("rulePack").Value;
            if (rulePack == null) return;

            ReplaceInStrings(rulePack, "rulesStrings", pArt, pStatue);
            ReplaceInStrings(rulePack, "untranslatedRulesStrings", pArt, pStatue);

            ClearDefCaches(def, defName);
        }

        /// <summary>按入口关键字分别替换权重；非入口规则原样保留。</summary>
        private static void ReplaceInStrings(RulePack rp, string fieldName, float pArt, float pStatue)
        {
            var list = Traverse.Create(rp).Field<List<string>>(fieldName).Value;
            if (list == null) return;

            int modified = 0;
            for (int i = 0; i < list.Count; i++)
            {
                float weight;
                if (list[i].StartsWith("r_art_description"))
                {
                    weight = pArt;
                }
                else if (list[i].StartsWith("r_statue_description"))
                {
                    weight = pStatue;
                }
                else
                {
                    continue;
                }

                string oldStr = list[i];
                string newStr = PWeightRegex.Replace(oldStr, $"(p={weight})");
                if (oldStr != newStr)
                {
                    list[i] = newStr;
                    modified++;
                }
            }
            if (modified > 0)
                Log.Message($"{Tag}   [{fieldName}] 修改了 {modified} 条");
        }

        /// <summary>
        /// 统计原版 ArtDescription_Statue 中 r_statue_description 的规则条数。
        /// 雕像描述是「多选一」，把本模组入口权重放大相同倍数，
        /// 实际触发率才仍等于 UI 显示的 p/(1+p)。
        /// </summary>
        private static int CountStatueCompetitorRules()
        {
            var def = DefDatabase<RulePackDef>.GetNamedSilentFail("ArtDescription_Statue");
            if (def == null) return 1;

            var rulePack = Traverse.Create(def).Field<RulePack>("rulePack").Value;
            if (rulePack == null) return 1;

            var list = Traverse.Create(rulePack).Field<List<string>>("rulesStrings").Value;
            if (list == null) return 1;

            int count = 0;
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].StartsWith("r_statue_description")) count++;
            }
            return count > 0 ? count : 1;
        }

        /// <summary>
        /// 刷新所有 include 了本模组规则包的上游规则包缓存，并自检挂载是否生效。
        /// 原版 RulePack.Rules 会把 include 链内联缓存进 rulesResolved，
        /// 不刷新则继续使用改权重之前的旧规则对象。
        /// </summary>
        private static void RefreshIncludeCaches(float p)
        {
            var ours = new List<RulePackDef>();
            AddIfExists(ours, NTKDefName);
            AddIfExists(ours, NTKCubeDefName);
            AddIfExists(ours, SharedRulesDefName);
            if (ours.Count == 0) return;

            int refreshed = 0;
            int artMakers = 0;
            foreach (var def in DefDatabase<RulePackDef>.AllDefs)
            {
                if (ours.Contains(def)) continue;
                if (!IncludesAny(def, ours)) continue;

                ClearDefCaches(def, def.defName);
                refreshed++;
                if (def.defName.StartsWith("ArtDescription_")) artMakers++;
            }

            Log.Message($"{Tag} 刷新 {refreshed} 个上游规则包缓存（p={p}，其中艺术描述包 {artMakers} 个）");

            if (artMakers == 0)
            {
                Log.Error($"{Tag} ✗ 没有任何艺术描述包 include 新三规则包，"
                    + "Patches/NewThreeKingdomsArt_ArtInclude.xml 未生效，权重设置将无效");
            }
        }

        /// <summary>按 defName 取规则包并加入列表；不存在时跳过。</summary>
        private static void AddIfExists(List<RulePackDef> list, string defName)
        {
            var def = DefDatabase<RulePackDef>.GetNamedSilentFail(defName);
            if (def != null) list.Add(def);
        }

        /// <summary>判断规则包是否直接 include 了给定规则包之一（Def 级或 RulePack 级）。</summary>
        private static bool IncludesAny(RulePackDef def, List<RulePackDef> targets)
        {
            if (def.include != null)
            {
                for (int i = 0; i < def.include.Count; i++)
                {
                    if (def.include[i] != null && targets.Contains(def.include[i])) return true;
                }
            }

            var rp = Traverse.Create(def).Field<RulePack>("rulePack").Value;
            if (rp != null && rp.include != null)
            {
                for (int i = 0; i < rp.include.Count; i++)
                {
                    if (rp.include[i] != null && targets.Contains(rp.include[i])) return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 清除 RulePackDef 的所有缓存层。
        /// </summary>
        private static void ClearDefCaches(RulePackDef def, string defName)
        {
            Traverse.Create(def).Field<List<Rule>>("cachedRules").Value = null;
            Traverse.Create(def).Field<List<Rule>>("cachedUntranslatedRules").Value = null;

            var rp = Traverse.Create(def).Field<RulePack>("rulePack").Value;
            if (rp != null)
            {
                var rpT = Traverse.Create(rp);
                rpT.Field<List<Rule>>("rulesRaw").Value = null;
                rpT.Field<List<Rule>>("untranslatedRulesRaw").Value = null;
                rpT.Field<List<Rule>>("rulesResolved").Value = null;
                rpT.Field<List<Rule>>("untranslatedRulesResolved").Value = null;
            }
        }
    }
}
