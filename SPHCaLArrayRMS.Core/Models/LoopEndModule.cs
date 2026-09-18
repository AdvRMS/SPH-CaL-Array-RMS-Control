namespace SPHCaLArrayRMS.Core.Models
{
    public sealed class LoopEndModule : ProcessModule
    {
        public LoopEndModule()
        {
            ModuleType = "Loop End";
        }

        public override string ToString()
        {
            return "End current loop block";
        }
    }
}
