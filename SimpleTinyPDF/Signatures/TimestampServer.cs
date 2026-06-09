namespace SimpleTinyPDF
{
    /// <summary>
    /// Well-known free RFC 3161 Time Stamp Authority servers.
    /// </summary>
    public enum TimestampServer
    {
        /// <summary>No timestamping (default).</summary>
        None = 0,

        /// <summary>DigiCert TSA — http://timestamp.digicert.com</summary>
        DigiCert,

        /// <summary>Sectigo TSA — http://timestamp.sectigo.com</summary>
        Sectigo,

        /// <summary>FreeTSA — https://freetsa.org/tsr</summary>
        FreeTSA
    }
}
