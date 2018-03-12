using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Hexagon
{
    public class WinExitSignal
    {
        public event EventHandler Exit;

        [DllImport("Kernel32")]
        public static extern bool SetConsoleCtrlHandler(HandlerRoutine Handler, bool Add);

        // A delegate type to be used as the handler routine
        // for SetConsoleCtrlHandler.
        public delegate bool HandlerRoutine(CtrlTypes CtrlType);

        // An enumerated type for the control messages
        // sent to the handler routine.
        public enum CtrlTypes
        {
            CTRL_C_EVENT = 0,
            CTRL_BREAK_EVENT,
            CTRL_CLOSE_EVENT,
            CTRL_LOGOFF_EVENT = 5,
            CTRL_SHUTDOWN_EVENT
        }

        /// <summary>
        /// Need this as a member variable to avoid it being garbage collected.
        /// </summary>
        private HandlerRoutine m_hr;
        private readonly IDisposable _objectToRelease;
        private readonly bool _handleCtrlC;

        public WinExitSignal(bool handleCtrlC = true, IDisposable objectToRelease = null)
        {
            m_hr = new HandlerRoutine(ConsoleCtrlCheck);
            _objectToRelease = objectToRelease;
            _handleCtrlC = handleCtrlC;

            SetConsoleCtrlHandler(m_hr, true);
        }

        /// <summary>
        /// Handle the ctrl types
        /// </summary>
        /// <param name="ctrlType"></param>
        /// <returns></returns>
        private bool ConsoleCtrlCheck(CtrlTypes ctrlType)
        {
            bool returnFalse;
            switch (ctrlType)
            {
                case CtrlTypes.CTRL_C_EVENT:
                case CtrlTypes.CTRL_BREAK_EVENT:
                    returnFalse = !_handleCtrlC;
                    break;
                default:
                    returnFalse = false;
                    break;
            }

            if (returnFalse) return false;

            _objectToRelease?.Dispose();
            Exit?.Invoke(_objectToRelease, EventArgs.Empty);
            return true;
        }


    }
}
