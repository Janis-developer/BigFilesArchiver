using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;

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
		enum ArchAction { Zip, Unzip };

        const int defaultChunkSize = 10 * 1024 * 1024;

		static ArchAction act;

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
			Archiver.act = act;
			
			if (bufferSize > defaultChunkSize)
				ArchWorker.ChunkSize = bufferSize;

			using (FileStream outFileStream = File.Create(outputName))
			{
				ArchWorker.OutFileStream = outFileStream;

                using FileStream inFileStream = File.OpenRead(inputName);

				if (workersCount < 1)
					workersCount = ChooseWorkersCount();

				ArchWorker[] archivers = CreateWorkers(workersCount);

                int idx = 0;

                do
                {
                    ArchWorker arch = archivers[idx];

                    General.Log($"main thread about to Wait for arch {idx} done, to start it in new thread again");
                    arch.Done.WaitOne();

                    //Load the next portion to be processed and written
                    int read = act == ArchAction.Zip ? arch.ReadChunk(inFileStream) : arch.ReadZippedChunk(inFileStream);

					if (read > 0)
                    {

						//TODO
						//Thread th = new Thread(new ParameterizedThreadStart(act == ArchAction.Zip ? ZipWork : UnzipWork));

						Thread th;
						if (act == ArchAction.Zip)
							//th = new Thread(new ParameterizedThreadStart(ZipWork));
							th = new Thread(ZipWork);
						else
							//th = new Thread(new ParameterizedThreadStart(UnzipWork));
							th = new Thread(UnzipWork);

						th.Start(arch);

                        if (++idx >= archivers.Length)
                            idx = 0;
                    }
                    else
                    {
                        //have read all

                        if (archivers.Length == 1)
                            break;

                        if (--idx < 0)
                            idx = archivers.Length - 1;

                        General.Log($"main thread about to Wait for the last arch {idx} done...");
                        archivers[idx].Done.WaitOne(); //prev is finished, meaning all before prev are finished

                        //now safe to go out and dispose outputStream
                        break;
                    }

                } while (true);

                //archivers = null;//no need
            }

			General.Log($"{act} finished.");
		}


		/// <summary>
		/// Detects optimal working threads count for this system
		/// </summary>
		/// <returns></returns>
		static public uint ChooseWorkersCount()
        {
			int procCount = Environment.ProcessorCount;
			General.Log($"Number Of Logical Processors: {procCount}");

			// it is unlikely to have single core nowdays, but just in case
			if (procCount < 2)
				// choose any number > 1 to demonstrate multithreading
				procCount = 2;

			// need to run more tests to figure out correlation
			if (act == ArchAction.Unzip)
				procCount /= 2;

			return (uint) procCount;
		}


		/// <summary>
		/// Creates requested number of workers
		/// </summary>
		/// <param name="count"></param>
		/// <returns></returns>
		static ArchWorker[] CreateWorkers(uint count)
		{
            Debug.Assert(count > 0);

            General.Log($"Creating {count} workers");

			ArchWorker[] archivers = new ArchWorker[count];

            for (int i = 0; i < archivers.Length; i++)
                archivers[i] = new ArchWorker(i);

            archivers[0].CanWrite.Set();

			if (archivers.Length > 1)
			{
				// create closed chain

				for (int idx = 0; idx < archivers.Length - 1; idx++)
				{
					archivers[idx].Next = archivers[idx + 1];
                }

                archivers[^1].Next = archivers[0];
			}

			return archivers;
		}


		static void ZipWork(object o)
		{
			ArchWorker arch = o as ArchWorker;

			General.Log($"thread started with arch worker {arch.Idx}");
			arch.ZipAndWrite();
		}


		static void UnzipWork(object o)
		{
			ArchWorker arch = o as ArchWorker;

			General.Log($"thread started with arch worker {arch.Idx}");
			arch.UnzipAndWrite();
		}

	}
}
