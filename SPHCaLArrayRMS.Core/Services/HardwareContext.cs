namespace SPHCaLArrayRMS.Core.Services
{
    public static class HardwareContext
    {
        public const string ValvePort = "COM9";
        public const string HeaterPort = "COM10";

        public static RelayBoardController ValveBoard { get; } = new("Valve Relay Board");
        public static RelayBoardController HeaterBoard { get; } = new("Heater Relay Board");
    }
}
