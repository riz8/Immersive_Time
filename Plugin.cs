using System;
using BepInEx;

namespace ImmersiveTime
{
    [BepInPlugin(GUID, NAME, VERSION)]
    public class ImmersiveTime : BaseUnityPlugin
    {
        public const string GUID = "rob.one.immersive_time";
        public const string NAME = "ImmersiveTime";
        public const string VERSION = "0.1";

        public static ImmersiveTime Instance;

        internal void Awake()
        {
            Instance = this;

            Instance.Logger.LogDebug("Awake");
        }
    }
}
