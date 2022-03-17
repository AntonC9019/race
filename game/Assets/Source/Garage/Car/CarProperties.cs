using System.Collections.Generic;
using System.IO;
using System.Xml.Serialization;
using EngineCommon.ColorPicker;
using Kari.Plugins.DataObject;
using Kari.Plugins.Flags;
using UnityEngine;
using TMPro;
using static EngineCommon.Assertions;
using UnityEngine.Events;
using System;

namespace Race.Garage
{
    /// <summary>
    /// Stores the actual source of truth data associated with the car.
    /// Other systems should synchronize to this data.
    /// Provides no means of actually setting the values, for that use `CarProperties`.
    /// </summary>
    [System.Serializable]
    [DataObject]
    public partial class CarDataModel
    {
        /// <summary>
        /// </summary>
        public Color mainColor;

        /// <summary>
        /// </summary>
        public CarStatsInfo statsInfo;

        /// <summary>
        /// Use this for serialization for now, but it should be replaced by a typesafe function
        /// generated via Kari.
        /// </summary>
        public readonly static XmlSerializer Serializer = new XmlSerializer(typeof(CarDataModel));

        // I'm not using notifyPropertyChanged or whatnot, because I want to have more control
        // over these things. I love declarative programming, but I want to do it with my
        // code generator when possible to have max control over things.
    }

    /// <summary>
    /// Information needed to implement the data binding between the car model
    /// and the widgets providing the interaction.
    /// This info is provided by the prefabs to aid in the discovery of the needed
    /// components in the prefabs.
    /// </summary>
    [System.Serializable]
    public struct DisplayCarInfo
    {
        public MeshRenderer meshRenderer;
        public string name;

        /// <summary>
        /// Base, unchangeable stats of the car.
        /// The values cannot go below these values and are purely additive on top.
        /// </summary>
        public CarStats baseStats;
    }

    [NiceFlags]
    public enum CarPrefabInfoFlags
    {
        /// <summary>
        /// Indicates whether the prefab can be used directly as is,
        /// or whether it should be spawned, and then reused.
        /// </summary>
        IsPrespawnedBit = 1 << 0,
    }

    [System.Serializable]
    public struct CarPrefabInfo
    {
        // public CarPrefabInfoFlags flags;

        /// <summary>
        /// Contains the car's prefabs.
        /// Must have a `DisplayCarInfoComponent`, via which we would get its mesh renderer.
        /// These are allowed to already exist in the scene, in which case they will not be duplicated.
        /// </summary>
        public GameObject prefab;
    }

    public struct CarInstanceInfo
    {
        // TODO:
        // I think these should be on the data model.
        // Then the data model could have further distinction between the different
        // kinds of data, like mutable/immutable, things that only exist in the current scene
        // vs normal data.

        /// <summary>
        /// </summary>
        public string name;
        
        public Material mainMaterial;
        public CarDataModel dataModel;
        public GameObject rootObject;
    }

    // TODO: I assume this one is going to be boxed, so it being a struct
    // should produce even more garbage than it being a class, but this needs profiling.
    /// <summary>
    /// Info passed when a car selection changes.
    /// </summary>
    public readonly struct CarSelectionChangedEventInfo
    {
        /// <summary>
        /// The CarProperties that raised the event.
        /// </summary>
        public readonly CarProperties carProperties;

        /// <summary>
        /// The index of the deselected car.
        /// Access `carProperties` to get the car model associated with it.
        /// Will be -1 if the car is the first one selected.
        /// </summary>
        public readonly int previousIndex;
        
        /// <summary>
        /// The index of the selected car.
        /// Will be -1 in case the car got deselected.
        /// </summary>
        public readonly int currentIndex;

        public CarSelectionChangedEventInfo(CarProperties carProperties, int previousIndex, int currentIndex)
        {
            this.carProperties = carProperties;
            this.previousIndex = previousIndex;
            this.currentIndex = currentIndex;
        }

        public ref CarInstanceInfo PreviousCarInfo => ref carProperties.GetCarInfo(previousIndex);
        public ref CarInstanceInfo CurrentCarInfo => ref carProperties.GetCarInfo(currentIndex);
    }

    // TODO:
    // It is possible to pass these by readonly reference with some shenanigans with pointers,
    // which can be hidden under autogenerated wrappers.
    // Then the events will take these wrappers instead of the actual types.
    // The wrapper will contain a pointer to the actual struct, which itself will live on the stack.
    // It will expose its properties via autogenerated getters.
    // This way there will always be ZERO ALLOCATIONS for the event info objects, and it's not hard to do either.
    // I can do this in a day probably, with the help of my code generator.
    //
    // See an example of the shenanigans in the `/concepts/Stackref` folder of the project.
    // For Unity, that will also require this package (leaving it here for future reference):
    // https://www.nuget.org/packages/System.Runtime.CompilerServices.Unsafe/ 

    public readonly struct CarStatsChangedEventInfo
    {
        /// <summary>
        /// The CarProperties that raised the event.
        /// </summary>
        public readonly CarProperties carProperties;

        /// <summary>
        /// The index (id) of the stat that has changed.
        /// The index of -1 means all stats have changed.
        /// </summary>
        public readonly int statIndex;

        public bool HaveAllStatsChanged => statIndex < 0;

        public CarStatsChangedEventInfo(CarProperties carProperties, int statIndex)
        {
            this.carProperties = carProperties;
            this.statIndex = statIndex;
        }

        public ref CarStatsInfo CurrentStatsInfo => ref carProperties.CurrentCarInfo.dataModel.statsInfo;
    }

    /// <summary>
    /// Provides data binding between the currently selected car and the other systems
    /// that need to get notified when that data changes.
    /// </summary>
    public class CarProperties : MonoBehaviour
    {
        // TODO: codegen stuff.

        [ContextMenuItem("Delete save files", nameof(DeleteSaveFiles))]
        [SerializeField] internal CarPrefabInfo[] _carPrefabInfos;

        /// <summary>
        /// </summary>
        private CarInstanceInfo[] _carInstanceInfos;

        // We need this one in the bridge script.
        public CarInstanceInfo[] CarInstanceInfos => _carInstanceInfos;

        /// <summary>
        /// </summary>
        public ref CarInstanceInfo GetCarInfo(int index)
        {
            assert(CurrentCarIndex >= 0 && CurrentCarIndex < _carInstanceInfos.Length);
            return ref _carInstanceInfos[index];
        }

        /// <summary>
        /// </summary>
        public ref CarInstanceInfo CurrentCarInfo => ref GetCarInfo(CurrentCarIndex);

        private int _currentSelectionIndex = -1;
        private bool _currentIsDirty;

        /// <summary>
        /// Returns -1 if no car is currently selected.
        /// </summary>
        public int CurrentCarIndex => _currentSelectionIndex;

        /// <summary>
        /// Check this before accessing <c>CurrentCarInfo</c>.
        /// </summary>
        public bool IsAnyCarSelected => _currentSelectionIndex >= 0;

        // For now, to avoid creating tiny scripts that do almost nothing,
        // just reference the color picker and the car mesh renderer here,
        // but when the property count grows, events can be used to decouple things.
        // I'm doing the simple thing here for now, it's not necessarily scalable
        // in the long run.
        // TODO: Could also consider the mesh renderer as the source of truth,
        // it's not clear what I want yet anyway.
        //
        // For example, here, we could use a bridge script that would listen to the picker events,
        // set the color here, which would notify it back, which it should ignore.
        // And the other way, when the data changes here, it would update it on the picker, and ignore
        // the event coming from that.
        [SerializeField] private CUIColorPicker _colorPicker;
        
        /// <summary>
        /// </summary>
        [SerializeField] public UnityEvent<CarSelectionChangedEventInfo> OnCarSelected;
        
        /// <summary>
        /// </summary>
        [SerializeField] public UnityEvent<CarStatsChangedEventInfo> OnStatsChanged;

        /// <summary>
        /// </summary>
        [SerializeField] public UnityEvent<CarProperties> OnCarsInitialized;

        void Awake()
        {
            assert(_carPrefabInfos is not null);
            _carInstanceInfos = new CarInstanceInfo[_carPrefabInfos.Length];

            for (int i = 0; i < _carPrefabInfos.Length; i++)
            {
                var prefabInfo = _carPrefabInfos[i];
                assert(prefabInfo.prefab != null);

                // TODO:
                // somehow store metadata outside the prefab??
                // Perhaps it is possible to do with addressables??
                var infoComponent = prefabInfo.prefab.GetComponent<DisplayCarInfoComponent>();
                assert(infoComponent != null);

                // if (prefabInfo.flags.HasFlag(CarPrefabInfoFlags.IsPrespawnedBit))
                if (!IsPrefab(prefabInfo.prefab))
                {
                    prefabInfo.prefab.SetActive(false);
                    ResetInstanceInfo(ref _carInstanceInfos[i], infoComponent, prefabInfo.prefab);
                }

                var name = infoComponent.info.name;

                _carInstanceInfos[i].name = name;
            }


            // Check whether the object is a prefab or an instantiated object in the scene or not.
            // This check feels like a hack, but it seems reliable.
            static bool IsPrefab(GameObject gameObject)
            {
                return gameObject.scene.name is null;
            }

            assert(_colorPicker != null);
            _colorPicker.OnValueChangedEvent.AddListener(OnPickerColorSet);
        }

        void Start()
        {
            InvokeCarsInitialized();
            // if (IsAnyCarSelected)
                // ResetModelWithCarDataMaybeFromFile(ref CurrentCarInfo);
        }

        private void InvokeCarsInitialized()
        {
            OnCarsInitialized.Invoke(this);
            OnCarsInitialized.RemoveAllListeners();
        }

        internal void TriggerStatsChangedEvent(int statChangedIndex = -1)
        {
            var info = new CarStatsChangedEventInfo(
                carProperties: this,
                statIndex: statChangedIndex);
            OnStatsChanged.Invoke(info);
            _currentIsDirty = true;
        }

        private void ResetInstanceInfo(ref CarInstanceInfo instanceInfo,
            DisplayCarInfoComponent infoComponent, GameObject carInstance)
        {
            assert(infoComponent != null);
            assert(infoComponent.info.meshRenderer != null);
            assert(carInstance != null);

            ref var info = ref infoComponent.info;

            var material = info.meshRenderer.material;
            assert(material != null);

            var statsInfo = new CarStatsInfo
            {
                baseStats    = info.baseStats,
                currentStats = info.baseStats,
                additionalStatValue = 0,
            };
            statsInfo.ComputeNonSerializedProperties();

            instanceInfo.dataModel = new CarDataModel
            {
                mainColor = material.color,
                statsInfo = statsInfo,
            };
            instanceInfo.mainMaterial = material;
            instanceInfo.rootObject = carInstance;
        }

        // TODO: might want to customize the way we get the file path.
        public static string GetSaveFilePath(string carName)
        {
            return Application.persistentDataPath + "/cardata_" + carName + ".xml";
        }

        internal void ResetModelWithCarDataMaybeFromFile(ref CarInstanceInfo info)
        {
            // Let's say the object's name is how we store the data.
            // Let's say we store it in XML for now.
            // TODO: might be worth it to pack the whole array in a single file.

            var dataFullFilePath = GetSaveFilePath(info.name);

            if (File.Exists(dataFullFilePath))
            // if (false)
            {
                print(dataFullFilePath);
                using var textReader = new StreamReader(dataFullFilePath);
                // The `Deserialize` cannot keep the existing values wherever
                // a value for a field was not found. It's a pretty stupid API design tbh.
                // TODO: generate a typesafe serialization function.
                // TODO: don't read the file if it hasn't changed.
                // TODO: maybe watch the asset and hotreload it.
                // TODO: check if the stats are valid?
                info.dataModel = (CarDataModel) CarDataModel.Serializer.Deserialize(textReader);

                // TODO: 
                // may want to encapsulate this in the struct,
                // but meh, working with pure data is more comfortable.
                info.dataModel.statsInfo.ComputeNonSerializedProperties();
            }

            ResetModel(ref info);

            // TODO: This is a little bit messy without the bridge handlers.
            void ResetModel(ref CarInstanceInfo info)
            {
                info.mainMaterial.color = info.dataModel.mainColor;
                _colorPicker.ColorRGB = info.dataModel.mainColor;
                // The callbacks are fired in the method below.
            }
        }

        private static void WriteModel(CarDataModel model, string fileName)
        {
            using var textWriter = new StreamWriter(fileName);
            CarDataModel.Serializer.Serialize(textWriter, model);
        }

        private void MaybeWriteCurrentModel()
        {
            if (_currentIsDirty)
            {
                var model = CurrentCarInfo.dataModel;
                var fullFilePath = GetSaveFilePath(CurrentCarInfo.name);
                WriteModel(model, fullFilePath);
                _currentIsDirty = false;
            }
        }

        public readonly struct PropertySetContext
        {
            public readonly CarInstanceInfo info;
            
            // TODO: Should be a codegened enum probably 
            public readonly string nameOfPropertyThatChanged;
        }

        /// <summary>
        /// Callback used for the color picker.
        /// </summary>
        public void OnPickerColorSet(Color color)
        {
            if (!IsAnyCarSelected)
                return;

            ref var info = ref CurrentCarInfo;
            var model = info.dataModel;

            if (model.mainColor != color)
            {
                _currentIsDirty = true;
                model.mainColor = color;

                if (info.mainMaterial != null)
                    info.mainMaterial.color = model.mainColor;

                // Currently, the only source that the data comes from is the color picker,
                // so i'm just not resetting it here.
                // But with an event system, that would be the responsibility of a bridge script.

                // TODO: fire callbacks.
                // PropertySetContext context
                // context.model = _currentModel;
                // context.nameOfPropertyThatChanged = nameof(CarDataModel.mainColor);
            }
        }

        /// <summary>
        /// </summary>
        public void SelectCar(int carIndex)
        {
            assert(carIndex >= -1);
            if (CurrentCarIndex == carIndex)
                return;
            
            var eventInfo = new CarSelectionChangedEventInfo(
                carProperties : this,
                previousIndex : CurrentCarIndex,
                currentIndex : carIndex);

            if (IsAnyCarSelected)
            {
                MaybeWriteCurrentModel();
                
                assert(CurrentCarInfo.rootObject != null);
                CurrentCarInfo.rootObject.SetActive(false);
            }

            assert(carIndex < _carInstanceInfos.Length);
            _currentSelectionIndex = carIndex;

            // The none option (at index 0) is just deselecting.
            if (carIndex >= 0)
            {
                ref var carInfo = ref CurrentCarInfo;

                if (carInfo.dataModel is null)
                {
                    ref var prefabInfo = ref _carPrefabInfos[CurrentCarIndex];
                    
                    var carGameObject = GameObject.Instantiate(prefabInfo.prefab);
                    // carGameObject.SetActive(false);
                    carGameObject.transform.SetParent(transform, worldPositionStays: false);

                    var infoComponent = carGameObject.GetComponent<DisplayCarInfoComponent>();
                    ResetInstanceInfo(ref carInfo, infoComponent, carGameObject);
                }
                ResetModelWithCarDataMaybeFromFile(ref carInfo);

                carInfo.rootObject.SetActive(true);
            }

            OnCarSelected.Invoke(eventInfo);
        }

        private void DeleteSaveFiles()
        {
            foreach (var prefabInfo in _carPrefabInfos)
            {
                var displayInfo = prefabInfo.prefab.GetComponent<DisplayCarInfoComponent>().info;
                var saveFileFullPath = GetSaveFilePath(displayInfo.name);
                if (File.Exists(saveFileFullPath))
                {
                    print("Deleting " + saveFileFullPath);
                    File.Delete(saveFileFullPath);
                }
            }
        }
        
        // TODO:
        // Some tool that would find the needed `UserProperties` automatically in the editor
        // when this object is added and hook it up automatically, without delegating to runtime,
        // and without serializing the reference to `UserProperties`. Thus we'd get some of the benefits
        // of singletons, while not actually making these global.
        // This should definitely in some way get hooked up automatically.
        public void OnUserModelLoaded(UserDataModel model)
        {
            InvokeCarsInitialized();
            for (int i = 0; i < CarInstanceInfos.Length; i++)
            {
                if (CarInstanceInfos[i].name == model.defaultCarName)
                {
                    SelectCar(i);
                    break;
                }
            }
        }
    }
}