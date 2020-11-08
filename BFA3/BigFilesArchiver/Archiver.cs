using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Xml;

namespace BigFilesArchiver
{

	/// <summary>
	/// Main class to do zip/unzip by chunks.
	/// Created according to the following requirements:
	/// Only basic classes and synchronization objects should be used for multithreading 
	/// (Thread, Manual/AutoResetEvent, Monitor, Semaphore, Mutex), 
	/// it is not allowed to use async/await, ThreadPool, BackgroundWorker, TPL.
	/// </summary>
	public static class Archiver
	{
		const int defaultChunkSize = 10 * 1024 * 1024;


		public static void ZipByChunks(string inputName, string outputName, int bufferSize = 0, uint workersCount = 0)
		{
			ManageThreads(inputName, outputName, ArchAction.Zip, bufferSize, workersCount);
		}


		public static void UnzipFromChunks(string inputName, string outputName, int bufferSize = 0, uint workersCount = 0)
		{
			ManageThreads(inputName, outputName, ArchAction.Unzip, bufferSize, workersCount);
		}


		private static void ManageThreads(string inputName, string outputName, ArchAction act, int bufferSize, uint workersCount = 0)
		{
			General.Log($"{act} started.");

			InOutBuffer[] buffers = null;
			ThreadWorker[] workers = null;
			ArchReader reader;
			ThreadWorker readerThread = null;
			ArchWriter writer;
			ThreadWorker writerThread = null;

			if (bufferSize < defaultChunkSize)
				bufferSize = defaultChunkSize;

            try
            {

                using FileStream outFileStream = File.Create(outputName);
            
                using FileStream inFileStream = File.OpenRead(inputName);

                if (workersCount < 1)
                    workersCount = ChooseWorkersCount();


                CreateAllObjects(act, bufferSize, workersCount, outFileStream, inFileStream, out buffers, out workers, out reader, out readerThread, out writer, out writerThread);

				StartConveyor(reader, readerThread, workers, buffers);

				int idx = 0;

                // as long as there are bytes read from input, keep conveyor
                // when no more input data, set this flag & wait till all therads are finihed.
                bool finishing = false;

                do
                {
                    //all zippers are working

                    ThreadWorker worker = workers[idx];

                    if (worker == null)
                    {
                        General.Log($"all threads are finished");
                        // good place to let the writer go
                        writerThread.Finish();
                        //this might be unnecesary, since program is exiting anyway,
                        //but to keep code orginised:
                        writerThread.Dispose(); 
                        writerThread = null;
                        break;
                    }

                    General.Log($"main thread about to Wait for worker {idx} done, to start writting");
                    worker.notifyDone.WaitOne(); //wait till zipper idx has done

                    General.Log($"{idx} start writting");

                    writer.Buffer = buffers[idx];
                    writerThread.haveWork.Set();

                    if (!finishing)
                    {
                        General.Log($"{idx} start reading next portion");
                        //reader.Buffer = buffers[idx].In;
                        reader.Buffer = buffers[idx];
                        readerThread.haveWork.Set();

                        General.Log($"{idx} need both In & Out buffers to be Ready to start next part zippin");
                        readerThread.notifyDone.WaitOne();

                        // check if read anyting
                        if (reader.Buffer.BytesRead <= 0)
                        {
                            //have read all, and all is (being) processed
                            finishing = true;

                            // right place to set the reader free
                            readerThread.Finish();
                            readerThread.Dispose();//to feel good
                            readerThread = null;
                        }

                    }

                    writerThread.notifyDone.WaitOne();

                    if (finishing)
                    {
                        worker.Finish();
                        workers[idx].Dispose();
                        workers[idx] = null;
                        //can clean buffers here as well
                        buffers[idx].Dispose();
                        buffers[idx] = null;
                    }
                    else
                    {
                        worker.haveWork.Set();
                    }

                    //cause rotating, and output shall be in-order
                    if (++idx >= workers.Length)
                        idx = 0;

                } while (true);

			}
			catch (Exception exc)
			{
				General.Log($"ManageThreads encountered an error {exc.Message} returning to caller.");
				throw;
			}
			finally
            {
                CleanUp(buffers, workers, readerThread, writerThread);
            }


            General.Log($"{act} finished.");
		}


        #region initialisation methods
        /// <summary>
        /// Creates all the objects required for the requested operation
        /// </summary>
        /// <param name="act"></param>
        /// <param name="bufferSize"></param>
        /// <param name="workersCount"></param>
        /// <param name="outFileStream"></param>
        /// <param name="inFileStream"></param>
        /// <param name="buffers"></param>
        /// <param name="workers"></param>
        /// <param name="reader"></param>
        /// <param name="readerThread"></param>
        /// <param name="writer"></param>
        /// <param name="writerThread"></param>
        static void CreateAllObjects(ArchAction act, int bufferSize, uint workersCount, FileStream outFileStream, FileStream inFileStream, out InOutBuffer[] buffers, out ThreadWorker[] workers, out ArchReader reader, out ThreadWorker readerThread, out ArchWriter writer, out ThreadWorker writerThread)
        {
            buffers = CreateBuffers(workersCount, bufferSize);
            ArchWorker[] archivers = CreateArchivers(buffers, act);

            workers = CreateThreadWorkers(archivers);
            reader = new ArchReader(inFileStream, act);
            readerThread = new ThreadWorker(reader, true);
            writer = new ArchWriter(outFileStream, act);
            writerThread = new ThreadWorker(writer, true);
        }


        /// <summary>
        /// Kick start of a zip/unzip process
        /// </summary>
        /// <param name="reader"></param>
        /// <param name="readerThread"></param>
        /// <param name="workers"></param>
        /// <param name="buffers"></param>
        static void StartConveyor(ArchReader reader, ThreadWorker readerThread, ThreadWorker[] workers, InOutBuffer[] buffers)
        {
			Debug.Assert(workers.Length == buffers.Length);

			for (int idx = 0; idx < workers.Length; idx++)
			{
				ThreadWorker worker = workers[idx];

				General.Log($"{idx} start reading");
				reader.Buffer = buffers[idx];
				readerThread.haveWork.Set();

				readerThread.notifyDone.WaitOne();
				General.Log($"{idx} reading chunk complete, start zippin");
				worker.haveWork.Set();
				worker.Start();
			}
		}


		/// <summary>
		/// Create number of working threads objects
		/// </summary>
		/// <param name="archivers">those who will do work in threads</param>
		/// <returns></returns>
		static ThreadWorker[] CreateThreadWorkers(ArchWorker[] archivers)
        {
			int count = archivers.Length;

			Debug.Assert(count > 0);

			General.Log($"Creating {count} thread workers");

			ThreadWorker[] workers = new ThreadWorker[count];

			for (int i = 0; i < archivers.Length; i++)
				workers[i] = new ThreadWorker(archivers[i]);

			return workers;
		}


		/// <summary>
		/// Creates requested number of working buffers with given size
		/// </summary>
		/// <param name="workersCount"></param>
		/// <param name="bufferSize"></param>
		/// <returns></returns>
		static InOutBuffer[] CreateBuffers(uint workersCount, int bufferSize)
        {
			InOutBuffer.ChunkSize = bufferSize;

			InOutBuffer[] buffers = Array.ConvertAll(new InOutBuffer[workersCount], _ => new InOutBuffer());

			return buffers;
		}


		/// <summary>
		/// Detects optimal working threads count for this system
		/// </summary>
		/// <returns></returns>
		public static uint ChooseWorkersCount()
        {
			int procCount = Environment.ProcessorCount;
			General.Log($"Number Of Logical Processors: {procCount}");

			// it is unlikely to have single core nowdays, but just in case
			if (procCount < 2)
				// choose any number > 1 to demonstrate multithreading
				procCount = 2;

			return (uint) procCount;
		}


		/// <summary>
		/// Create those who do zip/unzip
		/// </summary>
		/// <param name="buffers"></param>
		/// <param name="act">zip or unzip</param>
		/// <returns></returns>
		static ArchWorker[] CreateArchivers(InOutBuffer[] buffers, ArchAction act)
		{
			int count = buffers.Length;

			Debug.Assert(count > 0);

			General.Log($"Creating {count} (un)zippers");

			ArchWorker[] archivers = new ArchWorker[count];

			for (int i = 0; i < archivers.Length; i++)
				archivers[i] = new ArchWorker(inBuffer: buffers[i], outBuffer: buffers[i], act);

			return archivers;
		}
        #endregion initialisation methods


        #region clean up 
        /// <summary>
        /// Let's clean
        /// </summary>
        /// <param name="buffers"></param>
        /// <param name="workers"></param>
        /// <param name="readerThread"></param>
        /// <param name="writerThread"></param>
        private static void CleanUp(InOutBuffer[] buffers, ThreadWorker[] workers, ThreadWorker readerThread, ThreadWorker writerThread)
        {
            try
            {
                if (workers != null)
                {
                    foreach (var obj in workers)
                        obj?.Dispose();
                }
                if (buffers != null)
                {
                    foreach (var obj in buffers)
                        obj?.Dispose();
                }

                readerThread?.Dispose();
                writerThread?.Dispose();
            }
            catch (Exception exc2)
            {
                General.Log($"Clean up failed: {exc2.Message}");
            }
        }
        #endregion clean up 
    }
}
