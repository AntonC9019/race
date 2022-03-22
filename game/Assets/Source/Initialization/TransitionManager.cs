using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Race.Gameplay;
using Race.Garage;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceLocations;
using static EngineCommon.Assertions;

// for now
using BotDriverInfo = Race.Gameplay.DriverInfo;

using LocationsHandle = UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationHandle<System.Collections.Generic.IList<UnityEngine.ResourceManagement.ResourceLocations.IResourceLocation>>;

namespace Race.SceneTransition
{
    public class TransitionManager : MonoBehaviour, ITransitionFromGarageToGameplay
    {
        private const string GameplayLabel = "gameplay";
        private const string TracksLabel = "track";

        // These are not scenes, but gameobjects.
        // They act like scenes, but I'm using prefabs instead of actual scenes,
        // because scenes have no benefits over prefabs as far as I can tell.
        [SerializeField] private AssetReferenceGameObject _garageScenePrefab;
        [SerializeField] private AssetReferenceGameObject _gameplayScenePrefab;
        [SerializeField] private AssetReferenceGameObject _garageToGameplayTransitionScenePrefab;

        private Transform _garageTransform;
        private Transform _gameplayTransform;
        private Transform _garageToGameplayTransitionTransform;
        private IEnableDisableInput _enableDisableInput;

        
        /*
            In garage, button (GO! sort of button) ->
            Hide garage (I guess disable the root?), put on the loading screen ->
            
            The next operations can be done in any order:
            - Get the data needed to initialize the gameplay scene (user data model, car data model),
              extract only the relevant bits.
            - Asynchronously load the needed prefabs.
            - Create empty game object for the gameplay scene root.


            *This step has to be done in an assembly that knows of both the garage and the gameplay*. 

            Once the car prefabs have been loaded (this step is implemented):
            - For players, configure the car spec based on the stats.
            - For all, set up the colliders and other stuff.


            Now, hide the loading screen, and let the gameplay scene take control.
            So, find the object tagged with "Initialization" in the instantiated prefab,
            and let it do the rest.
            I think it should be the one to set up the inputs views, because, actually,
            the inputs shouldn't be activated immediately???

            Could do something fancier when I understand the problem better.
        */

        async void Start()
        {
            await InitializeGarage();
            Debug.Log("Done loading garage");
        }

        private static GameObject Instantiate_RunAwakes_DisableUpdates(GameObject prefab)
        {
            assert(prefab != null, "The prefab must not be null");

            var gameObject = GameObject.Instantiate(prefab);
            // Make sure all the Awakes have run.
            gameObject.SetActive(true);
            // Make sure no Updates run.
            gameObject.SetActive(false);

            return gameObject;
        }

        // private readonly struct Temp<TInitializationComponent>
        // {
        //     public readonly GameObject rootTransform;
        //     public readonly TInitializationComponent initializationComponent;

        //     public Temp(GameObject rootTransform, TInitializationComponent initializationComponent)
        //     {
        //         this.rootTransform = rootTransform;
        //         this.initializationComponent = initializationComponent;
        //     }
        // }

        // The gameobject returned is deactivated.
        // Tasks are not supported in WebGL, so might want to refactor this to use coroutines.
        // https://docs.unity3d.com/Packages/com.unity.addressables@1.9/manual/AddressableAssetsAsyncOperationHandle.html
        private async Task<(Transform, TInitializationComponent)> InstantiatePrefabAndFindInitializationObject<TInitializationComponent>(
            AssetReferenceGameObject prefabReference)
        {
            var prefab = (GameObject) prefabReference.Asset;
            // .Asset already does the check
            if (prefab == null)
                prefab = await prefabReference.LoadAssetAsync().Task;

            var gameObject = Instantiate_RunAwakes_DisableUpdates(prefab);

            var transform = gameObject.transform;
            
            var initializationTransform = FindInitializationTransform(transform);
            var initializationComponent = initializationTransform.GetComponent<TInitializationComponent>();

            return (transform, initializationComponent);
        }

        private async Task InitializeGarage()
        {
            assert(_garageScenePrefab != null);
            assert(_garageTransform == null);

            var (garageTransform, initializationComponent) = await InstantiatePrefabAndFindInitializationObject<IGarageInitialize>(_garageScenePrefab);
            _garageTransform = garageTransform;

            await initializationComponent.Initialize(new GarageInitializationInfo(this));

            // Now allow updates to run
            garageTransform.gameObject.SetActive(true);
        }

        public async Task TransitionFromGarageToGameplay(GarageToGameplayTransitionInfo info)
        {
            assert(_garageTransform != null);
            assert(_gameplayScenePrefab != null);
            assert(_garageToGameplayTransitionScenePrefab != null);

            _garageTransform.gameObject.SetActive(false);

            // This is stupid and I hate it ...
            // Addressables' API is terrible IMO. I'd do a custom thing and be happy.
            // Their code is complicated and unreadable too.
            LocationsHandle gameplayCarsLocationsHandle = Addressables.LoadResourceLocationsAsync(GameplayLabel);

            /*
                I'd do something like the following:
                - Refer to anything by the group prefix, and their index in that group.
                - Each group would have a metadata section, where the names can be mapped to indices if needed,
                  and any other metadata would be stored.
                  I think you'd still have to give some sort of "description" to the things that a bundle
                  or a server contains.
                  That section should be able to contain anything, perhaps even like smaller car models,
                  so that the player could browse the cars without loading in whole detailed meshes.
                - When loading the things, you must first collect the groups that you will be downloading from,
                  which I guess should be known statically, or retrieved dynamically. If it's known statically,
                  you can just get the needed group names synchronously.
                  Then you'd load all things from the groups in bulk again.
                  Once they have been loaded, you can just index directly into them.

                So I guess I'd like a lower-level abstraction better than Addressables.
                The lazy-loading of every object individually really gets to me.
                I guess I just don't think in objects, but in data.

                Like I would rather group the objects I need to query by their group name manually, than
                downloading each one individually and handing all of that off to some magical system.
                I would be much more efficient too.

                I might be missing something at this point too tho.
            */
            static async Task<GameObject> GetPrefabByIndex(
                int index, LocationsHandle locationsHandle)
            {
                var locations = await locationsHandle.Task;
                var correctLocation = locations[index];
                // LoadAssetAsync already uses caching internally.
                var prefabHandle = Addressables.LoadAssetAsync<GameObject>(correctLocation);
                return await prefabHandle.Task;
            }

            static async Task<GameObject> InstantiateAsyncByIndex(int index, LocationsHandle locationsHandle)
            {
                // This is again just stupid, because currently WE KNOW the cars are stored in the same bundle.
                // So they will always resolve instantly after the locations have been loaded.
                // We could've just iterated them manually at that point.
                var prefab = await GetPrefabByIndex(index, locationsHandle);
                var thing = Instantiate_RunAwakes_DisableUpdates(prefab);
                return thing;
            }

            static async Task<(GameObject, Gameplay.CarProperties, CarInfoComponent)> CreateCarAndGetSomeComponents(
                int carIndex, LocationsHandle locationsHandle)
            {
                GameObject car = await InstantiateAsyncByIndex(carIndex, locationsHandle);

                var carProperties = car.GetComponent<Gameplay.CarProperties>();
                assert(carProperties != null, "The car prefab must contain a `CarProperties` component");
                var infoComponent = car.GetComponent<CarInfoComponent>();

                return (car, carProperties, infoComponent);
            }

            Task<DriverInfo>[] playerTasks;
            {
                var playerCount = info.playerInfos.Length;
                assert(playerCount == 1);

                playerTasks = new Task<DriverInfo>[playerCount];
                for (int i = 0; i < playerTasks.Length; i++)
                {
                    var task = CreateCar(info.playerInfos[i], gameplayCarsLocationsHandle);
                    playerTasks[i] = task;

                    static async Task<DriverInfo> CreateCar(PlayerInfo playerInfo, LocationsHandle locationsHandle)
                    {
                        var (car, carProperties, infoComponent) = 
                            await CreateCarAndGetSomeComponents(playerInfo.carIndex, locationsHandle);

                        // static void InitializeCarPropertiesFromPlayerInfo(
                        //     in PlayerInfo playerInfo,
                        //     Gameplay.CarProperties carProperties,
                        //     Gameplay.CarInfoComponent infoComponent)
                        {
                            // 2
                            CarSpecInfo carSpec = GetEngineSpecFromStatsAndTemplate(
                                currentStats: playerInfo.carDataModel.statsInfo.currentStats,
                                rates: new StatsConversionRates(),
                                template: infoComponent.template.baseSpec);

                            // TODO:
                            // The mesh renderer should be in a separate metadata component,
                            // or should be accessed in a standard way (there are other ways too, via interfaces).
                            ApplyColor(playerInfo.carDataModel.mainColor, infoComponent);
                            Gameplay.InitializationHelper.FinalizeCarPropertiesInitialization(carProperties, infoComponent, car.transform, carSpec);
                            
                            static void ApplyColor(Color color, CarInfoComponent infoComponent)
                            {
                                infoComponent.visualParts.meshRenderer.material.color = color;
                            }
                        }

                        return new DriverInfo(car, carProperties);
                    }
                }
            }

            Task<BotDriverInfo>[] botTasks;
            {
                var botCount = info.botInfos.Length;
                assert(botCount == 1);

                botTasks = new Task<BotDriverInfo>[botCount];
                for (int i = 0; i < botTasks.Length; i++)
                {
                    var task = CreateCar(info.botInfos[i], gameplayCarsLocationsHandle);
                    botTasks[i] = task;

                    static async Task<BotDriverInfo> CreateCar(BotInfo botInfo, AsyncOperationHandle<IList<IResourceLocation>> locationsHandle)
                    {
                        var (car, carProperties, infoComponent) = 
                            await CreateCarAndGetSomeComponents(botInfo.carIndex, locationsHandle);

                        Gameplay.InitializationHelper.FinalizeCarPropertiesInitializationWithDefaults(carProperties, infoComponent, car.transform);

                        return new BotDriverInfo(car, carProperties);
                    }
                }
            }

            GameObject trackMap;
            {
                LocationsHandle tracksLocationsHandle = Addressables.LoadResourceLocationsAsync(TracksLabel);
                // TODO: do the awaits simultaneously.
                trackMap = await InstantiateAsyncByIndex(info.trackIndex, tracksLocationsHandle);
                // TODO: This should be a nicely displayed error, not an assertion.
                assert(trackMap != null);
            }
            
            {
                var (gameplaySceneRoot, initializationComponent) = 
                    await InstantiatePrefabAndFindInitializationObject<IGameplayInitialization>(_gameplayScenePrefab);
                _gameplayTransform = gameplaySceneRoot;

                var playerDriverInfos = await Task.WhenAll(playerTasks);
                var botDriverInfos = await Task.WhenAll(botTasks);
                
                var initializationInfo = new GameplayExternalInitializationInfo
                {
                    botInfos = botDriverInfos,
                    playerInfos = playerDriverInfos,
                    mapGameObject = trackMap,
                    rootTransform = gameplaySceneRoot,
                };

                var enableDisableInput = initializationComponent.Initialize(initializationInfo);
                enableDisableInput.EnableAllInput();

                _enableDisableInput = enableDisableInput;

                gameplaySceneRoot.gameObject.SetActive(true);
            }
        }


        [System.Serializable]
        public struct StatsConversionRates
        {
            // for now keep it const
            public const float c_torqueFactor = 1.0f;
            public readonly float torqueFactor => c_torqueFactor;
        }

        private static CarSpecInfo GetEngineSpecFromStatsAndTemplate(
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
            
            // I'm not sure this formula is correct, but it seems so.
            foreach (ref var g in carSpec.transmission.gearRatios.AsSpan())
                g *= motorRPMAtOldTorque / template.engine.optimalRPM;

            return carSpec;
        }

        private Transform FindInitializationTransform(Transform root)
        {
            return root.Find("initialization");
        }
    }
}