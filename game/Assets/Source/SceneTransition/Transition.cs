using UnityEngine;
using Race.Gameplay;
using Race.Garage;
using System;
using static EngineCommon.Assertions;

namespace Race.SceneTransition
{
    [System.Serializable]
    public struct PlayerInfo
    {
        public int carIndex;
        public Garage.CarDataModel carDataModel;
        public UserDataModel userDataModel;
        public IInputViewFactory inputViewFactory;
    }

    /// <summary>
    /// Creates and initializes input views.
    /// </summary>
    public interface IInputViewFactory
    {
        ICarInputView CreateCarInputView(int participantIndex, Gameplay.CarProperties carProperties);
        ICameraInputView CreateCameraInputView(int participantIndex, Gameplay.CarProperties carProperties);
    }
   
    [System.Serializable]
    public struct BotInfo
    {
        public int carIndex;
        public ICarInputView inputView;
    }

    public ref struct GameplayInitializationInfo
    {
        public Span<PlayerInfo> playerInfos;
        public Span<BotInfo> botInfos;
    }

    [System.Serializable]
    public struct StatsConversionRates
    {
        // for now keep it const
        public const float c_torqueFactor = 1.0f;
        public readonly float torqueFactor => c_torqueFactor;
    }

    public class Transition : MonoBehaviour
    {
        [SerializeField] private GameObject _cameraControlPrefab;

        public static CarSpecInfo GetEngineSpecFromStatsAndTemplate(
            in CarStats currentStats,
            in StatsConversionRates rates,
            in Race.Gameplay.CarSpecInfo template)
        {
            // Initialize by copying (it's a struct).
            var carSpec = template;

            // We don't do a full copy, we only copy what we know is going to change.
            // Right now the only reference type that gets changed is the gear ratios.
            // Wheel locations for example still point to the template ones.
            {
                ref var g = ref carSpec.transmission.gearRatios;
                g = g[..];
            }
            
            float motorRPMAtOldTorque;
            {
                float a = currentStats.accelerationModifier;
                float torqueBaseline = template.engine.maxTorque;
                float newTorque = a * rates.torqueFactor + torqueBaseline;

                // At optimalRPM the engine gave T torque.
                // Now it will give T' torque at that same point.
                // We shift it by dN to get the new desired RPM, such that at the old RPM it stays at T.  
                float previousTorque = torqueBaseline;
                // How much of the new torque is enough to get the previous torque.
                float neededEfficiencyForOldTorque = previousTorque / newTorque;

                motorRPMAtOldTorque = CarDataModelHelper.GetLowEngineRPMAtEngineEfficiency(neededEfficiencyForOldTorque, template.engine);

                carSpec.engine.maxTorque = newTorque;
            }
            
            foreach (ref var g in carSpec.transmission.gearRatios.AsSpan())
                g = template.engine.optimalRPM / (g * motorRPMAtOldTorque);

            return carSpec;
        }

        // TODO:
        public GameObject SpawnCar(Transform parent, int carIndex)
        {
            return null;
        }

        public static void ApplyColor(Color color, CarInfoComponent infoComponent)
        {
            infoComponent.visualParts.meshRenderer.material.color = color;
        }

        // Initialization always turns into a mess.
        public void InitializeScene(Transform root, in GameplayInitializationInfo initInfo)
        {
            // assert(initInfo.playerInfos.Length == 1, "For now only one player is allowed");

            var playerInfos = initInfo.playerInfos;

            for (int playerIndex = 0; playerIndex < playerInfos.Length; playerIndex++)
            {
                ref var playerInfo = ref playerInfos[playerIndex];
                var car = SpawnCar(root, playerInfo.carIndex);

                // 1
                var carProperties = car.GetComponent<Gameplay.CarProperties>();
                assert(carProperties != null, "The car prefab must contain a `CarProperties` component");
                var infoComponent = car.GetComponent<CarInfoComponent>();

                // 2
                CarSpecInfo carSpec = GetEngineSpecFromStatsAndTemplate(
                    currentStats: playerInfo.carDataModel.statsInfo.currentStats,
                    rates: new StatsConversionRates(),
                    template: infoComponent.template.baseSpec);
                
                // TODO:
                // The mesh renderer should be in a separate metadata component,
                // or should be accessed in a standard way (there are other ways too, via interfaces).
                ApplyColor(playerInfo.carDataModel.mainColor,
                    // messy!
                    carProperties.DataModel._infoComponent);    

                FinalizeCarPropertiesInitialization(carProperties, infoComponent, carSpec);
                
                {
                    // TODO: remove the factory, just pass adaptive input views
                    var factory = playerInfo.inputViewFactory;
                    var cameraInput = factory.CreateCameraInputView(playerIndex, carProperties);
                    var carInput = factory.CreateCarInputView(playerIndex, carProperties);
                    
                    {
                        var cameraControlGameObject = GameObject.Instantiate(_cameraControlPrefab);
                        var cameraControl = cameraControlGameObject.GetComponent<CameraControl>();
                        cameraControl.Initialize(car.transform, cameraInput);
                    }
                    {
                        var carController = car.GetComponent<CarController>();
                        carController.Initialize(carProperties, carInput);
                    }
                }
            }

            var botInfos = initInfo.botInfos;

            for (int botIndex = 0; botIndex < botInfos.Length; botIndex++)
            {
                ref var playerInfo = ref playerInfos[botIndex];

                var car = SpawnCar(root, playerInfo.carIndex);

                var carProperties = car.GetComponent<Gameplay.CarProperties>();
                assert(carProperties != null, "The car prefab must contain a `CarProperties` component");
                var infoComponent = car.GetComponent<CarInfoComponent>();

                var carSpec = infoComponent.template.baseSpec;

                FinalizeCarPropertiesInitialization(carProperties, infoComponent, carSpec);
            }
        }

        // I feel like initialization is a good cause for the code generator.
        // Adding features with this code is going to be a massive pain.
        // The initialization needs some actual thought.

        public static void FinalizeCarPropertiesInitialization(
            Gameplay.CarProperties carProperties,
            CarInfoComponent infoComponent,
            in CarSpecInfo carSpec)
        {
            // 3
            CarColliderSetupHelper.AdjustCenterOfMass(
                ref infoComponent.colliderParts, infoComponent.template.centerOfMassAdjustmentParameters);

            // 4
            {
                var carDataModel = new Gameplay.CarDataModel(carSpec, infoComponent);
                carProperties.Initialize(carDataModel);
            }
        }
    }
}
