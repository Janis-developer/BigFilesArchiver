using System;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;

namespace BigFilesArchiver
{
    class ArchWorker
	{
        readonly IInBuffer inBuffer;
        readonly IOutBuffer outBuffer;

		// shorcuts
		MemoryStream Out { get => outBuffer.Out; }
        byte[] In { get => inBuffer.In; }
        int BytesRead { get => inBuffer.BytesRead; }

        public Func<Task> DoWorkAsync { get; }


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
				DoWorkAsync = ZipAsync;
			else
				DoWorkAsync = UnzipAsync;
		}


		/// <summary>
		/// Zips bytes from input buffer into output buffer async
		/// </summary>
		async public Task ZipAsync()
		{
			General.Log($"worker zipping {BytesRead} bytes async...");

			using GZipStream zipStream = new GZipStream(Out, CompressionMode.Compress, true); //leave the stream open

			// Can also use overload with CancellationToken if quick stop will be required
			await zipStream.WriteAsync(In, 0, BytesRead);
		}


		/// <summary>
		/// UnZips bytes from input buffer into output buffer async
		/// </summary>
		async public Task UnzipAsync()
		{
			using var ms = new MemoryStream(In, 0, BytesRead);
			using var zipStream = new GZipStream(ms, CompressionMode.Decompress);

			General.Log($"worker Unzipping {BytesRead} bytes async...");

			await zipStream.CopyToAsync(Out);
		}

	}
}
