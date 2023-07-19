using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx;
using HarmonyLib;
using UnityEngine;
using BepInEx.Configuration;

namespace ImmersiveTime
{
    [BepInDependency("com.bepis.bepinex.configurationmanager", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("io.mefino.configurationmanager", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInPlugin(GUID, NAME, VERSION)]
    public class ImmersiveTime : BaseUnityPlugin
    {
        public const string GUID = "rob.one.immersive_time";
        public const string NAME = "ImmersiveTime";
        public const string VERSION = "0.9.1";

        public static ConfigEntry<bool> _alwaysOutdoorEnabled;

        public static ImmersiveTime Instance;
        #region STATIC
        private readonly struct TranslationLabels
        {
            public static readonly string EARLY_MORNING     = "Early Morning";
            public static readonly string MORNING           = "Morning";
            public static readonly string LATE_MORNING      = "Late Morning";
            public static readonly string NOON              = "Noon";
            public static readonly string EARLY_AFTERNOON   = "Early Afternoon";
            public static readonly string AFTERNOON         = "Afternoon";
            public static readonly string LATE_AFTERNOON    = "Late Afternoon";
            public static readonly string EARLY_EVENING     = "Early Evening";
            public static readonly string EVENING           = "Evening";
            public static readonly string LATE_EVENING      = "Late Evening";
            public static readonly string EARLY_NIGHT       = "Early Night";
            public static readonly string NIGHT             = "Night";
            public static readonly string LATE_NIGHT        = "Late Night";
            public static readonly string PHASE1            = "Less than 2 hours since enter";
            public static readonly string PHASE2            = "Less than 4 hours since enter";
            public static readonly string PHASE3            = "Less than 6 hours since enter";
            public static readonly string PHASE4            = "You have lost track of the time";
        }
        static readonly Dictionary<int, string> OutdoorTime = 
            new Dictionary<int, string>
        {
                {0, TranslationLabels.EARLY_NIGHT },
                {1, TranslationLabels.NIGHT },
                {2, TranslationLabels.NIGHT },
                {3, TranslationLabels.NIGHT },
                {4, TranslationLabels.LATE_NIGHT },
                {5, TranslationLabels.LATE_NIGHT },
                {6, TranslationLabels.EARLY_MORNING },
                {7, TranslationLabels.EARLY_MORNING },
                {8, TranslationLabels.MORNING },
                {9, TranslationLabels.MORNING },
                {10, TranslationLabels.LATE_MORNING },
                {11, TranslationLabels.LATE_MORNING },
                {12, TranslationLabels.NOON },
                {13, TranslationLabels.EARLY_AFTERNOON },
                {14, TranslationLabels.EARLY_AFTERNOON },
                {15, TranslationLabels.AFTERNOON },
                {16, TranslationLabels.AFTERNOON },
                {17, TranslationLabels.LATE_AFTERNOON },
                {18, TranslationLabels.LATE_AFTERNOON },
                {19, TranslationLabels.EARLY_EVENING },
                {20, TranslationLabels.EVENING },
                {21, TranslationLabels.EVENING },
                {22, TranslationLabels.LATE_EVENING },
                {23, TranslationLabels.EARLY_NIGHT }
        };
        static readonly Dictionary<int, string> IndoorTime =
            new Dictionary<int, string>
        {
                {0, TranslationLabels.PHASE1 },
                {1, TranslationLabels.PHASE1 },
                {2, TranslationLabels.PHASE2 },
                {3, TranslationLabels.PHASE2 },
                {4, TranslationLabels.PHASE3 },
                {5, TranslationLabels.PHASE3 },
                {6, TranslationLabels.PHASE4 },
        };
        #endregion

        private string      currentTimeText             = "[PLACEHOLDER]";
        private bool        currentlyOutdoor            = true;
        private float       gameTimeWhenEnterScene      = 0f;
        private GameObject  overlay1;
        private GameObject  overlay2;
        private GameObject  overlay3;
        private GameObject  overlay4;
        private GameObject  restMenuScale;
        private GameObject  restMenuIndicators;


        internal void Awake()
        {
            Instance = this;

            var harmony = new Harmony(GUID);
            harmony.PatchAll();
            _alwaysOutdoorEnabled = Config.Bind("Immersive Indoor", "Disable to allow special indoor time", false, "Disabled: The longer you are in a cave the less knowledge about time you will have. Sundial, flavor text and time indicators are all changed.");
        }

        internal void Update()
        {
        }

        public class CaveEntryData : SideLoader.SaveData.PlayerSaveExtension
        {
            public float    gameTimeWhenEnterScene;
            public bool     currentlyOutdoor;
            public override void Save(Character character, bool isWorldHost)
            {
                gameTimeWhenEnterScene  = Instance.gameTimeWhenEnterScene;
                currentlyOutdoor        = Instance.currentlyOutdoor;
            }

            public override void ApplyLoadedSave(Character character, bool isWorldHost)
            {
                Instance.gameTimeWhenEnterScene = gameTimeWhenEnterScene;
                Instance.currentlyOutdoor = _alwaysOutdoorEnabled.Value || currentlyOutdoor;

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
                Instance.restMenuScale = __instance.transform.FindInAllChildren("Scale").gameObject;
                Instance.restMenuIndicators = __instance.transform.FindInAllChildren("Indicators").gameObject;

                string plugin_path = Instance.Info.Location.Replace("ImmersiveTime.dll", "") + "Sprites/";

                var rest1 = CreateSpriteFromFile(plugin_path +"rest1.png");
                var rest2 = CreateSpriteFromFile(plugin_path + "rest2.png");
                var rest3 = CreateSpriteFromFile(plugin_path + "rest3.png");
                var rest4 = CreateSpriteFromFile(plugin_path + "rest4.png");

                var tile2 = __instance.transform.FindInAllChildren("Tile2");

                Instance.overlay1 = Instantiate(tile2.gameObject);
                Instance.overlay2 = Instantiate(tile2.gameObject);
                Instance.overlay3 = Instantiate(tile2.gameObject);
                Instance.overlay4 = Instantiate(tile2.gameObject);

                Instance.overlay1.name = "overlay1";
                Instance.overlay2.name = "overlay2";
                Instance.overlay3.name = "overlay3";
                Instance.overlay4.name = "overlay4";

                Instance.overlay1.GetComponent<UnityEngine.UI.Image>().sprite = rest1;
                Instance.overlay2.GetComponent<UnityEngine.UI.Image>().sprite = rest2;
                Instance.overlay3.GetComponent<UnityEngine.UI.Image>().sprite = rest3;
                Instance.overlay4.GetComponent<UnityEngine.UI.Image>().sprite = rest4;

                Instance.overlay1.transform.parent = __instance.transform.FindInAllChildren("Scroll View");
                Instance.overlay2.transform.parent = __instance.transform.FindInAllChildren("Scroll View");
                Instance.overlay3.transform.parent = __instance.transform.FindInAllChildren("Scroll View");
                Instance.overlay4.transform.parent = __instance.transform.FindInAllChildren("Scroll View");

                Instance.overlay1.transform.position = tile2.transform.position;
                Instance.overlay2.transform.position = tile2.transform.position;
                Instance.overlay3.transform.position = tile2.transform.position;
                Instance.overlay4.transform.position = tile2.transform.position;

                Instance.overlay1.transform.localScale = tile2.transform.localScale;
                Instance.overlay2.transform.localScale = tile2.transform.localScale;
                Instance.overlay3.transform.localScale = tile2.transform.localScale;
                Instance.overlay4.transform.localScale = tile2.transform.localScale;

                Instance.overlay1.SetActive(false);
                Instance.overlay2.SetActive(false);
                Instance.overlay3.SetActive(false);
                Instance.overlay4.SetActive(false);
            }
        }
        [HarmonyPatch(typeof(RestingMenu), "Show")]
        public class RestingMenu_Show
        {
            [HarmonyPostfix]
            public static void Postfix(RestingMenu __instance)
            {
                Instance.overlay1.SetActive(false);
                Instance.overlay2.SetActive(false);
                Instance.overlay3.SetActive(false);
                Instance.overlay4.SetActive(false);
                Instance.restMenuScale.SetActive(true);
                Instance.restMenuIndicators.SetActive(true);

                if (Instance.currentlyOutdoor)
                    return;

                Instance.restMenuScale.SetActive(false);
                Instance.restMenuIndicators.SetActive(false);

                var time_inside_cave = (int)(EnvironmentConditions.GameTimeF - Instance.gameTimeWhenEnterScene);

                if (time_inside_cave < 2)
                    Instance.overlay1.SetActive(true);
                else if (time_inside_cave < 4)
                    Instance.overlay2.SetActive(true);
                else if (time_inside_cave < 6)
                    Instance.overlay3.SetActive(true);
                else
                    Instance.overlay4.SetActive(true);
            }
        }

        [HarmonyPatch(typeof(SceneInteractionManager), "DoneLoadingLevel")]
        public class SceneInteractionManager_DoneLoadingLevel
        {
            [HarmonyPostfix]
            public static void Postfix(SceneInteractionManager __instance)
            {
                MapDisplay.Instance.FetchMap(); // We need to update the map for m_currentAreaHasMap to be set
                bool enteringOutdoor = _alwaysOutdoorEnabled.Value || MapDisplay.Instance.m_currentAreaHasMap;
                if (Instance.currentlyOutdoor && !enteringOutdoor) // Moving inside
                {
                    Instance.gameTimeWhenEnterScene = EnvironmentConditions.GameTimeF;
                }

                Instance.currentlyOutdoor = enteringOutdoor;
            }
        }

        [HarmonyPatch(typeof(MapDisplay), "Show", new Type[] { })]
        public class MapDisplay_Show
        {
            [HarmonyPostfix]
            public static void Postfix(MapDisplay __instance)
            {
                string flavor_text = string.Empty;
                if (Instance.currentlyOutdoor)
                {
                    var game_hour = (int)TOD_Sky.Instance.Cycle.Hour;
                    flavor_text = OutdoorTime[game_hour];
                }
                else
                {
                    var time_inside_cave = (int)(EnvironmentConditions.GameTimeF - Instance.gameTimeWhenEnterScene);
                    if (!IndoorTime.TryGetValue(time_inside_cave, out flavor_text))
                    {
                        flavor_text = IndoorTime.Last().Value;
                    }

                }

                Instance.currentTimeText = flavor_text;
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
                if (__instance.m_lblLogDate)
                {
                    float time = _logEntry.LogTime;
                    if (time != 0f)
                    {
                        int days_past = Mathf.FloorToInt(time / 24f);
                        int hour = Mathf.FloorToInt(time - (days_past * 24));
                        string text = string.Format(

                            "{0} {1}\n" +   // Day 1
                            "{2}",          // Morning

                            LocalizationManager.Instance.GetLoc("General_Day"), (days_past+1).ToString(),
                            OutdoorTime[hour]
                        );

                        __instance.m_lblLogDate.text = text;
                    }
                }
            }
        }
    }
}
