using System;
using System.Diagnostics;
using System.Threading;

namespace BigFilesArchiver
{
    /// <summary>
    /// Allows worker to do work in a thread, repeatedly
    /// </summary>
    class ThreadWorker : IDisposable
    {
        IRunnableInThread logicWorker;
        //or
        Action workLogic;

        public AutoResetEvent haveWork = new AutoResetEvent(false);

        public AutoResetEvent notifyDone = new AutoResetEvent(false);

        public bool KeepWorking { get; set; } = true;

        Thread thread;


        /// <summary>
        /// c-tor
        /// </summary>
        /// <param name="logicWorker"></param>
        /// <param name="startImmediately"></param>
        public ThreadWorker(IRunnableInThread logicWorker, bool startImmediately = false)
        {
            this.logicWorker = logicWorker;
            thread = new Thread(this.DoWork);

            if (startImmediately)
                this.Start();
        }


        /// <summary>
        /// c-tor
        /// </summary>
        /// <param name="workLogic"></param>
        /// <param name="startImmediately"></param>
        public ThreadWorker(Action workLogic, bool startImmediately = false)
        {
            this.workLogic = workLogic;
            thread = new Thread(this.DoWork);

            if (startImmediately)
                this.Start();
        }


        /// <summary>
        /// start actual work cycle
        /// </summary>
        public void Start()
        {
            thread.Start();
        }


        /// <summary>
        /// graceful finish
        /// </summary>
        public void Finish()
        {
            KeepWorking = false; //as soon as it finishes current work it exits +
            haveWork.Set(); //in case if its in Waiting state
        }

        /// <summary>
        /// immediate stop. implement if needed
        /// </summary>
        //public void Terminate() {}


        /// <summary>
        /// Method to be run in a thread.
        /// Implements a loop to repeat the work.
        /// </summary>
        private void DoWork()
        {
            Debug.Assert(logicWorker != null || workLogic != null);

            while(KeepWorking)
            {
                haveWork.WaitOne();

                if (!KeepWorking)
                {
                    return;
                }

                if (logicWorker != null)
                    logicWorker.DoWorkInThread();
                else 
                    workLogic?.Invoke();

                notifyDone.Set();
                //or can implement like this
                //callbackDel(this);
            }
        }

        #region Simple Dispose
        public void Dispose()
        {
            ((IDisposable)notifyDone).Dispose();
            ((IDisposable)haveWork).Dispose();
        }
        #endregion Simple Dispose
    }
}
