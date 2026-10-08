using System;
using System.Text;
using HarmonyLib;
using Il2CppBrokenArrow.Client.Ecs.Decks_v2.UI;
using Il2CppBrokenArrow.DataBase.Models;
using Il2CppBrokenArrow.Shared.Ecs;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using MelonLoader;
using UnityEngine;
using UnityEngine.UI;

[assembly: MelonInfo(typeof(MLCountriesMod.Mod), "MLCountriesMod", "1.0.0", "Speer")]
[assembly: MelonGame]

namespace MLCountriesMod
{
    // Broken Arrow's Arsenal / hangar filter bar (HangarUnitFilter) builds one
    // flag + specialization-icon block (CountryFilterButtonsGroup) per country, but only up to
    // _maxCountryGroupsCount, which the prefab sets for the two vanilla countries.
    // This mod raises that cap to the number of countries in the loaded database and,
    // if the wider bar no longer fits, scales it down so it ends where the unit grid ends.
    // Made with Claude (Anthropic).
    public class Mod : MelonMod
    {
        internal static MelonLogger.Instance Log;
        internal static MelonPreferences_Entry<bool> FitBar;
        internal static MelonPreferences_Entry<float> MinScale;
        internal static MelonPreferences_Entry<bool> Verbose;

        public override void OnInitializeMelon()
        {
            Log = LoggerInstance;
            var cat = MelonPreferences.CreateCategory("MLCountriesMod");
            FitBar = cat.CreateEntry("FitBar", true,
                description: "Shrink the country bar when it is wider than the unit list below it.");
            MinScale = cat.CreateEntry("MinScale", 0.6f,
                description: "Smallest scale the bar may be shrunk to (0.3 - 1.0).");
            Verbose = cat.CreateEntry("Verbose", false,
                description: "Write every country, its specializations and the built bar to the log when the Arsenal opens (for troubleshooting).");
            // Harmony patches in this assembly are applied by MelonLoader automatically.
        }
    }

    static class CountryList
    {
        // Each country flag and each specialization icon takes one bit of the game's int filter mask.
        const int MaskBits = 31;
        static bool warnedNotLoaded, warnedBits;

        /// Returns how many filter blocks the bar needs and logs what the game will see.
        public static int CountAndReport(out int bitsNeeded)
        {
            bitsNeeded = 0;
            var db = DataBaseService._instance;
            if (db == null || !db.IsLoaded)
            {
                if (!warnedNotLoaded)
                {
                    warnedNotLoaded = true;
                    Mod.Log.Warning("Database not loaded yet; leaving the country limit as it is.");
                }
                return -1;
            }

            var countries = new Il2CppSystem.Collections.Generic.List<Countries>(db.GetAllCountries());
            var sb = new StringBuilder();
            int shown = 0;
            for (int i = 0; i < countries.Count; i++)
            {
                var c = countries[i];
                if (c == null) continue;
                var specs = new Il2CppSystem.Collections.Generic.List<Specializations>(db.GetSpecializationsByCountryId(c.Id));
                int specsInHangar = 0;
                for (int j = 0; j < specs.Count; j++)
                    if (specs[j] != null && specs[j].ShowInHangar) specsInHangar++;

                if (!c.Hidden)
                {
                    shown++;
                    bitsNeeded += 1 + specsInHangar;
                }

                sb.Append($"\n  [{c.Id}] {c.Name}  flag='{c.FlagFileName}'  specs in hangar={specsInHangar}/{specs.Count}" +
                          $"  hidden={c.Hidden}  content={c.ContentMembership}");
                if (c.Hidden) sb.Append("   <- Hidden=true: the game will not show this country");
                if (specsInHangar == 0) sb.Append("   <- no specialization has ShowInHangar=true");
            }

            if (Mod.Verbose.Value)
                Mod.Log.Msg($"Countries in database: {countries.Count} ({shown} visible){sb}");

            if (bitsNeeded > MaskBits && !warnedBits)
            {
                warnedBits = true;
                Mod.Log.Warning($"Visible countries + specs need {bitsNeeded} filter bits, but the game's filter " +
                                $"mask only has {MaskBits}. Filtering by the last countries will misbehave; hide some " +
                                "specializations (ShowInHangar=false) or countries to stay within the limit.");
            }
            return countries.Count;
        }
    }

    [HarmonyPatch(typeof(HangarUnitFilter), nameof(HangarUnitFilter.Initialize))]
    static class HangarUnitFilter_Initialize
    {
        static int lastLogged = -1;

        static void Prefix(HangarUnitFilter __instance)
        {
            try
            {
                int needed = CountryList.CountAndReport(out _);
                int current = __instance._maxCountryGroupsCount;
                if (needed > current)
                {
                    __instance._maxCountryGroupsCount = needed;
                    if (needed != lastLogged || Mod.Verbose.Value)
                    {
                        lastLogged = needed;
                        Mod.Log.Msg($"Country limit raised {current} -> {needed}.");
                    }
                }
            }
            catch (Exception e) { Mod.Log.Error("Raising the country limit failed: " + e); }
        }

        static void Postfix(HangarUnitFilter __instance)
        {
            try
            {
                var groups = __instance._countryFilterButtonsGroups;
                if (Mod.Verbose.Value && groups != null)
                {
                    var sb = new StringBuilder();
                    for (int i = 0; i < groups.Count; i++)
                    {
                        var g = groups[i];
                        if (g == null) continue;
                        sb.Append($"\n  #{i} '{g.gameObject.name}' flag bit=0x{g.FilterFlag:X} next bit={g.NextBitIndex}");
                    }
                    Mod.Log.Msg($"Arsenal country blocks built: {groups.Count}{sb}");
                }
                BarFit.Apply(__instance);
            }
            catch (Exception e) { Mod.Log.Error("Country bar check failed: " + e); }
        }
    }

    [HarmonyPatch(typeof(HangarUnitFilter), nameof(HangarUnitFilter.OnEnable))]
    static class HangarUnitFilter_OnEnable
    {
        static void Postfix(HangarUnitFilter __instance)
        {
            try { BarFit.Apply(__instance); }
            catch (Exception e) { Mod.Log.Error("Country bar fit failed: " + e); }
        }
    }

    static class BarFit
    {
        static readonly Il2CppStructArray<Vector3> corners = new Il2CppStructArray<Vector3>(4);

        public static void Apply(HangarUnitFilter filter)
        {
            if (!Mod.FitBar.Value) return;
            var container = filter._countryButtonsContainer;
            var rt = container != null ? container.TryCast<RectTransform>() : null;
            if (rt == null || !rt.gameObject.activeInHierarchy) return;

            rt.localScale = Vector3.one;
            LayoutRebuilder.ForceRebuildLayoutImmediate(rt);

            rt.GetWorldCorners(corners);
            float left = corners[0].x;

            // Right edge of the actual content (last active child).
            float contentRight = float.MinValue;
            for (int i = 0; i < rt.childCount; i++)
            {
                var child = rt.GetChild(i).TryCast<RectTransform>();
                if (child == null || !child.gameObject.activeSelf) continue;
                child.GetWorldCorners(corners);
                contentRight = Math.Max(contentRight, corners[2].x);
            }
            if (contentRight == float.MinValue) return;

            // Where the bar may end: the unit list's right edge, else the parent's.
            float limit = float.MaxValue;
            var scroll = filter._unitScroll;
            var scrollRt = scroll != null ? scroll.transform.TryCast<RectTransform>() : null;
            if (scrollRt != null) { scrollRt.GetWorldCorners(corners); limit = corners[2].x; }
            var parentRt = rt.parent != null ? rt.parent.TryCast<RectTransform>() : null;
            if (parentRt != null) { parentRt.GetWorldCorners(corners); limit = Math.Min(limit, corners[2].x); }
            if (limit == float.MaxValue || contentRight <= limit) return;

            float scale = (limit - left) / (contentRight - left);
            scale = Mathf.Clamp(scale, Mathf.Clamp(Mod.MinScale.Value, 0.3f, 1f), 1f);
            rt.localScale = new Vector3(scale, scale, 1f);

            // Keep the bar's left edge where it was, whatever its pivot.
            rt.GetWorldCorners(corners);
            rt.position += new Vector3(left - corners[0].x, 0f, 0f);

            if (Mod.Verbose.Value)
                Mod.Log.Msg($"Country bar was wider than the unit list; scaled to {scale:0.00}.");
        }
    }
}
