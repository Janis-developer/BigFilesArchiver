using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace BigFilesArchiver
{
    /// <summary>
    /// Class meant to read input data (original or zipped) 
    /// for arch operations (zip or unzip)
    /// </summary>
    class ArchReader
    {
        private readonly FileStream inFileStream;

        public IInBuffer Buffer { get; set; }

        public Func<Task> ReadAsync { get; }


        /// <summary>
        /// c-tor
        /// </summary>
        /// <param name="inFileStream"></param>
        /// <param name="act">What action is requested - zipping or unzipping</param>
        public ArchReader(FileStream inFileStream, ArchAction act)
        {
            this.inFileStream = inFileStream;

            if (act == ArchAction.Zip)
                ReadAsync = ReadNextChunkAsync;
            else
                ReadAsync = ReadNextZippedChunkAsync;
        }


        /// <summary>
        /// This one is called to read original (not zipped) data async
        /// </summary>
        async public Task ReadNextChunkAsync()
        {
            Debug.Assert(Buffer != null);

            General.Log($"Reading {Buffer.In.Length} input bytes async...");

            Buffer.BytesRead = await inFileStream.ReadAsync(Buffer.In, 0, Buffer.In.Length);

            General.Log($"{Buffer.BytesRead} bytes has been read.");
        }


        /// <summary>
        /// This meant to read zipped file (which has header) async
        /// </summary>
        async public Task ReadNextZippedChunkAsync()
        {
            Debug.Assert(Buffer != null);

            byte[] sz = new byte[8];
            int rr = await inFileStream.ReadAsync(sz, 0, sz.Length);
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

                General.Log($"Reading {zippedChunkSize} zipped input bytes async...");
                Buffer.BytesRead = await inFileStream.ReadAsync(Buffer.In, 0, (int)zippedChunkSize);

                Debug.Assert(zippedChunkSize == Buffer.BytesRead);
            }
            General.Log($"{Buffer.BytesRead} bytes has been read.");
        }
    }
}
