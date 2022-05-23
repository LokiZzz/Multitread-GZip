using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.IO.Compression;
using System.Threading;
using System.Diagnostics;

namespace GZipTest
{
    class Program
    {
        private static ManualResetEvent _thrStopEvent = new ManualResetEvent(false);
        private static ManualResetEvent _thrConsolePrintFinishEvent = new ManualResetEvent(false);
        private static ManualResetEvent _thrQueueHasMsgEvent = new ManualResetEvent(false);
        private static List<Object> _lstConsoleMessagesQueue = new List<Object>();
        private static Boolean _blnHasErrors = false;

        private static ConsoleMessenger _consoleMessenger = new ConsoleMessenger(
                    _thrStopEvent,
                    _thrQueueHasMsgEvent,
                    _lstConsoleMessagesQueue,
                    _thrConsolePrintFinishEvent
        );

        static int Main(String[] p_arrArgs)
        {
            try
            {
                Process currentProcess = Process.GetCurrentProcess();
                currentProcess.PriorityClass = ProcessPriorityClass.RealTime;
                Thread thrConsoleWriterThread = new Thread(() => _consoleMessenger.PrintMessagesToConsole());
                thrConsoleWriterThread.Start();
                ValidationHelper.ValidateArgs(p_arrArgs);
                Console.CancelKeyPress += new ConsoleCancelEventHandler(CancelKeyPress);

                using (Zipper gzipArchiver = GetZipperByArg(p_arrArgs[0]))
                {
                    gzipArchiver.ProcessFile(p_arrArgs[1], p_arrArgs[2]);
                    WaitHandle.WaitAll(new WaitHandle[] { _thrStopEvent, _thrConsolePrintFinishEvent });
                }

                return _blnHasErrors ? 1 : 0;
            }
            catch (Exception exErr)
            {
                _thrStopEvent.Set();
                _thrConsolePrintFinishEvent.WaitOne(0);
                _consoleMessenger.PrintMessage(exErr, out _blnHasErrors);
                _consoleMessenger.PrintHelp();
                return 1;
            }
            finally
            {
                DisposeResetEvents();
            }
        }

        private static Zipper GetZipperByArg(String p_strZipperType)
        {
            if (p_strZipperType == "compress")
            {
                return new Compressor(_thrStopEvent, _thrQueueHasMsgEvent, _lstConsoleMessagesQueue);
            }
            if (p_strZipperType == "decompress")
            {
                return new Decompressor(_thrStopEvent, _thrQueueHasMsgEvent, _lstConsoleMessagesQueue);
            }
            else
            {
                throw new ArgumentException();
            }
        }

        private static void CancelKeyPress(object sender, ConsoleCancelEventArgs e)
        {
            _thrStopEvent.Set();
        }

        private static void DisposeResetEvents()
        {
            _thrStopEvent?.Close();
            _thrConsolePrintFinishEvent?.Close();
            _thrQueueHasMsgEvent?.Close();
        }
    }
}
