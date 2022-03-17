using UnityEngine;
using static EngineCommon.Assertions;

namespace Race.Gameplay
{
    public class Tachometer : MonoBehaviour
    {
        [SerializeField] private CarProperties _carProperties;
        [SerializeField] private RadialValueDisplay _valueDisplay;
        public ValueRange _displayValueRange;

        void Awake()
        {
            _carProperties.OnDataModelInitialized.AddListener(OnDataModelInitialized);
            _carProperties.OnDrivingStateChanged.AddListener(OnDrivingStateChanged);
        }

        public void OnDataModelInitialized(CarProperties properties)
        {
            ref readonly var engine = ref properties.DataModel.Spec.engine;

            _displayValueRange = new ValueRange
            {
                minValue = engine.idleRPM,
                maxValue = engine.maxRPM,
            };

            _valueDisplay.ResetPipsAndTextsToValues(_displayValueRange, largePipGap: 500.0f);
        }

        public void OnDrivingStateChanged(CarProperties properties)
        {
            float motorRPM = properties.DataModel.DrivingState.motorRPM;
            float normalizedRPM = (motorRPM - _displayValueRange.minValue) / _displayValueRange.Length;
            float clamped = Mathf.Clamp01(normalizedRPM);
            _valueDisplay.ResetNeedleRotation(clamped);
        }
    }
}