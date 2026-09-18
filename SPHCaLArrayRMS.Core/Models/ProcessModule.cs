using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace SPHCaLArrayRMS.Core.Models
{
    public abstract class ProcessModule : INotifyPropertyChanged
    {
        public string ModuleType { get; protected set; } = string.Empty;
        public string Summary => ToString() ?? string.Empty;

        private bool _isActive;
        public bool IsActive
        {
            get => _isActive;
            set
            {
                if (_isActive == value) return;
                _isActive = value;
                OnPropertyChanged();
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            if (propertyName != nameof(Summary))
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Summary)));
        }
    }
}
