namespace Nucleus.Core.Command
{
    /// <summary>Pure SSOT for operation-status wording, so the panel, the map overlay, and any feed read it
    /// identically instead of showing the raw enum.</summary>
    public static class OperationText
    {
        public static string StatusLabel(OperationStatus s)
        {
            switch (s)
            {
                case OperationStatus.Planning: return "Forming up";
                case OperationStatus.Active:   return "Active";
                case OperationStatus.Complete: return "Done";
                case OperationStatus.Failed:   return "Failed";
                default:                       return s.ToString();
            }
        }
    }
}
