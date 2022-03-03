using Kari.Plugins.Flags;
using UnityEngine;
using UnityEngine.UI;
using Race.Garage.Generated;
using static EngineCommon.Assertions;

namespace Race.Garage
{
    public class SpendMoneyForStatsButtonManager : MonoBehaviour
    {
        [SerializeField] private UserProperties _userProperties;
        [SerializeField] private CarProperties _carProperties;
        [SerializeField] private Button _button;
        private const float _StatIncreasePerCoin = 10;
        private const int _CoinUseAtATime = 50;

        [NiceFlags]
        public enum PossibilitiesFlags
        {
            NoCarSelected = 1 << 0,
            MaxStatsReached = 1 << 1,
            NotEnoughCoins = 1 << 2,
        }
        private PossibilitiesFlags _currentFlags = 0;

        void OnEnable()
        {
            _carProperties.OnCarSelected.AddListener(OnCarSelected);
            _carProperties.OnStatsChanged.AddListener(OnStatsChanged);
            _userProperties.OnCurrencyChanged.AddListener(OnCurrencyChanged);
            _button.onClick.AddListener(TradeCoinsForStatValue);

            // Reset the flags.
            _currentFlags.Set(PossibilitiesFlags.NoCarSelected, _carProperties.IsAnyCarSelected);
            if (_carProperties.IsAnyCarSelected)
                ResetMaxStatsFlag(ref _carProperties.CurrentCarInfo);
            ResetCoinsFlag(_userProperties);

            ResetButtonInteractability();
        }

        void OnDisable()
        {
            _carProperties.OnCarSelected.RemoveListener(OnCarSelected);
            _carProperties.OnStatsChanged.RemoveListener(OnStatsChanged);
            _userProperties.OnCurrencyChanged.RemoveListener(OnCurrencyChanged);
            _button.onClick.RemoveListener(TradeCoinsForStatValue);
        }

        private void ResetMaxStatsFlag(ref CarInstanceInfo carInfo)
        {
            _currentFlags.Set(
                PossibilitiesFlags.MaxStatsReached,
                carInfo.dataModel.statsInfo.totalStatValue >= CarStatsHelper.MaxStatValue);
        }

        private void ResetCoinsFlag(UserProperties userProperties)
        {
            _currentFlags.Set(
                PossibilitiesFlags.NotEnoughCoins,
                userProperties.DataModel.currency.coins < _CoinUseAtATime);
        }

        private void ResetButtonInteractability()
        {
            _button.interactable = _currentFlags == 0;
        }

        public void OnCarSelected(CarSelectionChangedEventInfo info)
        {
            _currentFlags.Set(PossibilitiesFlags.NoCarSelected, info.currentIndex < 0);

            if (info.currentIndex >= 0)
                ResetMaxStatsFlag(ref info.CurrentCarInfo);

            ResetButtonInteractability();
        }

        public void OnCurrencyChanged(UserPropertyChangedEventInfo<Currency> info)
        {
            ResetCoinsFlag(info.userProperties);
            ResetButtonInteractability();
        }

        public void OnStatsChanged(CarStatsChangedEventInfo info)
        {
            // Not implemented, because the additional value changes nowhere else but here.
        }

        public void TradeCoinsForStatValue()
        {
            assert(_currentFlags == 0, "You can't trust the button??");
            ref var coins = ref _userProperties.DataModel.currency.coins;
            assert(coins >= _CoinUseAtATime);
            coins -= _CoinUseAtATime;
            _userProperties.TriggerCurrencyChanged();

            assert(_carProperties.IsAnyCarSelected);
            ref var statsInfo = ref _carProperties.CurrentCarInfo.dataModel.statsInfo;
            statsInfo.additionalStatValue += _StatIncreasePerCoin;
            statsInfo.ComputeNonSerializedProperties();
            _carProperties.TriggerStatsChangedEvent();
        }
    }
}