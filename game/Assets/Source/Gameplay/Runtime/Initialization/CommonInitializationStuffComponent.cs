using UnityEngine;

namespace Race.Gameplay
{
    public class CommonInitializationStuffComponent : MonoBehaviour
    {
        public CommonInitializationStuff stuff;
    }

    [System.Serializable]
    public struct CommonInitializationStuff
    {
        public KeyboardInputViewFactory inputViewFactory;

        /// <summary>
        /// Currently, only UI needs DI (dependency injection).
        /// </summary>
        public Transform diRootTransform;

        public TrackLimitsConfiguration trackLimits;
        public Transform raceLogicTransform;
        public RaceProperties raceProperties;
    }
}