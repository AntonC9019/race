using System;
using EngineCommon;
using UnityEngine;
using static EngineCommon.Assertions;

namespace Race.Gameplay
{
    [System.Serializable]
    public struct KeyboardInputSmoothingParameters
    {
        // 1.5, 2
        public float maxSteeringAngleInputFactorChangePerSecond;

        // This one here is used to simulate gradual input specifically.
        // We might need another such engine-specific factor.
        public float maxMotorTorqueInputFactorChangePerSecond;
    }

    // TODO:
    // We should straight up have an adaptive input view that should handle any input source.
    // It should only be dependent on the input map, and select the way it interprets and how it
    // filters inputs based on where they come from.
    // Perhaps the simplest way would be a switch over the input device type,
    // and then just handle each case.
    // There aren't that many options, at the end of the day.
    public class CarKeyboardInputView : ICarInputView
    {
        private KeyboardInputSmoothingParameters _smoothingParameters;
        private CarControls.PlayerActions _player;
        
        public CarKeyboardInputView(KeyboardInputSmoothingParameters smoothingParameters, CarControls.PlayerActions player)
        {
            _smoothingParameters = smoothingParameters;
            _player = player;
        }

        public CarProperties CarProperties { get; set; }

        public CarMovementInputValues Movement
        {
            get
            {
                // I'm a little uneasy about this one.
                // I guess we just say that the movement data in the driving state is only to change in 
                // FixedUpdate(), then this is fine.
                // But I still kind of dislike that we tie ourselves to the engine like this here,
                // even though we cannot get "pure" input data without the engine backing us up.
                float timeSinceLastInput = Time.fixedDeltaTime;

                CarMovementInputValues result;
                var player = _player;
                // In case of brake, we just apply the read amount.
                // This is different for motor torque, where we want to allow gradual changes.
                // Maybe?? I'm not sure. We might want that damping here too.
                result.Brakes = player.Backward.ReadValue<float>();

                result.Forward = MathHelper.GetValueChangedByAtMost(
                    CarProperties.DataModel.DrivingState.motorTorqueInputFactor,
                    desiredValue: player.Forward.ReadValue<float>(),
                    _smoothingParameters.maxMotorTorqueInputFactorChangePerSecond * timeSinceLastInput);

                result.Turn = MathHelper.GetValueChangedByAtMost(
                    CarProperties.DataModel.DrivingState.steeringInputFactor,
                    desiredValue: player.Turn.ReadValue<float>(),
                    // This one might be part of the controller tho,
                    // Because the amount a wheel can turn should be constrained.
                    _smoothingParameters.maxSteeringAngleInputFactorChangePerSecond * timeSinceLastInput);

                return result;
            }
        }

        public bool Clutch => _player.Clutch.ReadValue<float>() > 0;

        public GearInputType Gear
        {
            get
            {
                var player = _player;
                if (player.GearUp.WasPerformedThisFrame())
                    return GearInputType.GearUp;
                if (player.GearDown.WasPerformedThisFrame())
                    return GearInputType.GearDown;
                return GearInputType.None;
            }
        }
    }
}