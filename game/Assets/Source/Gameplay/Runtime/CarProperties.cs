using System;
using UnityEngine;
using UnityEngine.Events;
using static EngineCommon.Assertions;

namespace Race.Gameplay
{
    // A fixed data model works good for me, because I can just come here and add things if I need.
    // More than that, it's simply data, so no cruft.
    // We could have a more extensible data model with a gameobject with components,
    // or just an array with opaque objects where you'd look up things by their ids,
    // or an autogenerated thing, or a dictionary with string keys.
    // There are many ways to do this dynamically, so it's not a problem to do it this way
    // if we even happen to need it.

    /// <summary>
    /// </summary>
    [System.Serializable]
    public class CarDataModel
    {
        /// <summary>
        /// Info about the car spec, or the configuration.
        /// Includes info about the engine, transmission, wheels.
        /// Does not include any actual game objects or colliders of sorts, it's just plain data.
        /// </summary>
        public readonly CarSpecInfo _spec;

        /// <summary>
        /// Current RPM, wheel RPM, gear and things like that.
        /// </summary>
        public CarDrivingState _drivingState;

        /// <summary>
        /// The info (metadata) attached to the car, taken directly from the game object. 
        /// </summary>
        public readonly CarInfoComponent _infoComponent;

        public CarDataModel(CarSpecInfo spec, CarInfoComponent infoComponent)
        {
            _spec = spec;
            _drivingState = new CarDrivingState();
            _infoComponent = infoComponent;
        }

        public ref CarDrivingState DrivingState => ref _drivingState;

        /// <summary>
        /// We do not allow it to change at runtime.
        /// </summary>
        public ref readonly CarSpecInfo Spec => ref _spec;
        public ref readonly CarColliderParts ColliderParts => ref _infoComponent.colliderParts;
        public ref readonly CarVisualParts VisualParts => ref _infoComponent.visualParts;
    }

    public static class CarDataModelHelper
    {
        // TODO: refactor this logic to be able to reuse it in other calculations.

        /// <summary>
        /// Returns the max speed estimate in m/s.
        /// </summary>
        public static float GetMaxSpeed(this CarDataModel dataModel)
        {
            var maxMotorRPM = dataModel.Spec.engine.maxRPM;
            var maxGearRatio = dataModel.Spec.transmission.gearRatios[^1];
            var circumferenceOfWheel = dataModel.ColliderParts.wheels[0].collider.GetCircumference();
            var maxWheelRPM = maxMotorRPM / maxGearRatio;
            var maxSpeed = FromRPMToSpeed(maxWheelRPM, circumferenceOfWheel);
            return maxSpeed;
        }

        /// <summary>
        /// Returns the speed in m/s.
        /// </summary>
        public static float FromRPMToSpeed(float rpm, float circumference)
        {
            return rpm * circumference / 60.0f;
        }

        /// <summary>
        /// Returns the speed in m/s.
        /// </summary>
        public static float FromRPMToSpeed(float rpm, in CarPart<WheelCollider> wheel)
        {
            return FromRPMToSpeed(rpm, wheel.collider.GetCircumference());
        }

        /// <summary>
        /// Returns the current vehicle speed in m/s.
        /// The speed is calculated based on the wheel RPM.
        /// </summary>
        public static float GetCurrentSpeed(this CarDataModel dataModel)
        {
            // float speed = model.ColliderParts.Rigidbody.velocity.magnitude;
            return FromRPMToSpeed(dataModel.DrivingState.wheelRPM, dataModel.ColliderParts.wheels[0]);
        }

        const float maxEngineEfficiency = 1.0f;

        // The idea is that the engine efficiency peaks when it's at the optimal RPM.
        // It dies down towards the edges (0 and `maxRPM` for the wheels).
        public static float GetEngineEfficiency(float motorRPM, in CarEngineInfo engine)
        {
            // I think in real cars the function should be more sophisticated.
            float a;
            float b;
            float c;

            if (motorRPM < engine.optimalRPM)
            {
                a = engine.optimalRPM - motorRPM;
                b = engine.optimalRPM;
                c = Mathf.Lerp(maxEngineEfficiency, engine.efficiencyAtIdleRPM, a / b);
            }
            else
            {
                a = engine.maxRPM - motorRPM;
                b = engine.maxRPM - engine.optimalRPM;
                c = Mathf.Lerp(engine.efficiencyAtMaxRPM, maxEngineEfficiency, a / b);
            }

            return c;
        }

        // The inverse of GetEngineEfficiency.
        // TODO: unittest for keeping the two in sync.
        /// <summary>
        /// By the spec, gives estimates rather than precise results.
        /// Does not depend on torque.
        /// </summary>
        public static float GetLowEngineRPMAtEngineEfficiency(float efficiency, in CarEngineInfo engine)
        {
            float a = efficiency - engine.efficiencyAtIdleRPM;
            float b = maxEngineEfficiency - engine.efficiencyAtIdleRPM;
            return Mathf.Lerp(engine.idleRPM, engine.optimalRPM, a / b);
        }

        /// <summary>
        /// By the spec, gives estimates rather than precise results.
        /// Does not depend on torque.
        /// </summary>
        public static float GetHighEngineRPMAtEngineEfficiency(float efficiency, in CarEngineInfo engine)
        {
            float a = efficiency - engine.efficiencyAtMaxRPM;
            float b = maxEngineEfficiency - engine.efficiencyAtMaxRPM;
            return Mathf.Lerp(engine.maxRPM, engine.optimalRPM, a / b);
        }
    }

    [RequireComponent(typeof(CarInfoComponent))]
    public class CarProperties : MonoBehaviour
    {
        private CarDataModel _dataModel;
        public ref CarDataModel DataModel => ref _dataModel;

        public void Initialize(CarDataModel dataModel)
        {
            _dataModel = dataModel;
            
            {
                ref readonly var spec = ref _dataModel.Spec;
                assert(spec.motorWheelLocations is not null);
                assert(spec.brakeWheelLocations is not null);
                assert(spec.steeringWheelLocations is not null);
            }

            TriggerDataModelInitialized();
            void TriggerDataModelInitialized()
            {
                OnDataModelInitialized.Invoke(this);
            }
        }

        // TODO: A separate event object could be helpful.
        public UnityEvent<CarProperties> OnDrivingStateChanged;
        public UnityEvent<CarProperties> OnDataModelInitialized;

        public void TriggerOnDrivingStateChanged()
        {
            OnDrivingStateChanged.Invoke(this);
        }

        void Setup()
        {
            TriggerOnDrivingStateChanged();
        }
    }
}