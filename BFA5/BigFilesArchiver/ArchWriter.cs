using System;
using System.IO;
using System.Threading.Tasks;

namespace BigFilesArchiver
{
    /// <summary>
    /// Class meant to write processed data (zipped or unzipped) 
    /// after arch operations (zip or unzip)
    /// </summary>
    class ArchWriter
    {
        private readonly FileStream outFileStream;

        public IOutBuffer Buffer { get; set; }

        public Func<Task> WriteAsync { get; }


        /// <summary>
        /// c-tor
        /// </summary>
        /// <param name="outFileStream"></param>
        /// <param name="act">What action was performed - zipping or unzipping</param>
        public ArchWriter(FileStream outFileStream, ArchAction act)
        {
            this.outFileStream = outFileStream;

            if (act == ArchAction.Zip)
                WriteAsync = WriteZippedChunkAsync;
            else
                WriteAsync = WriteChunkAsync;
        }


        /// <summary>
        /// This meant for writing zipped data (header first) async
        /// </summary>
        async public Task WriteZippedChunkAsync()
        {
            //zipStream.CopyTo(outFileStream); //doesnt support read

            General.Log($"Writing size {Buffer.BytesToWrite} at the begining of chunk async...");

            byte[] sz = BitConverter.GetBytes(Buffer.BytesToWrite);
            await outFileStream.WriteAsync(sz, 0, sz.Length);

            await WriteChunkAsync();
        }


        /// <summary>
        /// This one is called to write data as is (no size header) async
        /// </summary>
        async public Task WriteChunkAsync()
        {
            General.Log($"Writing {Buffer.BytesToWrite} bytes to file async...");

            Buffer.Out.Position = 0;

            await Buffer.Out.CopyToAsync(outFileStream);

            Buffer.Out.SetLength(0);//sets valid position of cause
        }
    }
}
