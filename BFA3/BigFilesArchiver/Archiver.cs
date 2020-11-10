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
            InOutBuffer[] buffers = null;
            ThreadWorker[] workers = null;

            try
            {
                General.Log($"Started '{inputName}' {act} into '{outputName}'...");

                if (bufferSize < defaultChunkSize)
                    bufferSize = defaultChunkSize;

                if (workersCount < 1)
                    workersCount = ChooseWorkersCount();

                General.Log($"buffer size {bufferSize}, workers count {workersCount}");

                //to feel good let's use 'using'

                // objects for input reading
                using FileStream inFileStream = File.OpenRead(inputName);
                ArchReader reader = new ArchReader(inFileStream, act);
                using ThreadWorker readerThread = new ThreadWorker(reader, true);

                // objects for output writing
                using FileStream outFileStream = File.Create(outputName);
                ArchWriter writer = new ArchWriter(outFileStream, act);
                using ThreadWorker writerThread = new ThreadWorker(writer, true);

                // create objects to do zipping/unzipping
                CreateBuffersAndWorkers(act, bufferSize, workersCount, out buffers, out workers);

                StartConveyor(reader, readerThread, workers, buffers);

                int idx = 0;

                // as long as there are bytes read from input, keep conveyor
                // when no more input data, set this flag & wait till all therads are finihed.
                bool finishing = false;

                do
                {
                    ThreadWorker worker = workers[idx];

                    if (worker == null)
                    {
                        General.Log($"all threads are finished");
                        // good place to let the writer go
                        writerThread.Finish();
                        //writerThread.Dispose(); 
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
                        reader.Buffer = buffers[idx];
                        readerThread.haveWork.Set();

                        General.Log($"{idx} need both In & Out buffers to be Ready to start next part zippin");
                        readerThread.notifyDone.WaitOne();

                        // check if have read anyting
                        if (reader.Buffer.BytesRead <= 0)
                        {
                            //have read all, and all is (being) processed
                            finishing = true;

                            // right place to set the reader free
                            readerThread.Finish();
                            //readerThread.Dispose();
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

                General.Log($"{act} finished successfuly.");
            }
            catch (Exception exc)
            {
                General.Log($"ManageThreads encountered an error {exc.Message} returning to caller.");
                throw;
            }
            finally
            {
                CleanUp(buffers, workers);
            }
        }


        #region initialisation methods
        /// <summary>
        /// Creates all the objects required for the requested operation
        /// </summary>
        /// <param name="act"></param>
        /// <param name="bufferSize"></param>
        /// <param name="workersCount"></param>
        /// <param name="buffers"></param>
        /// <param name="workers"></param>
        static void CreateBuffersAndWorkers(ArchAction act, int bufferSize, uint workersCount, out InOutBuffer[] buffers, out ThreadWorker[] workers)
        {
            CreateBuffers(workersCount, bufferSize, out buffers);

            CreateArchivers(buffers, act, out ArchWorker[] archivers);

            CreateThreadWorkers(archivers, out workers);
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
        /// <returns>workers as out param</returns>
        static void CreateThreadWorkers(ArchWorker[] archivers, out ThreadWorker[] workers)
        {
            int count = archivers.Length;

            Debug.Assert(count > 0);

            General.Log($"Creating {count} thread workers");

            workers = new ThreadWorker[count];

            for (int i = 0; i < archivers.Length; i++)
                workers[i] = new ThreadWorker(archivers[i]);
        }


        /// <summary>
        /// Creates requested number of working buffers with given size
        /// </summary>
        /// <param name="workersCount"></param>
        /// <param name="bufferSize"></param>
        /// <returns>buffers as out parameter</returns>
        static void CreateBuffers(uint workersCount, int bufferSize, out InOutBuffer[] buffers)
        {
            General.Log($"Creating {workersCount} in/out buffers");

            InOutBuffer.ChunkSize = bufferSize;

            //InOutBuffer[] buffers = Array.ConvertAll(new InOutBuffer[workersCount], _ => new InOutBuffer());

            buffers = new InOutBuffer[workersCount];

            for (int idx = 0; idx < buffers.Length; idx++)
                buffers[idx] = new InOutBuffer();
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

            return (uint)procCount;
        }


        /// <summary>
        /// Create those who do zip/unzip
        /// </summary>
        /// <param name="buffers"></param>
        /// <param name="act">zip or unzip</param>
        /// <param name="archivers">out param</param>
        /// <returns></returns>
        static void CreateArchivers(InOutBuffer[] buffers, ArchAction act, out ArchWorker[] archivers)
        {
            int count = buffers.Length;

            Debug.Assert(count > 0);

            General.Log($"Creating {count} (un)zippers");

            archivers = new ArchWorker[count];

            for (int i = 0; i < archivers.Length; i++)
                archivers[i] = new ArchWorker(inBuffer: buffers[i], outBuffer: buffers[i], act);
        }
        #endregion initialisation methods


        #region clean up 
        /// <summary>
        /// Let's clean
        /// </summary>
        /// <param name="buffers"></param>
        /// <param name="workers"></param>
        private static void CleanUp(InOutBuffer[] buffers, ThreadWorker[] workers)
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
            }
            catch (Exception exc2)
            {
                General.Log($"Clean up failed: {exc2.Message}");
            }
        }
        #endregion clean up 
    }
}
