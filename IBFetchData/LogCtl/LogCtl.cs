using System;
using System.Collections.Generic;
using System.Text;
using System.Security.Principal;
using System.Diagnostics;
using System.IO;
using System.Security.AccessControl;
using Microsoft.Win32;

using System.Threading;

namespace SWI.Controls
{
    public enum ErrorLevel
    {
        logDEB = -1,
        logINF,
        logWRN,
        logERR,
        logSEV
    }

    /*****************************************************************************
     * Logger
     * 
     * Singleton class for managing worker thread
     * 
     * **************************************************************************/

    internal sealed class Logger : IDisposable
    {
        const string EVENTLOGNAME = "Application";
        const string EVENTSOURCENAME = "LogCtl";

        private static readonly Logger instance = new Logger ();
        private Dictionary<string, ErrorLevel> m_levellist;
        private ErrorLevel m_defaultlevel;
        private object m_lock = new object ();
        private object m_NoClients = new object ();
        private volatile bool m_bTimeToDie;
        private bool m_bIfTrace;
        private Thread m_thr = null;
        private FileStream m_fs = null;
        private Queue<string> m_queue = new Queue<string> ();
        private AutoResetEvent m_queueEvent = new AutoResetEvent (false);
        private EventWaitHandle m_RenameEvent;
        private bool m_bIfUseFixedFormatting = false;
        private int[] m_ColumnWidths = new int[6];
        private long m_pos;

        public string m_Filename;
        public int m_RetryCount = 500;
        public int m_RetryRate = 200;
        public string m_FieldSeparator = "|";
        public string m_RecordSeparator = "\r\n";

        public static Logger Instance
        {
            get
            {
                return instance;
            }
        }

        /**************************************************************************
         * Trace()
         * 
         * ***********************************************************************/

        internal void Trace (string msg)
        {
            if (m_bIfTrace)
            {
                System.Diagnostics.Trace.WriteLine (msg);
            }
        }

        /*************************************************************************
         * Attach()
         * 
         * Each client must call Attach() once
         * 
         * **********************************************************************/

        public void Attach ()
        {
            lock (m_NoClients)
            {
               Trace (string.Format ("LogCtl.Attach incrementing no. clients to {0}", (int) m_NoClients + 1));

                m_NoClients = (int) m_NoClients + 1;
                if (m_thr == null)
                {
                    m_thr = new Thread (this.LogWorker);
                    m_thr.Start ();
                }
            }
        }

        /************************************************************************
         * Constructor
         * 
         * *********************************************************************/

        private Logger ()
        {
            m_NoClients = 0;
            m_levellist = new Dictionary<string, ErrorLevel> ();

            /* Make sure LogCtl can write to the event log using "LogCtl" as the event source
             * ------------------------------------------------------------------------------
             * Vista makes this difficult, and we need to add some messy code to deal with this.
             * Throw an exception if we can't set up the event source */

            EnsureEventSourceExists ();

            try
            {
                using (RegistryKey logkey = Registry.LocalMachine.OpenSubKey ("SOFTWARE\\SWI\\LogCtl", false))
                {
                    m_Filename = logkey.GetValue ("LogFile").ToString ();
                    using (RegistryKey rk = logkey.OpenSubKey ("LogLevel"))
                    {
                        foreach (string s in rk.GetValueNames ())
                        {
                            if (string.IsNullOrEmpty (s))
                            {
                                m_defaultlevel = Trans (rk.GetValue (s).ToString ());
                            }
                            else
                            {
                                m_levellist.Add (s, Trans (rk.GetValue (s).ToString ()));
                            }
                        }
                    }
                    try
                    {
                        m_bIfTrace = ((int) logkey.GetValue ("IfTrace")) != 0;

                        TextWriterTraceListener tw = new TextWriterTraceListener (logkey.GetValue ("TraceFile").ToString ());
                        System.Diagnostics.Trace.Listeners.Add (tw);
                        System.Diagnostics.Trace.AutoFlush = true;
                        System.Diagnostics.Trace.WriteLine ("Logger() built.");
                    }
                    catch (Exception)
                    {
                        EmergencyMessage (EventLogEntryType.Warning, "Missing TraceFile in registry");
                    }
                    try
                    {
                        m_bIfUseFixedFormatting = ((int) logkey.GetValue ("IfUseFixedFormatting")) != 0;
                        string FixedFormatting = logkey.GetValue ("FixedFormatting").ToString ();
                        string[] ar = FixedFormatting.Split (new char[] { ',', ' ', ';' }, StringSplitOptions.RemoveEmptyEntries);
                        if (ar.GetUpperBound (0) != 5)
                        {
                            EmergencyMessage (EventLogEntryType.Warning, "The FixedFormatting registry entry is not specifying 6 column widths as expected.");
                            m_bIfUseFixedFormatting = false;
                        }
                        for (int i = 0; i < 6; i++)
                        {
                            m_ColumnWidths[i] = int.Parse (ar[i]);
                        }
                    }
                    catch (Exception)
                    {
                        EmergencyMessage (EventLogEntryType.Warning, "IfUseFixedFormatting or FixedFormatting registry entries");
                    }
                }
            }
            catch (Exception)
            {
                /* Possibly this is the first time LogCtl has ever been used
                 * ---------------------------------------------------------
                 * so create some default settings */

                try
                {
                    using (RegistryKey logkey = Registry.LocalMachine.CreateSubKey ("SOFTWARE\\SWI\\LogCtl"))
                    {
                        logkey.SetValue ("LogFile", "C:\\Logs\\log.log");
                        using (RegistryKey rk = logkey.CreateSubKey ("LogLevel"))
                        {
                            rk.SetValue ("", "INF");
                        }
                        logkey.SetValue ("IfTrace", 0);
                        logkey.SetValue ("TraceFile", "C:\\Logs\\trace.log");
                        logkey.SetValue ("RenameEventName", "Global\\STE LogCtl-Rename");
                        logkey.SetValue ("IfUseFixedFormatting", 0);
                        logkey.SetValue ("FixedFormatting", "20 12 5 4 13 26");
                    }
                }
                catch (UnauthorizedAccessException)
                {
                    throw new Exception ("LogCtl could not read or write initialization settings. Please run as administrator and try again.");
                }
                catch (Exception e)
                {
                    throw new Exception (string.Format ("Unexpected exception encountered in LogCtl. {0}", e.Message));
                }
            }

            /* Prepare m_RenameEvent
             * ---------------------
             * add retries, Sept 17, 2009 -- B.Muth */

            int retries = 0;
TryAgain:
            
            try
            {
                using (RegistryKey logkey = Registry.LocalMachine.OpenSubKey ("SOFTWARE\\SWI\\LogCtl", false))
                {
                    string RenameEventName;


                    RenameEventName = logkey.GetValue ("RenameEventName").ToString ();

                    EventWaitHandleSecurity sec = new EventWaitHandleSecurity ();
                    //EventWaitHandleAccessRule rule = new EventWaitHandleAccessRule ("Everyone",
                    //                                                                EventWaitHandleRights.Modify | EventWaitHandleRights.Synchronize,
                    //                                                                AccessControlType.Allow);
                    //EventWaitHandleAccessRule rule = new EventWaitHandleAccessRule (new SecurityIdentifier ("S-1-1-0"), EventWaitHandleRights.FullControl, AccessControlType.Allow);
                    // change from 'Everyone' to 'Users'
                    EventWaitHandleAccessRule rule = new EventWaitHandleAccessRule (new SecurityIdentifier ("S-1-5-32-545"), EventWaitHandleRights.FullControl, AccessControlType.Allow);
                    sec.AddAccessRule (rule);
                    bool bCreated;

                    try
                    {
                        m_RenameEvent = new EventWaitHandle (true, EventResetMode.ManualReset, RenameEventName, out bCreated, sec);
                    }
                    catch (Exception e)
                    {
                        EmergencyMessage (EventLogEntryType.Error, string.Format ("EventWaitHandle() failed in an attempt to create the RenameEventname. {0}", e.Message));
                        if (++retries < 10)
                        {
                            Thread.Sleep (500);
                            goto TryAgain;
                        }
                        m_RenameEvent = null;
                    }
                }
            }
            catch (Exception )
            {
                throw new Exception ("RenameEventName registry entry is missing, or other exception encoutered. Please check the Application Event log.");
            }
        }

        /********************************************************************
        * EnsureEventSourceExists()
        * 
        * *****************************************************************/

        public void EnsureEventSourceExists ()
        {
            try
            {

                // Extra Raw event data can be added (later) if needed
                byte[] rawEventData = Encoding.ASCII.GetBytes ("");

                /* Check whether the Event Source exists.
                 * --------------------------------------
                 * It is possible that this may raise a security exception if the 
                 * current process account doesn't have permissions for all sub-keys 
                 * under HKEY_LOCAL_MACHINE\System\CurrentControlSet\Services\EventLog */

                string keyName = @"SYSTEM\CurrentControlSet\Services\EventLog\" + EVENTLOGNAME;

                RegistryKey rkEventSource = Registry.LocalMachine.OpenSubKey (keyName + @"\" + EVENTSOURCENAME);

                /* Check whether key exists
                 * ------------------------ */

                if (rkEventSource == null)
                {
                    /* Key does not exist. Create key which represents source
                     * ------------------------------------------------------ */

                    rkEventSource = Registry.LocalMachine.CreateSubKey (keyName + @"\" + EVENTSOURCENAME);
                }

                /* Now validate that the .NET Event Message File, EventMessageFile.dll
                 * -------------------------------------------------------------------
                 * (which correctly formats the content in a Log Message) is set for the event source */

                object eventMessageFile = rkEventSource.GetValue ("EventMessageFile");

                if (eventMessageFile == null)
                {
                    /* Source Event File Doesn't exist
                     * -------------------------------
                     * determine .NET framework location, for Event Messages file. */

                    RegistryKey dotNetFrameworkSettings = Registry.LocalMachine.OpenSubKey (@"SOFTWARE\Microsoft\.NetFramework\");

                    if (dotNetFrameworkSettings != null)
                    {

                        object dotNetInstallRoot = dotNetFrameworkSettings.GetValue ("InstallRoot",
                                                                                     null,
                                                                                     RegistryValueOptions.None);

                        if (dotNetInstallRoot != null)
                        {
                            string eventMessageFileLocation = dotNetInstallRoot.ToString () +
                                                                "v" +
                                                                System.Environment.Version.Major.ToString () + "." +
                                                                System.Environment.Version.Minor.ToString () + "." +
                                                                System.Environment.Version.Build.ToString () +
                                                                @"\EventLogMessages.dll";

                            /* Validate File exists
                             * -------------------- */

                            if (System.IO.File.Exists (eventMessageFileLocation))
                            {

                                /* The Event Message File exists 
                                 * in the anticipated location on the machine. 
                                 * Set this value for the new Event Source */

                                /* Re-open the key as writable
                                 * --------------------------- */

                                rkEventSource = Registry.LocalMachine.OpenSubKey (keyName + @"\" + EVENTSOURCENAME, true);

                                /* Set the "EventMessageFile" property
                                 * ----------------------------------- */

                                rkEventSource.SetValue ("EventMessageFile", eventMessageFileLocation, RegistryValueKind.String);
                            }
                        }
                    }

                    dotNetFrameworkSettings.Close ();
                }

                rkEventSource.Close ();

            }
            catch (UnauthorizedAccessException e)
            {
                throw new UnauthorizedAccessException (string.Format ("LogCtl needs to initialize the registry. Please re-run as administrator at least once. {0}", e.Message));
            }
            catch (Exception e)
            {
                throw e;
            }
        }

        /********************************************************************
         * Rename the log file
         * 
         * *****************************************************************/

        public string Rename ()
        {
            if (m_RenameEvent != null)
            {
                m_RenameEvent.Reset ();
            }

            StringBuilder sb = new StringBuilder (Path.GetDirectoryName (m_Filename));
            sb.Append ('\\');
            sb.Append (Path.GetFileNameWithoutExtension (m_Filename));
            sb.Append (DateTime.Now.ToString ("_yyyy-MMM-dd HH-mm-ss"));
            sb.Append (Path.GetExtension (m_Filename));
            string oldfilename = sb.ToString ();

            for (int i = 0; i < 60; i++)    // Wait up to 30 seconds
            {

                try
                {
                    File.Move (m_Filename, oldfilename);
                    if (m_RenameEvent != null)
                    {
                        m_RenameEvent.Set ();
                    }
                    return oldfilename;

                }
                catch (Exception)
                {
                    Thread.Sleep (500);
                }
            }
            EmergencyMessage (EventLogEntryType.Error, "Failed to rename the log file within the 30 second time limit");
            return null;
        }

        /********************************************************************
         * Dispose()
         * 
         * *****************************************************************/

        public void Dispose ()
        {
            lock (m_NoClients)
            {
                Trace (string.Format ("Logger.Dispose decrementing no. clients to {0}", (int) m_NoClients - 1));
                m_NoClients = (int) m_NoClients - 1;
                Trace (string.Format ("LogCtl.Dispose {0} clients", m_NoClients.ToString ()));
                if ((int) m_NoClients <= 0)
                {
                    m_bTimeToDie = true;
                    m_queueEvent.Set ();
                    Trace ("Logger.Dispose signaling singleton thread to terminate");
                    if (!m_thr.Join (1000 * 20)) // twenty seconds
                    {
                        EmergencyMessage (EventLogEntryType.Error, "LogCtl worker thread not shutting down. Aborting. Log messages may be lost.");
                        Trace ("Logger.Dispose aborting thread");
                        m_thr.Abort ();
                    }
                    m_thr = null;
                    if (m_fs != null)
                    {
                        m_fs.Flush ();
                        m_fs.Close ();
                        m_fs.Dispose ();
                        m_fs = null;
                    }
                }
            }
            Trace ("Logger.Dispose disposed.");
        }

        /********************************************************************
         * LogWorker
         * 
         * The singleton thread routine
         * 
         * *****************************************************************/

        public void LogWorker ()
        {
            while (true)
            {
                if (m_queueEvent.WaitOne (2000))
                {
                    StringBuilder sb = new StringBuilder ();

                    lock (m_lock)
                    {
                        Trace (string.Format ("LogWorker examining queue. size {0}", m_queue.Count));

                        /* Dequeue 20 at a time
                         * -------------------- */

                        if (m_bTimeToDie)
                        {
                            while (m_queue.Count > 0)
                            {
                                sb.Append (m_queue.Dequeue ());
                            }
                            if (sb.Length > 0)
                            {
                                Output (sb.ToString ());
                            }
                            return;
                        }
                        else
                        {
                            int i;

                            for (i = 0; i < 20 & m_queue.Count > 0; i++)
                            {
                                sb.Append (m_queue.Dequeue ());
                            }
                            Trace (string.Format ("Removed {0} messages for logging.", i));
                        }
                    }
                    if (sb.Length > 0)
                    {
                        Output (sb.ToString ());
                    }
                }
                else
                {
                    Trace ("No queue events for 2 seconds.");
                    if (m_fs != null)
                    {
                        Trace ("Closing FileStream.");
                        m_fs.Flush ();
                        m_fs.Close ();
                        m_fs.Dispose ();
                        m_fs = null;
                    }
                    if (m_bTimeToDie)
                    {
                        return;
                    }
                }
            }
        }

        /*************************************************************************
         * Output
         * 
         * output the log message to the log file
         * 
         * **********************************************************************/

        private void Output (string msg)
        {
            /* Check whether some LogCtl somewhere is needing to rename the file
             * ----------------------------------------------------------------- */

            if (m_RenameEvent != null)
            {
                try
                {
                    while (!m_RenameEvent.WaitOne (0, true))
                    {
                        if (m_fs != null)
                        {
                            m_fs.Flush ();
                            m_fs.Close ();
                            m_fs.Dispose ();
                            m_fs = null;
                        }

                        /* Some other logctl instance has reset the RenameEvent event
                         * ----------------------------------------------------------
                         * so wait up to 30 seconds for it to be reset */

                        if (!m_RenameEvent.WaitOne (30 * 1000, true))
                        {
                            /* Can't wait any longer
                             * --------------------- */

                            EmergencyMessage (EventLogEntryType.Warning, "The RenameEvent has been set for more than 30 seconds.");
                        }
                    }
                }
                catch (AbandonedMutexException e)
                {
                    EmergencyMessage (EventLogEntryType.Error, string.Format ("The Global Rename Event has been abandoned. {0}", e.Message));
                }
            }

            if (m_fs == null)
            {
                Trace ("Filestream closed. Opening a new one");
                try
                {
                    m_fs = new FileStream (m_Filename, FileMode.OpenOrCreate, FileAccess.Write, FileShare.ReadWrite);
                }
                catch (Exception e)
                {
                    throw new Exception (string.Format ("Failed to open {0}. {1}", m_Filename, e.Message));
                }
            }
            if (!TryLock ())
            {
                EmergencyMessage (EventLogEntryType.Error, string.Format ("Lock failed. msg={0}", msg));
            }
            UTF8Encoding ue = new UTF8Encoding ();
            try
            {
                m_fs.Write (ue.GetBytes (msg), 0, ue.GetByteCount (msg));
                m_fs.Flush ();
            }
            catch (Exception e)
            {
                EmergencyMessage (EventLogEntryType.Error, string.Format ("Write failure to log file {0}. {1}", m_Filename, e.Message));
            }

            if (!TryUnlock ())
            {
                EmergencyMessage (EventLogEntryType.Error, "Failed to unlock file.");
            }
        }

        /****************************************************************
         * TryLock
         * 
         * Try locking the file
         * 
         * ************************************************************/

        private bool TryLock ()
        {
            for (int i = 0; i < m_RetryCount; i++)
            {
                try
                {
                    m_pos = m_fs.Seek (0, SeekOrigin.End);
                    m_fs.Lock (m_pos, 10000);
                    m_fs.Seek (0, SeekOrigin.End);  // just to be sure
                    return true;
                }
                catch (IOException e)
                {
                    Trace (string.Format ("TryLock exception. {0}", e.Message));
                    Thread.Sleep (m_RetryRate);
                }
            }
            return false;
        }

        /****************************************************************
         * TryUnlock
         * 
         * Try unlocking the file
         * 
         * ************************************************************/

        private bool TryUnlock ()
        {
            for (int i = 0; i < m_RetryCount; i++)
            {
                try
                {
                    m_fs.Unlock (m_pos, 10000);
                    return true;
                }
                catch (IOException e)
                {
                    Trace (string.Format ("TryUnlock exception. {0}", e.Message));
                    Thread.Sleep (m_RetryRate);
                }
            }
            return false;
        }

        /************************************************************************
         * EmergencyMessage
         * 
         * Send an emergency message to the event log. Some unexpected condition
         * has occurred which is preventing LogCtl from logging complete
         * messages
         * 
         * Because of Vista restrictions, this code may through an exception which
         * we want to avoid. If we can't log an emergency message, skip it.
         * 
         * *********************************************************************/

        private void EmergencyMessage (EventLogEntryType et, string p)
        {
            string sSource = EVENTSOURCENAME;

            try
            {
                EventLog.WriteEntry (sSource, p, et, 0);
            }
            catch (Exception)
            {
            }
        }

        /*********************************************************************
         * Trans()
         * 
         * Tranlate Errorlevel to a string
         * 
         * ******************************************************************/

        public ErrorLevel Trans (string level)
        {
            switch (level)
            {
                case "DEB":
                    return ErrorLevel.logDEB;

                default:
                case "INF":
                    return ErrorLevel.logINF;

                case "WRN":
                    return ErrorLevel.logWRN;

                case "ERR":
                    return ErrorLevel.logERR;

                case "SEV":
                    return ErrorLevel.logSEV;
            }
        }

        /**********************************************************************************
         * Logger::Log ()
         * 
         * internal log message for processing log message to the internal queue
         * 
         * *******************************************************************************/

        public void Log (string AppName, string ComputerName, string User, string ProcessId, ErrorLevel level, string msg)
        {
            ErrorLevel thresholdlevel = m_defaultlevel;

            if (m_levellist.Count > 0)
            {
                if (m_levellist.ContainsKey (AppName))
                {
                    thresholdlevel = m_levellist[AppName];
                }
            }

            if (level >= thresholdlevel)
            {
                Add (FormatMsg (AppName, ComputerName, User, ProcessId, level, msg));
            }
        }

        /*******************************************************************************
         * FormatMsg
         * 
         * format the message for logging
         * 
         * ****************************************************************************/

        public string FormatMsg (string AppName, string ComputerName, string User, string ProcessId, ErrorLevel level, string msg)
        {
            if (m_bIfUseFixedFormatting)
            {
                int width = m_ColumnWidths[0];
                string sb = User;
                sb = sb.PadRight (width);
                sb += AppName;
                width += m_ColumnWidths[1];
                sb = sb.PadRight (width);
                sb += ProcessId;
                width += m_ColumnWidths[2];
                sb = sb.PadRight (width);

                switch (level)
                {
                    case ErrorLevel.logDEB:
                        sb += "DEB";
                        break;
                    case ErrorLevel.logINF:
                        sb += "INF";
                        break;
                    case ErrorLevel.logWRN:
                        sb += "WRN";
                        break;
                    case ErrorLevel.logERR:
                        sb += "ERR";
                        break;
                    case ErrorLevel.logSEV:
                        sb += "SEV";
                        break;
                }
                width += m_ColumnWidths[3];
                sb = sb.PadRight (width);
                sb += ComputerName;
                width += m_ColumnWidths[4];
                sb = sb.PadRight (width);
                sb += DateTime.Now.ToString ("yyyy-MMM-dd HH:mm:ss.ffff");
                width += m_ColumnWidths[5];
                sb = sb.PadRight (width);
                sb += msg;
                sb += m_RecordSeparator;
                return sb;
            }
            else
            {
                StringBuilder sb = new StringBuilder (User);
                sb.Append (m_FieldSeparator);
                sb.Append (AppName);
                sb.Append (m_FieldSeparator);
                sb.Append (ProcessId);
                sb.Append (m_FieldSeparator);

                switch (level)
                {
                    case ErrorLevel.logDEB:
                        sb.Append ("DEB");
                        break;
                    case ErrorLevel.logINF:
                        sb.Append ("INF");
                        break;
                    case ErrorLevel.logWRN:
                        sb.Append ("WRN");
                        break;
                    case ErrorLevel.logERR:
                        sb.Append ("ERR");
                        break;
                    case ErrorLevel.logSEV:
                        sb.Append ("SEV");
                        break;
                }
                sb.Append (m_FieldSeparator);
                sb.Append (ComputerName);
                sb.Append (m_FieldSeparator);
                sb.Append (DateTime.Now.ToString ("yyyy-MMM-dd HH:mm:ss.ffff"));
                sb.Append (m_FieldSeparator);
                sb.Append (msg);
                sb.Append (m_RecordSeparator);
                return sb.ToString ();
            }
        }

        private void Add (string msg)
        {
            lock (m_lock)
            {
                m_queue.Enqueue (msg);
            }
            m_queueEvent.Set ();
        }
    }

    /**************************************************************************
     * LogCtl class
     * 
     * One object per user. The LogCtl class in version 8 creates a singleton
     * Logger class, so that only one worker thread is created for the
     * client process.
     * 
     * ************************************************************************/

    public class LogCtl : IDisposable
    {
        private Logger m_logger;
        private string m_appname;
        private string m_ProcessId;
        private string m_ComputerName;
        private string m_User;
        private Stopwatch m_stopwatch = null;
        private bool m_bIfLogging;

        public bool IfLogging
        {
            get
            {
                return m_bIfLogging;
            }
            set
            {
                m_bIfLogging = value;
            }
        }

        public string LogFilename
        {
            get
            {
                return m_logger.m_Filename;
            }
            set
            {
                m_logger.m_Filename = value;
            }
        }

        public string RecordSeparator
        {
            get
            {
                return m_logger.m_RecordSeparator;
            }
            set
            {
                m_logger.m_FieldSeparator = value;
            }
        }

        public string FieldSeparator
        {
            get
            {
                return m_logger.m_FieldSeparator;
            }
            set
            {
                m_logger.m_FieldSeparator = value;
            }
        }

        public string AppName
        {
            get
            {
                return m_appname;
            }
        }

        public LogCtl (string APPNAME)
        {

            try
            {
                m_logger = Logger.Instance;
            }
            catch (TypeInitializationException e)
            {
                throw e.InnerException;
            }
            m_logger.Trace (string.Format ("[{0}] LogCtl() called", APPNAME));
            m_logger.Attach ();
            m_appname = APPNAME;
            m_ProcessId = Process.GetCurrentProcess ().Id.ToString ();
            m_ComputerName = Environment.MachineName;
            m_User = WindowsIdentity.GetCurrent ().Name.ToString ();
            IfLogging = true;
        }

        public void Dispose ()
        {
            m_logger.Trace (string.Format ("[{0}] LogCtl.Dispose called", m_appname));
            m_logger.Dispose ();
        }

        public void Log (ErrorLevel level, string msg)
        {
            Log (m_appname, level, msg);
        }

        public void Log (string AppName, ErrorLevel level, string msg)
        {
            if (IfLogging)
            {
                m_logger.Log (AppName, m_ComputerName, m_User, m_ProcessId, level, msg);
            }
        }

        public void TimerStart ()
        {
            m_stopwatch = Stopwatch.StartNew ();
        }

        public double TimerLog (string msg)
        {
            return TimerLog (m_appname, msg);
        }

        public double TimerLog (string AppName, string msg)
        {
            double elapsed = m_stopwatch.ElapsedMilliseconds / 1000.0;
            Log (AppName, ErrorLevel.logINF, string.Format ("{0} Elapsed time: {1:0.#####} seconds.", msg, elapsed));
            return elapsed;
        }

        public string Rename ()
        {
            return m_logger.Rename ();
        }
    }
}
