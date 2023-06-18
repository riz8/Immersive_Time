using System;
using BepInEx;
using HarmonyLib;

namespace ImmersiveTime
{
    [BepInPlugin(GUID, NAME, VERSION)]
    public class ImmersiveTime : BaseUnityPlugin
    {
        public const string GUID = "rob.one.immersive_time";
        public const string NAME = "ImmersiveTime";
        public const string VERSION = "0.1";

        public static ImmersiveTime Instance;

        
        private readonly struct TranslationLabels
        {
            public static readonly string MORNING   = LocalizationManager.Instance.GetLoc(EnvironmentConditions.TimeOfDayTimeSlot.Morning.ToString());      // >= 5 && < 12
            public static readonly string AFTERNOON = LocalizationManager.Instance.GetLoc(EnvironmentConditions.TimeOfDayTimeSlot.AfterNoon.ToString());    // >= 13 && < 18
            public static readonly string EVENING   = LocalizationManager.Instance.GetLoc(EnvironmentConditions.TimeOfDayTimeSlot.Evening.ToString());      // >= 18 && < 22
            public static readonly string NIGHT     = LocalizationManager.Instance.GetLoc(EnvironmentConditions.TimeOfDayTimeSlot.Night.ToString());        // >= 22 && < 5
            public static readonly string NOON      = LocalizationManager.Instance.GetLoc(EnvironmentConditions.TimeOfDayTimeSlot.Noon.ToString());         // See 1
        }

        private string currentTimeText;
        private readonly struct TimeSlot
        {
            public TimeSlot(string outputText_, Func<bool> predicate_)
            {
                outputText = outputText_;
                predicate = predicate_;
            }

            public bool MatchesCurrentTime() { return predicate(); }
            public string OutputText() { return outputText; }

            private readonly string     outputText;
            private readonly Func<bool> predicate;
        }

        readonly TimeSlot[] timeSlots = {
            new TimeSlot(TranslationLabels.MORNING,     () => { return EnvironmentConditions.Instance.IsMorning; }),
            new TimeSlot(TranslationLabels.AFTERNOON,   () => { return EnvironmentConditions.Instance.IsAfterNoon; }),
            new TimeSlot(TranslationLabels.EVENING,     () => { return EnvironmentConditions.Instance.IsEvening; }),
            new TimeSlot(TranslationLabels.NIGHT,       () => { return EnvironmentConditions.Instance.IsNight; }),
            new TimeSlot(TranslationLabels.NOON,        () => { return EnvironmentConditions.Instance.IsNoon; }) // See 1
        };

        internal void Awake()
        {
            Instance = this;

            var harmony = new Harmony(GUID);
            harmony.PatchAll();

            //Instance.Logger.LogDebug("Awake");
        }

        internal void Update()
        {
            //Instance.Logger.LogDebug("Update");
        }

        [HarmonyPatch(typeof(MapDisplay), "Show", new Type[] { })]
        public class MapDisplay_Show
        {
            [HarmonyPostfix]
            public static void Postfix(MapDisplay _)
            {
                Instance.currentTimeText = Array.Find(
                    Instance.timeSlots, timeSlot => timeSlot.MatchesCurrentTime())
                        .OutputText();
            }
        }

        [HarmonyPatch(typeof(MapDisplay), "Update")]
        public class MapDisplay_Update
        {
            [HarmonyPostfix]
            public static void Postfix(MapDisplay __instance)
            {
                __instance.m_timeOfDay.text = Instance.currentTimeText;

            }
        }
    }
}

// 1.   The source code has a bug using || instead of &&
//      This means that IsNoon matches anything after 12. Like: (time > 12 || time < 5)
//      The best counteraction is to validate Noon after all other timeslots