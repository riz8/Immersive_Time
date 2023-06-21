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
        public const string VERSION = "0.2";

        public static ImmersiveTime Instance;

        
        private readonly struct TranslationLabels
        {
            public static readonly string MORNING   = LocalizationManager.Instance.GetLoc(EnvironmentConditions.TimeOfDayTimeSlot.Morning.ToString());      // >= 5 && < 12
            public static readonly string AFTERNOON = LocalizationManager.Instance.GetLoc(EnvironmentConditions.TimeOfDayTimeSlot.AfterNoon.ToString());    // >= 13 && < 18
            public static readonly string EVENING   = LocalizationManager.Instance.GetLoc(EnvironmentConditions.TimeOfDayTimeSlot.Evening.ToString());      // >= 18 && < 22
            public static readonly string NIGHT     = LocalizationManager.Instance.GetLoc(EnvironmentConditions.TimeOfDayTimeSlot.Night.ToString());        // >= 22 && < 5
            public static readonly string NOON      = LocalizationManager.Instance.GetLoc(EnvironmentConditions.TimeOfDayTimeSlot.Noon.ToString());         // See 1
        }

        private string  currentTimeText         = "[PLACEHOLDER]";
        private bool    currentlyIndoor         = false;
        private double  gameTimeWhenEnterScene  = .0;
        private readonly struct TimeSlot
        {
            public TimeSlot(string outputText_, Func<double, bool> predicate_)
            {
                outputText = outputText_;
                predicate = predicate_;
            }

            public bool MatchesCurrentTime()
            {
                double deltaTime = calculteTimeInScene();
                return predicate(deltaTime);
            }
            public string OutputText() { return outputText; }

            private readonly string             outputText;
            private readonly Func<double, bool> predicate;

            public double calculteTimeInScene()
            {
                return EnvironmentConditions.GameTime - Instance.gameTimeWhenEnterScene;
            }
        }
        readonly TimeSlot[] timeSlots = {
            new TimeSlot("Less than 2 hours since enter",   (double timeSinceEnterIndoor) => { return Instance.currentlyIndoor && timeSinceEnterIndoor < 2; } ),
            new TimeSlot("Less than 4 hours since enter",   (double timeSinceEnterIndoor) => { return Instance.currentlyIndoor && timeSinceEnterIndoor < 4; } ),
            new TimeSlot("Less than 6 hours since enter",   (double timeSinceEnterIndoor) => { return Instance.currentlyIndoor && timeSinceEnterIndoor < 6; } ),
            new TimeSlot("You have lost track of time",     (double _) => { return Instance.currentlyIndoor; } ),
            new TimeSlot(TranslationLabels.MORNING,         (double _) => { return EnvironmentConditions.Instance.IsMorning; }),
            new TimeSlot(TranslationLabels.AFTERNOON,       (double _) => { return EnvironmentConditions.Instance.IsAfterNoon; }),
            new TimeSlot(TranslationLabels.EVENING,         (double _) => { return EnvironmentConditions.Instance.IsEvening; }),
            new TimeSlot(TranslationLabels.NIGHT,           (double _) => { return EnvironmentConditions.Instance.IsNight; }),
            new TimeSlot(TranslationLabels.NOON,            (double _) => { return EnvironmentConditions.Instance.IsNoon; }) // See 1
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
            Sprite newSprite = Sprite.Create(texture, textureRect, Vector2.zero, 1, 0, SpriteMeshType.FullRect);
            return newSprite;
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
                    Instance.gameTimeWhenEnterScene = EnvironmentConditions.GameTime;
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

                TimeSlot slot = Array.Find(Instance.timeSlots, timeSlot => timeSlot.MatchesCurrentTime());

                Instance.currentTimeText = slot.OutputText();

                Instance.Logger.LogDebug("Time in instance " + slot.calculteTimeInScene());


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