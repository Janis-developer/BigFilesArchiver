using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Threading;

namespace BigFilesArchiver
{
    class ArchWorker : IDisposable
	{
		// common members (for bunch of workers, managed by upper level)
		public static FileStream OutFileStream { get; set; }
		public static int ChunkSize { get; set; } = 10 * 1024 * 1024;

		// an instance members
        public int Idx { get; }

        public AutoResetEvent Done { get; } = new AutoResetEvent(true);

        public AutoResetEvent CanWrite { get; } = new AutoResetEvent(false);

        internal ArchWorker Next { get; set; }

        byte[] array = new byte[ChunkSize]; //to read input into
		int read = 0; //how many bytes were actually read (the last reading can be less)

        readonly MemoryStream msOutput = new MemoryStream(ChunkSize);

        public ArchWorker(int idx)
        {
            Debug.Assert(OutFileStream != null, "The rule of using ArchWorker object - to set static OutFileStream before instantiating");
            this.Idx = idx;
        }


		#region Zipping
		internal int ReadChunk(FileStream inputStream)
		{
			General.Log($"worker {Idx} Reading {array.Length} input bytes...");
			return read = inputStream.Read(array, 0, array.Length);
		}


		public void Zip()
		{
            General.Log($"worker {Idx} zipping {read} bytes...");

			using GZipStream zipStream = new GZipStream(msOutput, CompressionMode.Compress, true); //leave the stream open

            zipStream.Write(array, 0, read); //! not whole array but only read part

            //it flushes anyway when being disposed, but no harm
            //zipStream.Flush();
        }


		public void Write(FileStream outputStream)
		{
			//zipStream.CopyTo(outputStream); //doesnt support read

			byte[] sz = BitConverter.GetBytes(msOutput.Length);
			outputStream.Write(sz, 0, sz.Length);

			General.Log($"worker {Idx} Writing {msOutput.Length} zipped bytes to file...");

			msOutput.Position = 0;
			msOutput.CopyTo(outputStream);

			//no need
			//msOutput.Flush();
			//outputStream.Flush();

			msOutput.SetLength(0);//sets valid position of cause
		}


		public void ZipAndWrite()
		{
			Zip();

			if (Next != null)
			{
				General.Log($"worker {Idx} going to wait for its turn to canWrite");

				CanWrite.WaitOne();
			}

			Write(OutFileStream);

			if (Next != null)
			{
				General.Log($"worker {Idx} Sets next archiver.canWrite");
				Next.CanWrite.Set();
			}

			General.Log($"worker {Idx} Sets done");
			Done.Set();
		}
		#endregion Zipping

		#region Unzipping
		internal int ReadZippedChunk(FileStream inputStream)
		{
			byte[] sz = new byte[8];
			int rr = inputStream.Read(sz, 0, sz.Length);
			if (rr < 8)
			{
				return 0;
			}

			long zippedChunkSize = BitConverter.ToInt64(sz);

            Debug.Assert(zippedChunkSize > 0);

			//System.Diagnostics.Debug.Assert(zippedChunkSize < array.Length);
			if (zippedChunkSize > array.Length)
				array = new byte[zippedChunkSize];

			General.Log($"worker {Idx} Reading {zippedChunkSize} zipped bytes into array...");
			read = inputStream.Read(array, 0, (int)zippedChunkSize);

            Debug.Assert(zippedChunkSize == read);

			return read;
		}


		public void Unzip()
		{
			using var ms = new MemoryStream(array, 0, read);
			using var zipStream = new GZipStream(ms, CompressionMode.Decompress);

			General.Log($"worker {Idx} Unzipping {read} bytes...");

			zipStream.CopyTo(msOutput);

			//it flushes anyway when being disposed, but no harm
			//zipStream.Flush();
		}


		public void WriteUnzipped(FileStream outputStream)
		{
			General.Log($"worker {Idx} writing {msOutput.Length} unzipped bytes to file...");

			msOutput.Position = 0;

			msOutput.CopyTo(outputStream);

			msOutput.SetLength(0);
		}


		public void UnzipAndWrite()
		{
			Unzip();

			if (Next != null)
			{
				General.Log($"worker {Idx} going to wait for its turn to canWrite");

				CanWrite.WaitOne();
			}

			WriteUnzipped(OutFileStream);

			if (Next != null)
			{
				General.Log($"worker {Idx} Sets next archiver.canWrite");
				Next.CanWrite.Set();
			}

			General.Log($"worker {Idx} Sets done");
			Done.Set();
		}
		#endregion Unzipping

		#region Dispose Pattern
		private bool disposedValue;

		protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
					// TODO: dispose managed state (managed objects)
					msOutput?.Dispose();
					Done?.Dispose();
					CanWrite?.Dispose();
				}

				// TODO: free unmanaged resources (unmanaged objects) and override finalizer
				// TODO: set large fields to null
				array = null;
				disposedValue = true;
            }
        }

		// // TODO: override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
		// ~ArchWorker()
		// {
		//     // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
		//     Dispose(disposing: false);
		// }

		public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
			// TODO: uncomment the following line if the finalizer is overridden above.
			//GC.SuppressFinalize(this);
		}
		#endregion Dispose Pattern
	}
}
