using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace GZipTest
{
    public class ConsoleMessenger
    {
        private ManualResetEvent _thrStopEvent;
        private ManualResetEvent _thrQueueHasMsgEvent;
        private List<Object> _lstConsoleMessagesQueue;
        private ManualResetEvent _thrConsolePrintFinishEvent;

        public ConsoleMessenger(ManualResetEvent p_thrStopEvent, ManualResetEvent p_thrQueueHasMsgEvent,
            List<Object> p_lstConsoleMessagesQueue, ManualResetEvent p_thrConsolePrintFinishEvent)
        {
            _thrStopEvent = p_thrStopEvent;
            _thrQueueHasMsgEvent = p_thrQueueHasMsgEvent;
            _thrConsolePrintFinishEvent = p_thrConsolePrintFinishEvent;
            _lstConsoleMessagesQueue = p_lstConsoleMessagesQueue;

        }

        public void PrintMessagesToConsole()
        {
            while (!_thrStopEvent.WaitOne(0) || _lstConsoleMessagesQueue.Count > 0)
            {
                lock (_lstConsoleMessagesQueue)
                {
                    if (_lstConsoleMessagesQueue.Count > 0)
                    {
                        foreach (Object objMsg in _lstConsoleMessagesQueue)
                        {
                            PrintMessage(objMsg, out bool blnHasErrors);
                        }
                        _lstConsoleMessagesQueue.Clear();
                        _thrQueueHasMsgEvent.Reset();
                    }
                }
            }
            _thrConsolePrintFinishEvent.Set();
        }

        public void PrintMessage(Object p_objMsg, out bool op_blnHasErrors)
        {
            op_blnHasErrors = false;
            ConsoleColor cmdPrevColor = Console.ForegroundColor;
            try
            {
                Exception exErr = p_objMsg as Exception;
                if (exErr != null)
                {
                    op_blnHasErrors = true;
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"ERROR: {exErr.Message}\n");
                }
                else
                {
                    ColoredMessage objColoredMsg = p_objMsg as ColoredMessage;
                    if (objColoredMsg != null)
                    {
                        Console.ForegroundColor = objColoredMsg.Color;
                        Console.WriteLine($"{objColoredMsg.Msg.ToString()}\n");
                    }
                    else
                    {
                        Console.ForegroundColor = ConsoleColor.White;
                        Console.WriteLine($"{p_objMsg.ToString()}\n");
                    }
                }
            }
            finally
            {
                Console.ForegroundColor = cmdPrevColor;
            }
        }

        public void PrintHelp()
        {
            String strHelpMessage = "Zip: GZipTest.exe compress [Source file path] [Destination file path]\n"
                + "Unzip: GZipTest.exe decompress [Compressed file path] [Destination file path]\n"
                + "Abort: CTRL + C\n"
            ;
            PrintMessage(strHelpMessage, out bool blnHasErrors);
        }
    }
}
