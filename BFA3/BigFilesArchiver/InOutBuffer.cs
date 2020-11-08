using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace BigFilesArchiver
{
    class InOutBuffer : IInBuffer, IOutBuffer, IDisposable
    {
        public static int ChunkSize { get; set; } = 10 * 1024 * 1024;

        public byte[] In { get; set; } = new byte[ChunkSize];
        public int BytesRead { get; set; } = 0;

        public MemoryStream Out { get; set; } = new MemoryStream(ChunkSize);


        #region Dispose Pattern
        private bool disposedValue;

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // TODO: dispose managed state (managed objects)
                    Out.Dispose();
                }

                // TODO: free unmanaged resources (unmanaged objects) and override finalizer
                // TODO: set large fields to null
                In = null;
                disposedValue = true;
            }
        }

        // // TODO: override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
        // ~InOutBuffer()
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
