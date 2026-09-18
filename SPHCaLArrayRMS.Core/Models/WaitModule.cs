namespace SPHCaLArrayRMS.Core.Models
{
    public sealed class WaitModule : ProcessModule
    {
        private double _durationInSeconds = 1.00;

        public double DurationInSeconds
        {
            get => _durationInSeconds;
            set
            {
                _durationInSeconds = Math.Max(0, Math.Round(value, 2, MidpointRounding.AwayFromZero));
                OnPropertyChanged();
            }
        }

        public WaitModule()
        {
            ModuleType = "Wait";
        }

        public override string ToString()
        {
            return $"{DurationInSeconds:F2} s";
        }
    }
}
