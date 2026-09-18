namespace SPHCaLArrayRMS.Core.Models
{
    public sealed class HeaterControlModule : ProcessModule
    {
        private int _channel = 1;
        private int _powerWatts;
        private SwitchAction _action = SwitchAction.Off;
        private double _durationInSeconds;

        public int Channel
        {
            get => _channel;
            set
            {
                _channel = Math.Clamp(value, 1, 4);
                OnPropertyChanged();
            }
        }

        public int PowerWatts
        {
            get => _powerWatts;
            set
            {
                _powerWatts = Math.Clamp(value, 0, ushort.MaxValue);
                OnPropertyChanged();
            }
        }

        public SwitchAction Action
        {
            get => _action;
            set
            {
                _action = value;
                OnPropertyChanged();
            }
        }

        public double DurationInSeconds
        {
            get => _durationInSeconds;
            set
            {
                _durationInSeconds = Math.Max(0, Math.Round(value, 2, MidpointRounding.AwayFromZero));
                OnPropertyChanged();
            }
        }

        public HeaterControlModule()
        {
            ModuleType = "Heater Control";
        }

        public override string ToString()
        {
            return $"CH{Channel} | {PowerWatts} W | {Action} | {DurationInSeconds:F2} s";
        }
    }
}
