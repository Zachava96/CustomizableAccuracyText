using System;
using BepInEx;
using BepInEx.Logging;
using BepInEx.Configuration;
using HarmonyLib;
using Rhythm;
using TMPro;
using UnityEngine;

namespace CustomizableAccuracyText
{
    [BepInPlugin(PLUGIN_GUID, PLUGIN_NAME, PLUGIN_VERSION)]
    [BepInIncompatibility("net.zachava.showpreciseaccuracytext")]
    [BepInProcess("UNBEATABLE.exe")]
    public class CustomizableAccuracyText : BaseUnityPlugin
    {
        public const string PLUGIN_GUID = "net.zachava.customizableaccuracytext";
        public const string PLUGIN_NAME = "Customizable Accuracy Text";
        public const string PLUGIN_VERSION = "1.0.0";
        internal static new ManualLogSource Logger;
        public static ConfigEntry<string> NormalTierTextString;
        public static string[] NormalTierText
        {
            get
            {
                return NormalTierTextString.Value.Split(',');
            }
        }
        public static ConfigEntry<string> SpikeTierText;
        public static ConfigEntry<string> ShowNormalAccuracyString;
        public static bool[] ShowNormalAccuracy
        {
            get
            {
                string[] boolStrings = ShowNormalAccuracyString.Value.Split(',');
                bool[] bools = new bool[boolStrings.Length];
                for (int i = 0; i < boolStrings.Length; i++)
                {
                    bools[i] = bool.Parse(boolStrings[i]);
                }
                return bools;
            }
        }
        public static ConfigEntry<bool> ShowSpikeAccuracy;
        public static ConfigEntry<int> MinPrecisionDigits;
        public static ConfigEntry<bool> EnableMaxScoreDetection;
        public static ConfigEntry<int> MaxScoreThreshold;
        public static ConfigEntry<string> MaxScoreText;
        public static ConfigEntry<bool> ShowMaxScoreAccuracy;
        public static ConfigEntry<bool> LogNonMaxScores;

        private void Awake()
        {
            Logger = base.Logger;
            NormalTierTextString = Config.Bind(
                "General",
                "NormalTierText",
                "MISS,BARELY,OKAY,GOOD,GREAT,PERFECT,CRITICAL",
                "What text to show for each normal tier. Comma-delimited.\n" +
                "Index 0 is MISS, 1 is BARELY, 2 is OKAY, 3 is GOOD, 4 is GREAT, 5 is PERFECT, and 6 is CRITICAL.\n" +
                "You can set an entry to an empty string if you want to not show anything."
            );
            SpikeTierText = Config.Bind(
                "General",
                "SpikeTierText",
                "NICE",
                "What text to show when dodging a spike."
            );
            ShowNormalAccuracyString = Config.Bind(
                "General",
                "ShowNormalAccuracy",
                "false,false,false,false,false,true,true",
                "Whether to show precise accuracy for each normal tier. Comma-delimited bools.\n" +
                "Index 0 is MISS, 1 is BARELY, 2 is OKAY, 3 is GOOD, 4 is GREAT, 5 is PERFECT, and 6 is CRITICAL.\n" +
                "Setting Judgment Style to Simple in-game will suppress precise accuracy text for everything."
            );
            ShowSpikeAccuracy = Config.Bind(
                "General",
                "ShowSpikeAccuracy",
                false,
                "Whether to show precise accuracy for spike dodges."
            );
            MinPrecisionDigits = Config.Bind(
                "General",
                "MinPrecisionDigits",
                3,
                "The minimum number of digits to show for precise accuracy (default 3)."
            );
            EnableMaxScoreDetection = Config.Bind(
                "Max Score",
                "EnableMaxScoreDetection",
                false,
                "Whether to have a special tier for max score. Only displays during play."
            );
            MaxScoreThreshold = Config.Bind(
                "Max Score",
                "MaxScoreThreshold",
                8,
                "The millisecond threshold for max score detection. If absolute value of accuracy is less than or equal to this, it counts as max score."
            );
            MaxScoreText = Config.Bind(
                "Max Score",
                "MaxScoreText",
                "MAX",
                "What text to show when the max score is achieved."
            );
            ShowMaxScoreAccuracy = Config.Bind(
                "Max Score",
                "ShowMaxScoreAccuracy",
                true,
                "Whether to show precise accuracy for max score."
            );
            LogNonMaxScores = Config.Bind(
                "Max Score",
                "LogNonMaxScores",
                false,
                "Whether to log non-max scores to the BepInEx log. Useful if you want to see if a run was max score."
            );

            Logger.LogInfo($"Plugin {PLUGIN_GUID} is loaded!");
            var harmony = new Harmony(PLUGIN_GUID);
            harmony.PatchAll(typeof(BackgroundAccuracyDisplayboardPatches));
        }
    }

    // This is called by the actual patched method in BackgroundAccuracyDisplayboard
    class AttackInfoPatches
    {
        public static string GetTierText(AttackInfo __instance, Score score)
        {
            if (FileStorage.beatmapOptions.judgmentDisplayStyle == StorableBeatmapOptions.JudgmentDisplayStyle.None)
            {
                return "";
            }
            string[] normalTierText = CustomizableAccuracyText.NormalTierText;
            string spikeTierText = CustomizableAccuracyText.SpikeTierText.Value;
            bool[] showNormalAccuracy = CustomizableAccuracyText.ShowNormalAccuracy;
            bool showSpikeAccuracy = CustomizableAccuracyText.ShowSpikeAccuracy.Value;
            int precisionDigits = CustomizableAccuracyText.MinPrecisionDigits.Value;
            string tierText;
            string accuracyText = "";
            bool detailed = FileStorage.beatmapOptions.judgmentDisplayStyle == StorableBeatmapOptions.JudgmentDisplayStyle.Detailed;
            bool whiff = __instance.GetPreciseAccuracy() > score.leniency;
            float preciseAccuracy = __instance.GetPreciseAccuracy();
            string preciseAccuracyText;
			if (preciseAccuracy >= 0f)
			{
				preciseAccuracyText = string.Format("+ {0:D" + precisionDigits + "}", (int)Mathf.Abs(preciseAccuracy));
			}
            else
            {
                preciseAccuracyText = string.Format("- {0:D" + precisionDigits + "}", (int)Mathf.Abs(preciseAccuracy));
            }
            int accuracyTier = __instance.GetAccuracyTier(score);

            if (__instance.info.type == NoteType.Dodge)
            {
                tierText = __instance.miss ? normalTierText[0] : spikeTierText;
                // whiff is true if the "hit" is outside leniency, which for spikes means a miss or avoiding without dodging
                if (detailed && !__instance.miss && !whiff && showSpikeAccuracy)
                {
                    accuracyText = " " + preciseAccuracyText;
                }

                if (string.IsNullOrEmpty(tierText) && string.IsNullOrEmpty(accuracyText))
                {
                    // Hidden tier. Caller skips display so lingering judgements are not overridden.
                    return "";
                }

                return tierText + accuracyText;
            }

            // non-dodge notes
            if (CustomizableAccuracyText.EnableMaxScoreDetection.Value && Mathf.Abs(preciseAccuracy) <= CustomizableAccuracyText.MaxScoreThreshold.Value)
            {
                tierText = CustomizableAccuracyText.MaxScoreText.Value;
                if (detailed && !whiff && CustomizableAccuracyText.ShowMaxScoreAccuracy.Value)
                {
                    accuracyText = " " + preciseAccuracyText;
                }
                return tierText + accuracyText;
            }
            if (CustomizableAccuracyText.LogNonMaxScores.Value && Mathf.Abs(preciseAccuracy) > CustomizableAccuracyText.MaxScoreThreshold.Value)
            {
                CustomizableAccuracyText.Logger.LogInfo(
                    $"Non-max score detected: {preciseAccuracy}ms " +
                    $"({__instance.info.type} note " +
                    $"at {__instance.info.height} height " +
                    $"at {__instance.info.time / 1000f:F3}s)"
                );

            }

            tierText = normalTierText[accuracyTier];
            if (detailed && !whiff && showNormalAccuracy[accuracyTier])
            {
                accuracyText = " " + preciseAccuracyText;
            }

            if (string.IsNullOrEmpty(tierText) && string.IsNullOrEmpty(accuracyText))
            {
                // Empty tier label with timing disabled. Treat as fully hidden for this judgement.
                return "";
            }

            return tierText + accuracyText;
        }
    }

    // Vanilla Display always resets timer, alpha, gradient, alignment, and rotation even when tier text is empty. 
    // We snapshot first, then undo those changes when a judgement is configured to show nothing.
    [HarmonyPatch(typeof(BackgroundAccuracyDisplayboard))]
    [HarmonyPatch("Display")]
    class BackgroundAccuracyDisplayboardPatches
    {
        // Lane visual state that vanilla Display mutates every call.
        private struct DisplayVisualSnapshot
        {
            internal string Score;
            internal float Timer;
            internal float Alpha;
            internal TextAlignmentOptions Alignment;
            internal VertexGradient ColorGradient;
            internal Side Side;
            internal Quaternion BoardRotation; // shared parent transform, not per-lane
        }

        private static DisplayVisualSnapshot CaptureDisplayState(BackgroundAccuracyDisplayboard board, AttackInfo attack)
        {
            TextMeshProUGUI laneText;
            string score;
            float timer;
            Side side;

            if (board.controller.inBrawl || attack.lane.height == Height.Top)
            {
                laneText = board.top;
                score = board.topScore;
                timer = board.topTimer;
                side = board.topSide;
            }
            else if (attack.lane.height == Height.Low)
            {
                laneText = board.low;
                score = board.lowScore;
                timer = board.lowTimer;
                side = board.lowSide;
            }
            else
            {
                laneText = board.mid;
                score = board.midScore;
                timer = board.midTimer;
                side = board.midSide;
            }

            return new DisplayVisualSnapshot
            {
                Score = score,
                Timer = timer,
                Alpha = laneText.alpha,
                Alignment = laneText.alignment,
                ColorGradient = laneText.colorGradient,
                Side = side,
                BoardRotation = board.transform.localRotation
            };
        }

        private static void RestoreDisplayState(BackgroundAccuracyDisplayboard board, AttackInfo attack, DisplayVisualSnapshot snapshot)
        {
            TextMeshProUGUI laneText;

            if (board.controller.inBrawl || attack.lane.height == Height.Top)
            {
                laneText = board.top;
                board.topScore = snapshot.Score;
                board.topTimer = snapshot.Timer;
                board.topSide = snapshot.Side;
            }
            else if (attack.lane.height == Height.Low)
            {
                laneText = board.low;
                board.lowScore = snapshot.Score;
                board.lowTimer = snapshot.Timer;
                board.lowSide = snapshot.Side;
            }
            else
            {
                laneText = board.mid;
                board.midScore = snapshot.Score;
                board.midTimer = snapshot.Timer;
                board.midSide = snapshot.Side;
            }

            laneText.alpha = snapshot.Alpha;
            laneText.alignment = snapshot.Alignment;
            laneText.colorGradient = snapshot.ColorGradient;
            board.transform.localRotation = snapshot.BoardRotation;
        }

        private static void ApplyTierText(BackgroundAccuracyDisplayboard board, AttackInfo attack, string tierText)
        {
            if (board.controller.inBrawl || attack.lane.height == Height.Top)
            {
                board.topScore = tierText;
            }
            else if (attack.lane.height == Height.Low)
            {
                board.lowScore = tierText;
            }
            else
            {
                board.midScore = tierText;
            }
        }

        static void Prefix(BackgroundAccuracyDisplayboard __instance, AttackInfo attack, out DisplayVisualSnapshot __state)
        {
            __state = CaptureDisplayState(__instance, attack);
        }

        static void Postfix(
            BackgroundAccuracyDisplayboard __instance,
            AttackInfo attack,
            Score score,
            DisplayVisualSnapshot __state)
        {
            string tierText = AttackInfoPatches.GetTierText(attack, score);
            if (string.IsNullOrEmpty(tierText))
            {
                // Stats were still counted by vanilla Display; only revert the visual side effects.
                RestoreDisplayState(__instance, attack, __state);
                return;
            }

            // Vanilla already applied timer, alpha, rotation, etc. Override the string only.
            ApplyTierText(__instance, attack, tierText);
        }
    }
}
