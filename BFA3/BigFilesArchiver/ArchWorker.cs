using System;
using System.IO;
using System.IO.Compression;

namespace BigFilesArchiver
{
    class ArchWorker : IRunnableInThread
	{
		IInBuffer inBuffer;
		IOutBuffer outBuffer;

		// shorcuts
		MemoryStream Out { get => outBuffer.Out; }
        byte[] In { get => inBuffer.In; }
        int BytesRead { get => inBuffer.BytesRead; }

		Action work;


		/// <summary>
		/// c-tor
		/// </summary>
		/// <param name="inBuffer">in data</param>
		/// <param name="outBuffer">out buffer</param>
		/// <param name="act">Zip or Unzip</param>
		public ArchWorker(IInBuffer inBuffer, IOutBuffer outBuffer, ArchAction act)
		{
			this.inBuffer = inBuffer;
			this.outBuffer = outBuffer;

			if (act == ArchAction.Zip)
				work = Zip;
			else
				work = Unzip;
		}


		/// <summary>
		/// Zips bytes from input buffer into output buffer
		/// </summary>
		public void Zip()
		{
            General.Log($"worker zipping {BytesRead} bytes...");

			using GZipStream zipStream = new GZipStream(Out, CompressionMode.Compress, true); //leave the stream open

            zipStream.Write(In, 0, BytesRead);
        }


		/// <summary>
		/// UnZips bytes from input buffer into output buffer
		/// </summary>
		public void Unzip()
		{
			using var ms = new MemoryStream(In, 0, BytesRead);
			using var zipStream = new GZipStream(ms, CompressionMode.Decompress);

			General.Log($"worker Unzipping {BytesRead} bytes...");

			zipStream.CopyTo(Out);
		}


		/// <summary>
		/// Specify the work to be done in thread
		/// </summary>
		void IRunnableInThread.DoWorkInThread()
		{
			work();
		}
	}
}
