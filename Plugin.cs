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

        private string morning      = LocalizationManager.Instance.GetLoc(EnvironmentConditions.TimeOfDayTimeSlot.Morning.ToString());      // >= 5 && < 12
        private string afterNoon    = LocalizationManager.Instance.GetLoc(EnvironmentConditions.TimeOfDayTimeSlot.AfterNoon.ToString());    // >= 13 && < 18
        private string evening      = LocalizationManager.Instance.GetLoc(EnvironmentConditions.TimeOfDayTimeSlot.Evening.ToString());      // >= 18 && < 22
        private string night        = LocalizationManager.Instance.GetLoc(EnvironmentConditions.TimeOfDayTimeSlot.Night.ToString());        // >= 22 && < 5
        private string noon         = LocalizationManager.Instance.GetLoc(EnvironmentConditions.TimeOfDayTimeSlot.Noon.ToString());

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


        [HarmonyPatch(typeof(MapDisplay), "Update")]
        public class MapDisplay_Update
        {
            [HarmonyPostfix]
            public static void Postfix(MapDisplay __instance)
            {
                __instance.m_timeOfDay.text = Instance.morning;

            }
        }
    }
}
