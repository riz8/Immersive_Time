using System;
using BepInEx;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace ImmersiveTime
{
    [BepInPlugin(GUID, NAME, VERSION)]
    public class ImmersiveTime : BaseUnityPlugin
    {
        public const string GUID = "rob.one.immersive_time";
        public const string NAME = "ImmersiveTime";
        public const string VERSION = "0.3";

        public static ImmersiveTime Instance;


        private readonly struct TranslationLabels
        {
            public static readonly string MORNING = LocalizationManager.Instance.GetLoc(EnvironmentConditions.TimeOfDayTimeSlot.Morning.ToString());
            public static readonly string NOON = LocalizationManager.Instance.GetLoc(EnvironmentConditions.TimeOfDayTimeSlot.Noon.ToString());
            public static readonly string AFTERNOON = LocalizationManager.Instance.GetLoc(EnvironmentConditions.TimeOfDayTimeSlot.AfterNoon.ToString());
            public static readonly string EVENING = LocalizationManager.Instance.GetLoc(EnvironmentConditions.TimeOfDayTimeSlot.Evening.ToString());
            public static readonly string NIGHT = LocalizationManager.Instance.GetLoc(EnvironmentConditions.TimeOfDayTimeSlot.Night.ToString());
        }

        private string currentTimeText          = "[PLACEHOLDER]";
        private bool currentlyIndoor            = false;
        private float gameTimeWhenEnterScene    = 0f;

        private class TimeSlot
        {
            public TimeSlot(string outputText_, Func<float, bool> predicate_)
            {
                outputText  = outputText_;
                predicate   = predicate_;
            }

            public bool MatchesTimeInCave()
            {
                float deltaTime = EnvironmentConditions.GameTimeF - Instance.gameTimeWhenEnterScene;
                return predicate(deltaTime);
            }

            public bool MatchesCurrentGameHour()
            {
                float game_hour = TOD_Sky.Instance.Cycle.Hour;
                return predicate(game_hour);
            }

            public string OutputText() { return outputText; }

            private readonly string outputText;
            protected readonly Func<float, bool> predicate;
        }

        readonly TimeSlot[] timeSlotsCave = {
            new TimeSlot("Less than 2 hours since enter",   (float timeSinceEnterIndoor) => { return Instance.currentlyIndoor && timeSinceEnterIndoor < 2; } ),
            new TimeSlot("Less than 4 hours since enter",   (float timeSinceEnterIndoor) => { return Instance.currentlyIndoor && timeSinceEnterIndoor < 4; } ),
            new TimeSlot("Less than 6 hours since enter",   (float timeSinceEnterIndoor) => { return Instance.currentlyIndoor && timeSinceEnterIndoor < 6; } ),
            new TimeSlot("You have lost track of time",     (float _) => { return Instance.currentlyIndoor; } ),
        };
        readonly TimeSlot[] timeSlotsOutdoor = {
            new TimeSlot(TranslationLabels.MORNING,         (float gameHour) => { return Instance.IsMorning(gameHour); }),
            new TimeSlot(TranslationLabels.NOON,            (float gameHour) => { return Instance.IsNoon(gameHour); }),
            new TimeSlot(TranslationLabels.AFTERNOON,       (float gameHour) => { return Instance.IsAfterNoon(gameHour); }),
            new TimeSlot(TranslationLabels.EVENING,         (float gameHour) => { return Instance.IsEvening(gameHour); }),
            new TimeSlot(TranslationLabels.NIGHT,           (float gameHour) => { return Instance.IsNight(gameHour); })
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

        private bool IsMorning(float hour)      { return hour >= 5f && hour < 12f; }
        private bool IsNoon(float hour)         { return hour >= 12f && hour < 13f; }
        private bool IsAfterNoon(float hour)    { return hour >= 13f && hour < 18f; }
        private bool IsEvening(float hour)      { return hour >= 18f && hour < 22f; }
        private bool IsNight(float hour)        { return hour >= 22f || hour < 5f; }

        public class CaveEntryData : SideLoader.SaveData.PlayerSaveExtension
        {
            public float    gameTimeWhenEnterScene;
            public bool     currentlyIndoor;
            public override void Save(Character character, bool isWorldHost)
            {
                gameTimeWhenEnterScene  = Instance.gameTimeWhenEnterScene;
                currentlyIndoor         = Instance.currentlyIndoor;
            }

            public override void ApplyLoadedSave(Character character, bool isWorldHost)
            {
                Instance.gameTimeWhenEnterScene = gameTimeWhenEnterScene;
                Instance.currentlyIndoor = currentlyIndoor;
            }
        }

        private static Sprite CreateSpriteFromFile(string filePath)
        {
            if (!System.IO.File.Exists(filePath))
            {
                return null;
            }

            byte[] byteData = System.IO.File.ReadAllBytes(filePath);
            Texture2D texture = new(0, 0, TextureFormat.RGBA32, false);
            texture.LoadImage(byteData, true);
            Rect textureRect = new(0, 0, texture.width, texture.height);
            return Sprite.Create(texture, textureRect, Vector2.zero, 1, 0, SpriteMeshType.FullRect);
        }

        [HarmonyPatch(typeof(RestingMenu), "StartInit")]
        public class RestingMenu_StartInit
        {
            [HarmonyPostfix]
            public static void Postfix(RestingMenu __instance)
            {
                var img = CreateSpriteFromFile("D:/rest_image_orig.png");
                var tile1 = __instance.transform.FindInAllChildren("Tile1").GetComponent<Image>();
                var tile2 = __instance.transform.FindInAllChildren("Tile2").GetComponent<Image>();
                tile1.sprite = img;
                tile2.sprite = img;
            }
        }

        [HarmonyPatch(typeof(SceneInteractionManager), "DoneLoadingLevel")]
        public class SceneInteractionManager_DoneLoadingLevel
        {
            [HarmonyPostfix]
            public static void Postfix(SceneInteractionManager __instance)
            {
                MapDisplay.Instance.FetchMap(); // We need to update the map for m_currentAreaHasMap to be set
                bool enteringIndoor = !MapDisplay.Instance.m_currentAreaHasMap;

                if (!Instance.currentlyIndoor && enteringIndoor) // Moving inside
                {
                    Instance.gameTimeWhenEnterScene = EnvironmentConditions.GameTimeF;
                    Instance.Logger.LogDebug("Entering inside. New timer " + Instance.gameTimeWhenEnterScene);
                }

                Instance.currentlyIndoor = enteringIndoor;
            }
        }

        [HarmonyPatch(typeof(MapDisplay), "Show", new Type[] { })]
        public class MapDisplay_Show
        {
            [HarmonyPostfix]
            public static void Postfix(MapDisplay __instance)
            {
                TimeSlot slot = Array.Find(Instance.timeSlotsCave, timeSlot => timeSlot.MatchesTimeInCave());
                if (slot is null)
                {
                    slot = Array.Find(Instance.timeSlotsOutdoor, timeSlot => timeSlot.MatchesCurrentGameHour());
                }

                Instance.currentTimeText = slot.OutputText();
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

        [HarmonyPatch(typeof(QuestEntryDisplay), "SetDetail")]
        public class QuestEntryDisplay_SetDetail
        {
            [HarmonyPostfix]
            public static void Postfix(QuestEntryDisplay __instance, QuestLogEntry _logEntry)
            {
                __instance.m_currentLogEntry = _logEntry;
                if (__instance.m_lblText)
                {
                    __instance.m_lblText.text = "- " + _logEntry.Text;
                }
                if (__instance.m_lblLogDate)
                {
                    float time = _logEntry.LogTime;
                    if (time != 0f)
                    {
                        int days_past = Mathf.FloorToInt(time / 24f) + 1;
                        string text = string.Format(

                            "{0} {1}\n" +   // Day 1
                            "{2}",          // Morning

                            LocalizationManager.Instance.GetLoc("General_Day"), (days_past).ToString(),
                            "Morning"
                        );

                        __instance.m_lblLogDate.text = text;
                        return;
                    }
                    __instance.m_lblLogDate.text = string.Empty;
                }
            }
        }
    }
}
