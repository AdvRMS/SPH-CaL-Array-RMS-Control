namespace SPHCaLArrayRMS.Core.Models
{
    public sealed class LoopStartModule : ProcessModule
    {
        private int _loopCount = 2;

        public int LoopCount
        {
            get => _loopCount;
            set
            {
                _loopCount = Math.Clamp(value, 1, 9999);
                OnPropertyChanged();
            }
        }

        public LoopStartModule()
        {
            ModuleType = "Loop Start";
        }

        public override string ToString()
        {
            return $"Repeat {LoopCount} times";
        }
    }
}
