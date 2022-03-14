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
        public CarSpecInfoObject _spec;

        /// <summary>
        /// Current RPM, wheel RPM, gear and things like that.
        /// </summary>
        public CarDrivingState _drivingState;

        /// <summary>
        /// The gameobjects that are not linked to visuals directly.
        /// </summary>
        public CarColliderParts _colliderParts;

        /// <summary>
        /// We do not allow it to change at runtime.
        /// </summary>
        public ref readonly CarSpecInfo Spec => ref _spec.info;
        public ref CarDrivingState DrivingState => ref _drivingState;
        public ref readonly CarColliderParts ColliderParts => ref _colliderParts;
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

        // The idea is that the engine efficiency peaks when it's at the optimal RPM.
        // It dies down towards the edges (0 and `maxRPM` for the wheels).
        public static float GetEngineEfficiency(float motorRPM, in CarEngineInfo engine)
        {
            // I think in real cars the function should be more sophisticated.
            float a;
            float b;
            float c;
            const float maxEfficiency = 1.0f;

            if (motorRPM < engine.optimalRPM)
            {
                a = engine.optimalRPM - motorRPM;
                b = engine.optimalRPM;
                c = Mathf.Lerp(maxEfficiency, engine.efficiencyAtIdleRPM, a / b);
            }
            else
            {
                a = engine.maxRPM - motorRPM;
                b = engine.maxRPM - engine.optimalRPM;
                c = Mathf.Lerp(engine.efficiencyAtMaxRPM, maxEfficiency, a / b);
            }

            return c;
        }

        public static float GetMotorRPM(float wheelRPM, float gearRatio)
        {
            return wheelRPM * gearRatio; 
        }
    }

    public class CarProperties : MonoBehaviour
    {
        // For now, show it in the inspector.
        // In the end it should be set up dynamically.
        internal CarDataModel _dataModel;
        public CarDataModel DataModel => _dataModel;

        [SerializeField] internal CarVisualParts _visualParts;
        public ref readonly CarVisualParts VisualParts => ref _visualParts;

        // For just configure here, but this should get to us from elsewhere.
        [SerializeField] private CarColliderSetupHelper.CenterOfMassAdjustmentParameters _centerOfMassAdjustmentParameters;
        
        // TODO: A separate event object could be helpful.
        public UnityEvent<CarDataModel> OnDrivingStateChanged;

        public void TriggerOnDrivingStateChanged()
        {
            OnDrivingStateChanged.Invoke(DataModel);
        }

        void Awake()
        {
            CarColliderSetupHelper.AdjustCenterOfMass(ref DataModel._colliderParts, _centerOfMassAdjustmentParameters);

            ref readonly var spec = ref DataModel.Spec;
            var gearRatios = spec.transmission.gearRatios;
            assert(gearRatios is not null);
            assert(gearRatios.Length > 0);

            int firstPositiveGearIndex = -1;
            for (int i = 0; i < gearRatios.Length; i++)
            {
                if (gearRatios[i] > 0)
                {
                    firstPositiveGearIndex = i;
                    break;
                }
            }
            DataModel.DrivingState.gearIndex = firstPositiveGearIndex;

            assert(spec.motorWheelLocations is not null);
            assert(spec.brakeWheelLocations is not null);
            assert(spec.steeringWheelLocations is not null);
        }

        void Setup()
        {
            TriggerOnDrivingStateChanged();
        }
    }
}