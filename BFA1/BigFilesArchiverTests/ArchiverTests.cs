using Microsoft.VisualStudio.TestTools.UnitTesting;
using BigFilesArchiver;
using System;
using System.Collections.Generic;
using System.Text;
using System.IO;

namespace BigFilesArchiver.Tests
{
    [TestClass()]
    public class ArchiverTests
    {
        static string dataPath = @"..\..\..\..\Data\";

        //string inFile = Path.Combine(dataPath, "in.pdf");
        static string inFile = Path.Combine(dataPath, "in.dat");
        static string outFile = inFile + ".bfa1.cgz";
        static string unzipFile = inFile + ".bfa1.unzipped";
        static int bufferSize = 100 * 1024 * 1024;
        static int differentBufferSize = 380 * 1024 * 1024;
        static int smallerBufferSize = 75 * 1024 * 1024;


        [TestMethod()]
        public void ZipByChunksTest()
        {
            Archiver.ZipByChunks(inFile, outFile, bufferSize);
        }

        [TestMethod()]
        public void ZipByChunks_UsingSmallerBufferTest()
        {
            Archiver.ZipByChunks(inFile, outFile, smallerBufferSize);
        }

        [TestMethod()]
        public void UnzipFromChunksTest()
        {
            Archiver.UnzipFromChunks(outFile, unzipFile, bufferSize);
        }

        [TestMethod()]
        public void UnzipFromChunks_UsingDifferentBufferTest()
        {
            Archiver.UnzipFromChunks(outFile, unzipFile, differentBufferSize);
        }

        [TestMethod()]
        public void UnzipFromChunks_UsingSmallerBufferTest()
        {
            Archiver.UnzipFromChunks(outFile, unzipFile, smallerBufferSize);
        }

        [TestMethod()]
        public void UnzipFromChunks_UsingDifferentThreadNumTest()
        {
            uint threadsCount = Archiver.ChooseWorkersCount() / 2;

            Archiver.UnzipFromChunks(outFile, unzipFile, bufferSize, threadsCount);
        }

        [TestMethod()]
        public void UnzipFromChunks_UsingSmallerBufferSizeAndThreadNumTest()
        {
            uint threadsCount = Archiver.ChooseWorkersCount() / 2;

            Archiver.UnzipFromChunks(outFile, unzipFile, smallerBufferSize, threadsCount);
        }
    }
}