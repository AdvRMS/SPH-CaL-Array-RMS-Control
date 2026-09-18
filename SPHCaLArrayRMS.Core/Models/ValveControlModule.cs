namespace SPHCaLArrayRMS.Core.Models
{
    public sealed class ValveControlModule : ProcessModule
    {
        private ValveChannel _channel = ValveChannel.D1;
        private ValveAction _action = ValveAction.Close;

        public ValveChannel Channel
        {
            get => _channel;
            set
            {
                _channel = value;
                OnPropertyChanged();
            }
        }

        public ValveAction Action
        {
            get => _action;
            set
            {
                _action = value;
                OnPropertyChanged();
            }
        }

        public int RelayNumber => (int)Channel + 1;

        public ValveControlModule()
        {
            ModuleType = "Valve Control";
        }

        public override string ToString()
        {
            return $"{Channel} | {Action}";
        }
    }
}
