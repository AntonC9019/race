using EngineCommon;
using UnityEngine;
using static EngineCommon.Assertions;

namespace Race.Gameplay
{
    public class BotInputView : ICarInputView
    {
        public CarMovementInputValues Movement
        {
            get
            {
                return new CarMovementInputValues
                {
                    Forward = 1.0f,
                    Brakes = 0.0f,
                    Turn = 0.0f,
                };
            }
        }

        public bool Clutch => false;
        public GearInputType Gear => GearInputType.None;

        public void OnDrivingStateChanged(CarProperties properties)
        {
            // do stuff.
        }

        public void ResetTo(CarProperties properties)
        {
            // TODO: obviously broken
            properties.OnDrivingStateChanged.AddListener(OnDrivingStateChanged);
        }
    }
}