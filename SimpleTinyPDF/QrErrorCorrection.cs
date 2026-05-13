namespace SimpleTinyPDF
{
    /// <summary>
    /// Error correction level for QR codes. Higher levels allow more damage
    /// tolerance but produce denser codes.
    /// </summary>
    public enum QrErrorCorrection
    {
        /// <summary>~7% recovery capacity.</summary>
        Low,

        /// <summary>~15% recovery capacity.</summary>
        Medium,

        /// <summary>~25% recovery capacity.</summary>
        Quartile,

        /// <summary>~30% recovery capacity.</summary>
        High
    }
}
