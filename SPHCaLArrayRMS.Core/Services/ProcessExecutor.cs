using SPHCaLArrayRMS.Core.Models;

namespace SPHCaLArrayRMS.Core.Services
{
    public sealed class ProcessExecutor
    {
        private CancellationTokenSource? _cts;

        public event Action<int>? StepChanged;
        public event Action<string>? StatusChanged;
        public event Action<int, int>? GlobalLoopChanged;

        public bool IsRunning => _cts != null;

        public async Task RunAsync(IReadOnlyList<ProcessModule> modules, int globalLoopCount)
        {
            if (_cts != null)
                throw new InvalidOperationException("A process is already running.");
            if (globalLoopCount < 1)
                throw new ArgumentOutOfRangeException(nameof(globalLoopCount));

            _cts = new CancellationTokenSource();
            StatusChanged?.Invoke("Running");
            DiagnosticLogger.Log($"Process started with {modules.Count} steps and {globalLoopCount} global loop(s).");

            try
            {
                for (int globalLoop = 1; globalLoop <= globalLoopCount; globalLoop++)
                {
                    _cts.Token.ThrowIfCancellationRequested();
                    GlobalLoopChanged?.Invoke(globalLoop, globalLoopCount);
                    await RunSingleCycleAsync(modules, _cts.Token);
                }

                StatusChanged?.Invoke("Completed");
                DiagnosticLogger.Log("Process completed.");
            }
            catch (OperationCanceledException)
            {
                StatusChanged?.Invoke("Stopped");
                DiagnosticLogger.Log("Process stopped.");
            }
            catch (Exception ex)
            {
                StatusChanged?.Invoke("Error");
                DiagnosticLogger.Log($"Process error: {ex.Message}");
                throw;
            }
            finally
            {
                StepChanged?.Invoke(-1);
                _cts.Dispose();
                _cts = null;
            }
        }

        public void Stop()
        {
            _cts?.Cancel();
            SafeHeaterOff();
        }

        private async Task RunSingleCycleAsync(IReadOnlyList<ProcessModule> modules, CancellationToken token)
        {
            var loopStack = new Stack<(int StartIndex, int Remaining)>();
            int index = 0;

            while (index < modules.Count)
            {
                token.ThrowIfCancellationRequested();
                StepChanged?.Invoke(index);
                ProcessModule module = modules[index];

                if (module is LoopStartModule loopStart)
                {
                    loopStack.Push((index, Math.Max(1, loopStart.LoopCount)));
                }
                else if (module is LoopEndModule)
                {
                    if (loopStack.Count == 0)
                        throw new InvalidOperationException("Loop End has no matching Loop Start.");

                    var state = loopStack.Pop();
                    int remaining = state.Remaining - 1;
                    if (remaining > 0)
                    {
                        loopStack.Push((state.StartIndex, remaining));
                        index = state.StartIndex + 1;
                        continue;
                    }
                }
                else
                {
                    await ExecuteModuleAsync(module, token);
                }

                index++;
            }

            if (loopStack.Count > 0)
                throw new InvalidOperationException("Loop Start has no matching Loop End.");
        }

        private static async Task ExecuteModuleAsync(ProcessModule module, CancellationToken token)
        {
            switch (module)
            {
                case HeaterControlModule heater:
                    if (!HardwareContext.HeaterBoard.IsConnected)
                        throw new InvalidOperationException("Heater Relay Board is not connected.");

                    if (heater.Action == SwitchAction.On)
                    {
                        HardwareContext.HeaterBoard.SendHeaterParameters(heater.Channel, heater.PowerWatts);
                        HardwareContext.HeaterBoard.SetRelay(7, true);
                    }
                    else
                    {
                        HardwareContext.HeaterBoard.SetRelay(7, false);
                    }

                    DiagnosticLogger.Log($"Heater CH{heater.Channel}, {heater.PowerWatts} W, {heater.Action}, {heater.DurationInSeconds:F2} s.");
                    if (heater.DurationInSeconds > 0)
                        await Task.Delay(TimeSpan.FromSeconds(heater.DurationInSeconds), token);
                    break;

                case ValveControlModule valve:
                    if (!HardwareContext.ValveBoard.IsConnected)
                        throw new InvalidOperationException("Valve Relay Board is not connected.");

                    HardwareContext.ValveBoard.SetRelay(valve.RelayNumber, valve.Action == ValveAction.Open);
                    DiagnosticLogger.Log($"{valve.Channel} -> {valve.Action}.");
                    break;

                case WaitModule wait:
                    DiagnosticLogger.Log($"Wait {wait.DurationInSeconds:F2} s.");
                    if (wait.DurationInSeconds > 0)
                        await Task.Delay(TimeSpan.FromSeconds(wait.DurationInSeconds), token);
                    break;

                default:
                    throw new InvalidOperationException($"Unsupported module: {module.GetType().Name}");
            }
        }

        private static void SafeHeaterOff()
        {
            if (!HardwareContext.HeaterBoard.IsConnected) return;
            try
            {
                HardwareContext.HeaterBoard.SetRelay(7, false);
            }
            catch (Exception ex)
            {
                DiagnosticLogger.Log($"Heater safe-off error: {ex.Message}");
            }
        }
    }
}
