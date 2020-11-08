using System;
using System.IO;

namespace BigFilesArchiver
{
    /// <summary>
    /// Class meant to write processed data (zipped or unzipped) 
    /// after arch operations (zip or unzip)
    /// </summary>
    class ArchWriter : IRunnableInThread
    {
        private readonly FileStream outFileStream;

        public IOutBuffer Buffer { get; set; }

        private readonly Action Write;


        /// <summary>
        /// c-tor
        /// </summary>
        /// <param name="outFileStream"></param>
        /// <param name="act">What action was performed - zipping or unzipping</param>
        public ArchWriter(FileStream outFileStream, ArchAction act)
        {
            this.outFileStream = outFileStream;

            if (act == ArchAction.Zip)
                Write = WriteZippedChunk;
            else
                Write = WriteChunk;
        }


        /// <summary>
        /// This meant for writing zipped data (header first)
        /// </summary>
        private void WriteZippedChunk()
        {
            //zipStream.CopyTo(outFileStream); //doesnt support read

            General.Log($"Writing size {Buffer.BytesToWrite} at the begining of chunk...");

            byte[] sz = BitConverter.GetBytes(Buffer.BytesToWrite);
            outFileStream.Write(sz, 0, sz.Length);

            WriteChunk();
        }


        /// <summary>
        /// This one is called to write data as is (no size header)
        /// </summary>
        private void WriteChunk()
        {
            General.Log($"Writing {Buffer.BytesToWrite} bytes to file...");

            Buffer.Out.Position = 0;

            Buffer.Out.CopyTo(outFileStream);

            Buffer.Out.SetLength(0);//sets valid position of cause
        }


        /// <summary>
        /// To run in a thread
        /// </summary>
        void IRunnableInThread.DoWorkInThread()
        {
            Write();
        }
    }
}
