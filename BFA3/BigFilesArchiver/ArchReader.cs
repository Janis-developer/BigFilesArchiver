using System;
using System.Diagnostics;
using System.IO;

namespace BigFilesArchiver
{
    /// <summary>
    /// Class meant to read input data (original or zipped) 
    /// for arch operations (zip or unzip)
    /// </summary>
    class ArchReader : IRunnableInThread
    {
        private readonly FileStream inFileStream;

        public IInBuffer Buffer { get; set; }

        private readonly Action Read;


        /// <summary>
        /// c-tor
        /// </summary>
        /// <param name="inFileStream"></param>
        /// <param name="act">What action is requested - zipping or unzipping</param>
        public ArchReader(FileStream inFileStream, ArchAction act)
        {
            this.inFileStream = inFileStream;

            if (act == ArchAction.Zip)
                Read = ReadNextChunk;
            else
                Read = ReadNextZippedChunk;
        }


        /// <summary>
        /// This one is called to read original (not zipped) data
        /// </summary>
        void ReadNextChunk()
        {
            Debug.Assert(Buffer != null);

            General.Log($"Reading {Buffer.In.Length} input bytes...");

            Buffer.BytesRead = inFileStream.Read(Buffer.In, 0, Buffer.In.Length);

            General.Log($"{Buffer.BytesRead} bytes has been read.");
        }


        /// <summary>
        /// This meant to read zipped file (which has header)
        /// </summary>
        void ReadNextZippedChunk()
        {
            Debug.Assert(Buffer != null);

            byte[] sz = new byte[8];
            int rr = inFileStream.Read(sz, 0, sz.Length);
            if (rr < 8)
            {
                Buffer.BytesRead = 0; //finish reading
            }
            else
            {
                long zippedChunkSize = BitConverter.ToInt64(sz);

                Debug.Assert(zippedChunkSize > 0);

                //System.Diagnostics.Debug.Assert(zippedChunkSize < array.Length);
                if (zippedChunkSize > Buffer.In.Length)
                    Buffer.In = new byte[zippedChunkSize];

                General.Log($"Reading {zippedChunkSize} zipped input bytes...");
                Buffer.BytesRead = inFileStream.Read(Buffer.In, 0, (int)zippedChunkSize);

                Debug.Assert(zippedChunkSize == Buffer.BytesRead);
            }
            General.Log($"{Buffer.BytesRead} bytes has been read.");
        }


        /// <summary>
        /// Implementing this interface to run in a htread
        /// </summary>
        void IRunnableInThread.DoWorkInThread()
        {
            Read();
        }
    }
}
