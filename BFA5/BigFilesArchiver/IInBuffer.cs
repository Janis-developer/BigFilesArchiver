namespace BigFilesArchiver
{
    /// <summary>
    /// 
    /// Interface for an input buffer
    /// 
    /// </summary>
    interface IInBuffer
    {
        byte[] In { get; set; }
        public int BytesRead { get; set; }
    }
}
