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
            public static readonly string MORNING   = LocalizationManager.Instance.GetLoc(EnvironmentConditions.TimeOfDayTimeSlot.Morning.ToString());      // >= 5 && < 12
            public static readonly string AFTERNOON = LocalizationManager.Instance.GetLoc(EnvironmentConditions.TimeOfDayTimeSlot.AfterNoon.ToString());    // >= 13 && < 18
            public static readonly string EVENING   = LocalizationManager.Instance.GetLoc(EnvironmentConditions.TimeOfDayTimeSlot.Evening.ToString());      // >= 18 && < 22
            public static readonly string NIGHT     = LocalizationManager.Instance.GetLoc(EnvironmentConditions.TimeOfDayTimeSlot.Night.ToString());        // >= 22 && < 5
            public static readonly string NOON      = LocalizationManager.Instance.GetLoc(EnvironmentConditions.TimeOfDayTimeSlot.Noon.ToString());         // See 1
        }

        private string  currentTimeText         = "[PLACEHOLDER]";
        private bool    currentlyIndoor         = false;
        private float   gameTimeWhenEnterScene  = 0f;
        private readonly struct TimeSlot
        {
            public TimeSlot(string outputText_, Func<float, bool> predicate_)
            {
                outputText  = outputText_;
                predicate   = predicate_;
            }

            public bool MatchesCurrentTime()
            {
                float deltaTime = EnvironmentConditions.GameTimeF - Instance.gameTimeWhenEnterScene;
                return predicate(deltaTime);
            }
            public string OutputText() { return outputText; }

            private readonly string             outputText;
            private readonly Func<float, bool>  predicate;
        }
        readonly TimeSlot[] timeSlots = {
            new TimeSlot("Less than 2 hours since enter",   (float timeSinceEnterIndoor) => { return Instance.currentlyIndoor && timeSinceEnterIndoor < 2; } ),
            new TimeSlot("Less than 4 hours since enter",   (float timeSinceEnterIndoor) => { return Instance.currentlyIndoor && timeSinceEnterIndoor < 4; } ),
            new TimeSlot("Less than 6 hours since enter",   (float timeSinceEnterIndoor) => { return Instance.currentlyIndoor && timeSinceEnterIndoor < 6; } ),
            new TimeSlot("You have lost track of time",     (float _) => { return Instance.currentlyIndoor; } ),
            new TimeSlot(TranslationLabels.MORNING,         (float _) => { return EnvironmentConditions.Instance.IsMorning; }),
            new TimeSlot(TranslationLabels.AFTERNOON,       (float _) => { return EnvironmentConditions.Instance.IsAfterNoon; }),
            new TimeSlot(TranslationLabels.EVENING,         (float _) => { return EnvironmentConditions.Instance.IsEvening; }),
            new TimeSlot(TranslationLabels.NIGHT,           (float _) => { return EnvironmentConditions.Instance.IsNight; }),
            new TimeSlot(TranslationLabels.NOON,            (float _) => { return EnvironmentConditions.Instance.IsNoon; }) // See 1
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
                TimeSlot slot = Array.Find(Instance.timeSlots, timeSlot => timeSlot.MatchesCurrentTime());
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
    }
}

// 1.   The source code has a bug using || instead of &&
//      This means that IsNoon matches anything after 12. Like: (time > 12 || time < 5)
//      The best counteraction is to validate Noon after all other timeslots