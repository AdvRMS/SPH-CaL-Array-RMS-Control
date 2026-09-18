using Microsoft.Win32;
using Newtonsoft.Json;
using SPHCaLArrayRMS.Core.Models;
using SPHCaLArrayRMS.Core.Services;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace SPHCaLArrayRMS.App.ViewModels
{
    public sealed class MainViewModel : INotifyPropertyChanged
    {
        private readonly ProcessExecutor _executor = new();
        private readonly DispatcherTimer _clockTimer = new();
        private ProcessModule? _selectedModule;
        private string _statusText = "Idle";
        private int _globalLoopCount = 1;
        private string _currentGlobalLoopText = "Global Loop: 0/1";
        private string _elapsedText = "Elapsed: 00:00:00";
        private string _remainingText = "Remaining: 00:00:00";
        private string _estimatedEndTimeText = "ETA: --:--:--";
        private bool _isProcessIdle = true;
        private DateTime? _runStartedAt;
        private DateTime? _estimatedEndAt;
        private TimeSpan _plannedDuration;

        public ObservableCollection<ProcessModule> ProcessModules { get; } = new();
        public ObservableCollection<string> EventLog { get; } = new();
        public int[] HeaterChannels { get; } = { 1, 2, 3, 4 };
        public SwitchAction[] SwitchActions { get; } = Enum.GetValues<SwitchAction>();
        public ValveChannel[] ValveChannels { get; } = Enum.GetValues<ValveChannel>();
        public ValveAction[] ValveActions { get; } = Enum.GetValues<ValveAction>();

        public ProcessModule? SelectedModule
        {
            get => _selectedModule;
            set => SetProperty(ref _selectedModule, value);
        }

        public string StatusText
        {
            get => _statusText;
            private set => SetProperty(ref _statusText, value);
        }

        public int GlobalLoopCount
        {
            get => _globalLoopCount;
            set => SetProperty(ref _globalLoopCount, Math.Clamp(value, 1, 9999));
        }

        public string CurrentGlobalLoopText
        {
            get => _currentGlobalLoopText;
            private set => SetProperty(ref _currentGlobalLoopText, value);
        }

        public string ElapsedText
        {
            get => _elapsedText;
            private set => SetProperty(ref _elapsedText, value);
        }

        public string RemainingText
        {
            get => _remainingText;
            private set => SetProperty(ref _remainingText, value);
        }

        public string EstimatedEndTimeText
        {
            get => _estimatedEndTimeText;
            private set => SetProperty(ref _estimatedEndTimeText, value);
        }

        public bool IsProcessIdle
        {
            get => _isProcessIdle;
            private set => SetProperty(ref _isProcessIdle, value);
        }

        public bool IsValveConnected => HardwareContext.ValveBoard.IsConnected;
        public bool IsHeaterConnected => HardwareContext.HeaterBoard.IsConnected;
        public string ValveConnectionText => IsValveConnected ? "Disconnect" : "Connect";
        public string HeaterConnectionText => IsHeaterConnected ? "Disconnect" : "Connect";

        public ICommand AddModuleCommand { get; }
        public ICommand MoveUpCommand { get; }
        public ICommand MoveDownCommand { get; }
        public ICommand DeleteCommand { get; }
        public ICommand ClearCommand { get; }
        public ICommand SaveCommand { get; }
        public ICommand LoadCommand { get; }
        public ICommand RunCommand { get; }
        public ICommand StopCommand { get; }
        public ICommand ToggleValveConnectionCommand { get; }
        public ICommand ToggleHeaterConnectionCommand { get; }

        public MainViewModel()
        {
            DiagnosticLogger.LogReceived += OnLogReceived;
            _clockTimer.Interval = TimeSpan.FromMilliseconds(200);
            _clockTimer.Tick += (_, _) => UpdateTimingDisplay();

            _executor.StepChanged += OnStepChanged;
            _executor.StatusChanged += status => Application.Current.Dispatcher.Invoke(() => StatusText = status);
            _executor.GlobalLoopChanged += (current, total) =>
                Application.Current.Dispatcher.Invoke(() => CurrentGlobalLoopText = $"Global Loop: {current}/{total}");

            AddModuleCommand = new RelayCommand(AddModule);
            MoveUpCommand = new RelayCommand(_ => MoveSelected(-1), _ => CanMove(-1));
            MoveDownCommand = new RelayCommand(_ => MoveSelected(1), _ => CanMove(1));
            DeleteCommand = new RelayCommand(_ => DeleteSelected(), _ => SelectedModule != null);
            ClearCommand = new RelayCommand(_ => ProcessModules.Clear(), _ => ProcessModules.Count > 0);
            SaveCommand = new RelayCommand(_ => SaveProcess());
            LoadCommand = new RelayCommand(_ => LoadProcess());
            RunCommand = new RelayCommand(async _ => await RunProcessAsync(), _ => !_executor.IsRunning && ProcessModules.Count > 0);
            StopCommand = new RelayCommand(_ => _executor.Stop(), _ => _executor.IsRunning);
            ToggleValveConnectionCommand = new RelayCommand(_ => ToggleValveConnection());
            ToggleHeaterConnectionCommand = new RelayCommand(_ => ToggleHeaterConnection());
        }

        private void AddModule(object? parameter)
        {
            ProcessModule? module = parameter?.ToString() switch
            {
                "Valve" => new ValveControlModule(),
                "Heater" => new HeaterControlModule(),
                "Wait" => new WaitModule(),
                "LoopStart" => new LoopStartModule(),
                "LoopEnd" => new LoopEndModule(),
                _ => null
            };
            if (module == null) return;

            int index = SelectedModule == null ? ProcessModules.Count : ProcessModules.IndexOf(SelectedModule) + 1;
            ProcessModules.Insert(index, module);
            SelectedModule = module;
        }

        private bool CanMove(int offset)
        {
            if (SelectedModule == null) return false;
            int index = ProcessModules.IndexOf(SelectedModule);
            int target = index + offset;
            return index >= 0 && target >= 0 && target < ProcessModules.Count;
        }

        private void MoveSelected(int offset)
        {
            if (!CanMove(offset) || SelectedModule == null) return;
            int index = ProcessModules.IndexOf(SelectedModule);
            ProcessModules.Move(index, index + offset);
        }

        private void DeleteSelected()
        {
            if (SelectedModule == null) return;
            int index = ProcessModules.IndexOf(SelectedModule);
            ProcessModules.Remove(SelectedModule);
            SelectedModule = ProcessModules.Count == 0 ? null : ProcessModules[Math.Min(index, ProcessModules.Count - 1)];
        }

        private async Task RunProcessAsync()
        {
            if (!CheckRequiredHardware())
                return;

            int globalLoops = GlobalLoopCount;
            double singleCycleSeconds = CalculateConfiguredDuration(ProcessModules);
            _plannedDuration = TimeSpan.FromSeconds(singleCycleSeconds * globalLoops);
            _runStartedAt = DateTime.Now;
            _estimatedEndAt = _runStartedAt.Value.Add(_plannedDuration);
            IsProcessIdle = false;
            CurrentGlobalLoopText = $"Global Loop: 0/{globalLoops}";
            UpdateTimingDisplay();
            _clockTimer.Start();
            CommandManager.InvalidateRequerySuggested();

            try
            {
                await _executor.RunAsync(ProcessModules.ToList(), globalLoops);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Process Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                _clockTimer.Stop();
                UpdateTimingDisplay();
                if (StatusText == "Completed")
                    RemainingText = "Remaining: 00:00:00";
                IsProcessIdle = true;
                CommandManager.InvalidateRequerySuggested();
            }
        }

        private bool CheckRequiredHardware()
        {
            bool needsValveBoard = ProcessModules.Any(module => module is ValveControlModule);
            bool needsHeaterBoard = ProcessModules.Any(module => module is HeaterControlModule);
            var missing = new List<string>();

            if (needsValveBoard && !IsValveConnected)
                missing.Add("Valve Relay Board");
            if (needsHeaterBoard && !IsHeaterConnected)
                missing.Add("Heater Relay Board");

            if (missing.Count == 0)
                return true;

            string message = "Connect the required hardware before running:\n\n" + string.Join("\n", missing.Select(name => $"• {name}"));
            MessageBox.Show(message, "Hardware Connection Required", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        private static double CalculateConfiguredDuration(IEnumerable<ProcessModule> modules)
        {
            double currentDuration = 0;
            var stack = new Stack<(double ParentDuration, int Count)>();

            foreach (ProcessModule module in modules)
            {
                if (module is HeaterControlModule heater)
                {
                    currentDuration += Math.Max(0, heater.DurationInSeconds);
                }
                else if (module is WaitModule wait)
                {
                    currentDuration += Math.Max(0, wait.DurationInSeconds);
                }
                else if (module is LoopStartModule loopStart)
                {
                    stack.Push((currentDuration, Math.Max(1, loopStart.LoopCount)));
                    currentDuration = 0;
                }
                else if (module is LoopEndModule && stack.Count > 0)
                {
                    var frame = stack.Pop();
                    currentDuration = frame.ParentDuration + currentDuration * frame.Count;
                }
            }

            return currentDuration;
        }

        private void UpdateTimingDisplay()
        {
            if (!_runStartedAt.HasValue)
            {
                ElapsedText = "Elapsed: 00:00:00";
                RemainingText = "Remaining: 00:00:00";
                EstimatedEndTimeText = "ETA: --:--:--";
                return;
            }

            TimeSpan elapsed = DateTime.Now - _runStartedAt.Value;
            TimeSpan remaining = _plannedDuration - elapsed;
            if (remaining < TimeSpan.Zero) remaining = TimeSpan.Zero;

            ElapsedText = $"Elapsed: {FormatDuration(elapsed)}";
            RemainingText = $"Remaining: {FormatDuration(remaining)}";
            EstimatedEndTimeText = _estimatedEndAt.HasValue ? $"ETA: {_estimatedEndAt.Value:HH:mm:ss}" : "ETA: --:--:--";
        }

        private static string FormatDuration(TimeSpan value)
        {
            int hours = Math.Max(0, (int)value.TotalHours);
            return $"{hours:D2}:{Math.Max(0, value.Minutes):D2}:{Math.Max(0, value.Seconds):D2}";
        }

        private void SaveProcess()
        {
            var dialog = new SaveFileDialog
            {
                Filter = "Process JSON (*.json)|*.json",
                FileName = "process.json"
            };
            if (dialog.ShowDialog() != true) return;

            var settings = new JsonSerializerSettings
            {
                TypeNameHandling = TypeNameHandling.Objects,
                Formatting = Formatting.Indented
            };
            File.WriteAllText(dialog.FileName, JsonConvert.SerializeObject(ProcessModules, settings));
            DiagnosticLogger.Log($"Process saved: {Path.GetFileName(dialog.FileName)}.");
        }

        private void LoadProcess()
        {
            var dialog = new OpenFileDialog { Filter = "Process JSON (*.json)|*.json" };
            if (dialog.ShowDialog() != true) return;

            var settings = new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.Objects };
            var loaded = JsonConvert.DeserializeObject<ObservableCollection<ProcessModule>>(File.ReadAllText(dialog.FileName), settings);
            if (loaded == null) return;

            ProcessModules.Clear();
            foreach (ProcessModule module in loaded)
                ProcessModules.Add(module);
            SelectedModule = ProcessModules.FirstOrDefault();
            DiagnosticLogger.Log($"Process loaded: {Path.GetFileName(dialog.FileName)}.");
        }

        private void ToggleValveConnection()
        {
            try
            {
                if (HardwareContext.ValveBoard.IsConnected)
                    HardwareContext.ValveBoard.Disconnect();
                else
                    HardwareContext.ValveBoard.Connect(HardwareContext.ValvePort);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Valve Relay Connection", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            NotifyConnectionProperties();
        }

        private void ToggleHeaterConnection()
        {
            try
            {
                if (HardwareContext.HeaterBoard.IsConnected)
                    HardwareContext.HeaterBoard.Disconnect();
                else
                    HardwareContext.HeaterBoard.Connect(HardwareContext.HeaterPort);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Heater Relay Connection", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            NotifyConnectionProperties();
        }

        private void OnStepChanged(int index)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                foreach (ProcessModule module in ProcessModules)
                    module.IsActive = false;
                if (index >= 0 && index < ProcessModules.Count)
                {
                    ProcessModules[index].IsActive = true;
                    SelectedModule = ProcessModules[index];
                }
            });
        }

        private void OnLogReceived(string message)
        {
            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                EventLog.Insert(0, message);
                while (EventLog.Count > 100)
                    EventLog.RemoveAt(EventLog.Count - 1);
            }));
        }

        private void NotifyConnectionProperties()
        {
            OnPropertyChanged(nameof(IsValveConnected));
            OnPropertyChanged(nameof(IsHeaterConnected));
            OnPropertyChanged(nameof(ValveConnectionText));
            OnPropertyChanged(nameof(HeaterConnectionText));
        }

        public void Cleanup()
        {
            _executor.Stop();
            _clockTimer.Stop();
            DiagnosticLogger.LogReceived -= OnLogReceived;
            HardwareContext.ValveBoard.Dispose();
            HardwareContext.HeaterBoard.Dispose();
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            CommandManager.InvalidateRequerySuggested();
            return true;
        }
    }
}
